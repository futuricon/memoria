using FluentValidation;

using Memoria.Reviews.Contracts;
using Memoria.Reviews.Contracts.Commands;

namespace Memoria.Reviews.Features.RecordReview;

internal sealed class RecordReviewCommandValidator : AbstractValidator<RecordReviewCommand>
{
    public RecordReviewCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.CardId).NotEmpty();
        RuleFor(c => c.Note).MaximumLength(1000).When(c => c.Note is not null);
        RuleFor(c => c.AnswerText)
            .MaximumLength(ReviewConstraints.MaxAnswerLength)
            .When(c => c.AnswerText is not null);
        RuleFor(c => c.AiScore!.Value)
            .InclusiveBetween(0, 100)
            .When(c => c.AiScore is not null);
    }
}
