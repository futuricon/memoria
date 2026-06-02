using MediatR;

using Memoria.Reminders.Contracts.Dtos;
using Memoria.Shared.Kernel.Results;

namespace Memoria.Reminders.Contracts.Queries;

/// <summary>
/// Reminders the user has already received in Telegram (status
/// <c>Sent</c>) but never confirmed or skipped. These are the "I read it,
/// got distracted, never rated" cases that would otherwise stay invisible
/// in the SPA — the dashboard widget surfaces them so the user can finish
/// the rating without going back to the bot.
/// </summary>
public sealed record GetPendingRatingsForUserQuery(Guid UserId, int Take = 10)
    : IRequest<Result<IReadOnlyList<DueReminderDto>>>;
