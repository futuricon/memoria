using FluentValidation;

using Memoria.Users.Contracts.Commands;

namespace Memoria.Users.Features.UpdateUserPreferences;

internal sealed class UpdateUserPreferencesCommandValidator : AbstractValidator<UpdateUserPreferencesCommand>
{
    public UpdateUserPreferencesCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.TimeZoneId)
            .NotEmpty()
            .Must(BeValidTimeZone)
            .WithMessage("'{PropertyValue}' is not a valid IANA timezone identifier.");
    }

    private static bool BeValidTimeZone(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(id);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }
}
