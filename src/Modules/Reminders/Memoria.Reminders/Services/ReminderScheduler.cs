using Memoria.Reminders.Domain;
using Memoria.Reminders.Options;
using Memoria.Users.Contracts.Dtos;
using Microsoft.Extensions.Options;

namespace Memoria.Reminders.Services;

internal sealed class ReminderScheduler
{
    private const int RequiredIntervalCount = 5;

    private readonly RemindersOptions _options;

    public ReminderScheduler(IOptions<RemindersOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    public IReadOnlyList<Reminder> CreateScheduleFor(
        Guid cardId,
        Guid userId,
        UserPreferencesDto preferences,
        DateTime anchorUtc)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        if (_options.Intervals.Count != RequiredIntervalCount)
        {
            throw new InvalidOperationException(
                $"RemindersOptions.Intervals must contain exactly {RequiredIntervalCount} entries, " +
                $"actual: {_options.Intervals.Count}.");
        }

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(preferences.TimeZoneId);
        var reminders = new List<Reminder>(RequiredIntervalCount);

        for (var stage = 1; stage <= RequiredIntervalCount; stage++)
        {
            var naiveUtc = anchorUtc + _options.Intervals[stage - 1];
            var local = TimeZoneInfo.ConvertTimeFromUtc(naiveUtc, timeZone);
            var shiftedLocal = ShiftIfInQuietHours(local, preferences.QuietHoursStart, preferences.QuietHoursEnd);
            var scheduledAtUtc = TimeZoneInfo.ConvertTimeToUtc(shiftedLocal, timeZone);

            reminders.Add(new Reminder(cardId, userId, stage, scheduledAtUtc));
        }

        return reminders;
    }

    private static DateTime ShiftIfInQuietHours(
        DateTime localTime,
        TimeOnly? quietStart,
        TimeOnly? quietEnd)
    {
        if (quietStart is null || quietEnd is null)
        {
            return localTime;
        }

        var start = quietStart.Value;
        var end = quietEnd.Value;
        var current = TimeOnly.FromDateTime(localTime);
        var wraps = start > end;

        bool insideWindow = wraps
            ? current >= start || current < end
            : current >= start && current < end;

        if (!insideWindow)
        {
            return localTime;
        }

        // We're inside the quiet window — shift to end-of-window.
        // For wrapping window:
        //   - if current is in the "after-start" half (current >= start) → end is next day.
        //   - if current is in the "before-end" half (current < end)    → end is same day.
        // For non-wrapping window: end is always same day.
        var targetDate = (wraps && current >= start)
            ? localTime.Date.AddDays(1)
            : localTime.Date;

        // Preserve Kind (Unspecified after ConvertTimeFromUtc).
        return DateTime.SpecifyKind(targetDate.Add(end.ToTimeSpan()), localTime.Kind);
    }
}
