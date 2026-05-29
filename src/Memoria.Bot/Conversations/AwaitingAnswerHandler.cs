using System.Globalization;

using MediatR;

using Memoria.AI.Contracts.Abstractions;
using Memoria.AI.Contracts.Dtos;
using Memoria.Bot.Services;
using Memoria.Cards.Contracts.Queries;
using Memoria.Reviews.Contracts;
using Memoria.Reviews.Contracts.Commands;

using Microsoft.Extensions.Logging;

using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Memoria.Bot.Conversations;

/// <summary>
/// Continuation handler for the <see cref="AwaitingAnswerState"/>: takes the
/// user's free-text answer to a Question-card reminder, grades it against the
/// card's reference answer via <see cref="IAnswerGrader"/>, records the review
/// (auto-graded — this also confirms the reminder and schedules the next one
/// through the Reviews→Reminders event), and replies with the verdict.
/// </summary>
internal sealed class AwaitingAnswerHandler : IConversationContinuationHandler
{
    private readonly ITelegramBotClient _client;
    private readonly IMediator _mediator;
    private readonly IAnswerGrader _grader;
    private readonly CurrentUserResolver _resolver;
    private readonly IConversationStateStore _conversations;
    private readonly ILogger<AwaitingAnswerHandler> _logger;

    public AwaitingAnswerHandler(
        ITelegramBotClient client,
        IMediator mediator,
        IAnswerGrader grader,
        CurrentUserResolver resolver,
        IConversationStateStore conversations,
        ILogger<AwaitingAnswerHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(mediator);
        ArgumentNullException.ThrowIfNull(grader);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(conversations);
        ArgumentNullException.ThrowIfNull(logger);
        _client = client;
        _mediator = mediator;
        _grader = grader;
        _resolver = resolver;
        _conversations = conversations;
        _logger = logger;
    }

    public string DialogName => "AwaitingAnswer";

    public async Task HandleAsync(Message message, ConversationState state, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(state);

        if (state is not AwaitingAnswerState awaiting)
        {
            return;
        }

        var chatId = message.Chat.Id;
        var answer = (message.Text ?? string.Empty).Trim();

        if (answer.Length == 0)
        {
            await Send(chatId, "✍️ Please reply with your answer.", ct).ConfigureAwait(false);
            return; // keep awaiting
        }

        if (answer.Length > ReviewConstraints.MaxAnswerLength)
        {
            await Send(
                chatId,
                $"❌ Answer is too long (max {ReviewConstraints.MaxAnswerLength.ToString(CultureInfo.InvariantCulture)} characters). Try a shorter answer:",
                ct).ConfigureAwait(false);
            return; // keep awaiting
        }

        var resolved = await _resolver.ResolveAsync(message.From!.Id, ct).ConfigureAwait(false);
        if (resolved.IsFailure)
        {
            _conversations.Clear(chatId);
            await Send(chatId, "❌ Not linked. Use /start first.", ct).ConfigureAwait(false);
            return;
        }

        var userId = resolved.Value!.UserId;

        var cardResult = await _mediator
            .Send(new GetCardByIdQuery(userId, awaiting.CardId, IncludeDeleted: false), ct)
            .ConfigureAwait(false);
        if (cardResult.IsFailure)
        {
            _conversations.Clear(chatId);
            await Send(chatId, $"❌ {cardResult.Error!.Message}", ct).ConfigureAwait(false);
            return;
        }

        var card = cardResult.Value!;

        // Instant feedback: the LLM call takes a few seconds, so acknowledge
        // immediately and edit this same message with the verdict when done.
        var thinking = await _client
            .SendMessage(chatId, "⏳ Checking your answer…", cancellationToken: ct)
            .ConfigureAwait(false);
        var messageId = thinking.MessageId;

        var grade = await _grader
            .GradeAsync(new GradingRequest(card.Title, card.Body, answer), ct)
            .ConfigureAwait(false);
        if (grade.IsFailure)
        {
            _logger.LogWarning(
                "Grading unavailable for reminder {ReminderId} ({Code}); keeping awaiting state",
                awaiting.ReminderId, grade.Error!.Code);
            await Edit(chatId, messageId, "⚠️ Couldn't grade your answer right now. Please try again in a moment.", ct)
                .ConfigureAwait(false);
            return; // keep awaiting — transient
        }

        var result = grade.Value!;
        var rating = AnswerRatingMapper.FromScore(result.Score);

        var record = await _mediator.Send(
            new RecordReviewCommand(
                userId,
                awaiting.CardId,
                awaiting.ReminderId,
                rating,
                Note: null,
                AnswerText: answer,
                AiScore: result.Score,
                AiFeedback: result.Feedback,
                AutoGraded: true),
            ct).ConfigureAwait(false);

        _conversations.Clear(chatId);

        await Edit(
            chatId,
            messageId,
            record.IsFailure ? $"❌ {record.Error!.Message}" : BuildResultText(result, card.Body),
            ct).ConfigureAwait(false);
    }

    private static string BuildResultText(GradingResult result, string referenceBody)
    {
        var emoji = result.Verdict switch
        {
            GradingVerdict.Correct => "✅",
            GradingVerdict.Partial => "🟡",
            _ => "❌",
        };

        var score = result.Score.ToString(CultureInfo.InvariantCulture);
        return
            $"{emoji} Score: {score}/100\n\n" +
            (string.IsNullOrWhiteSpace(result.Feedback) ? string.Empty : Escape(result.Feedback) + "\n\n") +
            "📖 Reference answer:\n" +
            Escape(referenceBody);
    }

    private async Task Send(long chatId, string text, CancellationToken ct)
    {
        await _client.SendMessage(chatId, text, parseMode: ParseMode.Markdown, cancellationToken: ct)
            .ConfigureAwait(false);
    }

    private async Task Edit(long chatId, int messageId, string text, CancellationToken ct)
    {
        try
        {
            await _client.EditMessageText(chatId, messageId, text, parseMode: ParseMode.Markdown, cancellationToken: ct)
                .ConfigureAwait(false);
        }
        catch (ApiRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to edit grading message {MessageId}; sending new", messageId);
            await Send(chatId, text, ct).ConfigureAwait(false);
        }
    }

    private static string Escape(string s) =>
        s
            .Replace("_", "\\_", StringComparison.Ordinal)
            .Replace("*", "\\*", StringComparison.Ordinal)
            .Replace("`", "\\`", StringComparison.Ordinal)
            .Replace("[", "\\[", StringComparison.Ordinal);
}
