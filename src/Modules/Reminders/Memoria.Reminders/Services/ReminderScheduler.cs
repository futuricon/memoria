using Memoria.Reminders.Domain;
using Memoria.Reminders.Options;
using Memoria.Reviews.Contracts.Dtos;
using Memoria.Users.Contracts.Dtos;
using Microsoft.Extensions.Options;

namespace Memoria.Reminders.Services;

/// <summary>
/// Builds reminders for the adaptive (Leitner/SM-2-style) schedule. A card has
/// at most one pending reminder at a time: a fresh card gets a stage-1 reminder
/// (<see cref="CreateFirstReminder"/>); after each rating the next reminder is
/// computed from the current stage and the <see cref="Rating"/>
/// (<see cref="ComputeNext"/> / <see cref="CreateNextReminder"/>); a Skip
/// re-arms the same stage after <see cref="RemindersOptions.HardRetryInterval"/>
/// (<see cref="CreateRetryReminder"/>). All scheduling honors the user's
/// timezone and quiet-hours window.
/// </summary>
internal sealed class ReminderScheduler
{
    private readonly RemindersOptions _options;

    public ReminderScheduler(IOptions<RemindersOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    private int MaxStage => _options.Intervals.Count;

    /// <summary>
    /// First reminder for a freshly created (or restored) card: stage 1,
    /// delay = <c>Intervals[0]</c>.
    /// </summary>
    public Reminder CreateFirstReminder(
        Guid cardId,
        Guid userId,
        UserPreferencesDto preferences,
        DateTime anchorUtc)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        EnsureIntervals();

        return BuildReminder(cardId, userId, stage: 1, delay: _options.Intervals[0], preferences, anchorUtc);
    }

    /// <summary>
    /// Adaptive transition: given the stage of the just-reviewed reminder and
    /// the user's rating, produces the next reminder.
    /// </summary>
    public Reminder CreateNextReminder(
        Guid cardId,
        Guid userId,
        int currentStage,
        Rating rating,
        UserPreferencesDto preferences,
        DateTime anchorUtc)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        EnsureIntervals();

        var (nextStage, delay) = ComputeNext(currentStage, rating);
        return BuildReminder(cardId, userId, nextStage, delay, preferences, anchorUtc);
    }

    /// <summary>
    /// Resume scheduling at <paramref name="stage"/> with delay
    /// <c>Intervals[stage - 1]</c>. Used by the Unpause flow to thaw a card
    /// back at the same stage where it was frozen.
    /// </summary>
    public Reminder CreateReminderForStage(
        Guid cardId,
        Guid userId,
        int stage,
        UserPreferencesDto preferences,
        DateTime anchorUtc)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        EnsureIntervals();

        var clamped = Math.Clamp(stage, 1, MaxStage);
        return BuildReminder(cardId, userId, clamped, _options.Intervals[clamped - 1], preferences, anchorUtc);
    }

    /// <summary>
    /// Retry at the SAME stage after a Skip — uses
    /// <see cref="RemindersOptions.HardRetryInterval"/>, no Review recorded.
    /// </summary>
    public Reminder CreateRetryReminder(
        Guid cardId,
        Guid userId,
        int currentStage,
        UserPreferencesDto preferences,
        DateTime anchorUtc)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        EnsureIntervals();

        return BuildReminder(cardId, userId, currentStage, _options.HardRetryInterval, preferences, anchorUtc);
    }

    /// <summary>
    /// Maps <c>(currentStage, rating)</c> → <c>(nextStage, delay)</c>:
    /// Forgot resets to stage 1; Hard repeats the stage after HardRetryInterval;
    /// Good advances one stage; Easy advances two — both capped at the last
    /// stage and using that stage's interval as the delay.
    /// </summary>
    public (int NextStage, TimeSpan Delay) ComputeNext(int currentStage, Rating rating)
    {
        EnsureIntervals();

        return rating switch
        {
            Rating.Forgot => (1, _options.Intervals[0]),
            Rating.Hard => (currentStage, _options.HardRetryInterval),
            Rating.Good => StageStep(Math.Min(currentStage + 1, MaxStage)),
            Rating.Easy => StageStep(Math.Min(currentStage + 2, MaxStage)),
            _ => (currentStage, _options.HardRetryInterval),
        };

        (int, TimeSpan) StageStep(int stage) => (stage, _options.Intervals[stage - 1]);
    }

    private void EnsureIntervals()
    {
        if (_options.Intervals.Count < 1)
        {
            throw new InvalidOperationException(
                "RemindersOptions.Intervals must contain at least one entry.");
        }
    }

    private Reminder BuildReminder(
        Guid cardId,
        Guid userId,
        int stage,
        TimeSpan delay,
        UserPreferencesDto preferences,
        DateTime anchorUtc)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(preferences.TimeZoneId);
        var naiveUtc = anchorUtc + delay;
        var local = TimeZoneInfo.ConvertTimeFromUtc(naiveUtc, timeZone);
        var shiftedLocal = ShiftIfInQuietHours(local, preferences.QuietHoursStart, preferences.QuietHoursEnd);
        var scheduledAtUtc = TimeZoneInfo.ConvertTimeToUtc(shiftedLocal, timeZone);

        return new Reminder(cardId, userId, stage, scheduledAtUtc);
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
