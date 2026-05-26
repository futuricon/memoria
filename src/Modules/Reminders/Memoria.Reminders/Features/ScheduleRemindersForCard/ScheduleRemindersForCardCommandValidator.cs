using FluentValidation;

using Memoria.Reminders.Contracts.Commands;

namespace Memoria.Reminders.Features.ScheduleRemindersForCard;

internal sealed class ScheduleRemindersForCardCommandValidator
    : AbstractValidator<ScheduleRemindersForCardCommand>
{
    public ScheduleRemindersForCardCommandValidator()
    {
        RuleFor(c => c.CardId).NotEmpty();
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.AnchorUtc)
            .Must(d => d.Kind == DateTimeKind.Utc)
            .WithMessage("AnchorUtc must be UTC (DateTimeKind.Utc).");
    }
}
