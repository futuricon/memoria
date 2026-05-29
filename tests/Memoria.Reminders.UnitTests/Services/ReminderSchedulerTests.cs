using FluentAssertions;

using Memoria.Reminders.Options;
using Memoria.Reminders.Services;
using Memoria.Reviews.Contracts.Dtos;
using Memoria.Users.Contracts.Dtos;

namespace Memoria.Reminders.UnitTests.Services;

public sealed class ReminderSchedulerTests
{
    private static readonly DateTime AnchorUtc = new(2026, 5, 23, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid CardId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    // 8-stage ladder mirroring appsettings.json:Reminders:Intervals.
    // RemindersOptions carries no default (see its doc-comment — .NET
    // ConfigurationBinder appends rather than replaces on collection
    // properties), so each test supplies its own.
    private static RemindersOptions DefaultOptions() => new()
    {
        Intervals = new[]
        {
            TimeSpan.FromMinutes(10),
            TimeSpan.FromHours(1),
            TimeSpan.FromDays(1),
            TimeSpan.FromDays(3),
            TimeSpan.FromDays(7),
            TimeSpan.FromDays(14),
            TimeSpan.FromDays(30),
            TimeSpan.FromDays(90),
        },
        HardRetryInterval = TimeSpan.FromHours(1),
    };

    private static ReminderScheduler CreateSut(RemindersOptions? options = null) =>
        new(Microsoft.Extensions.Options.Options.Create(options ?? DefaultOptions()));

    private static UserPreferencesDto PrefsWith(
        string tzId = "UTC",
        TimeOnly? quietStart = null,
        TimeOnly? quietEnd = null) =>
        new(UserId, tzId, quietStart, quietEnd);

    [Fact]
    public void CreateFirstReminderReturnsStageOne()
    {
        var sut = CreateSut();

        var reminder = sut.CreateFirstReminder(CardId, UserId, PrefsWith(), AnchorUtc);

        reminder.StageNumber.Should().Be(1);
    }

    [Fact]
    public void CreateFirstReminderUsesFirstInterval()
    {
        var sut = CreateSut();

        var reminder = sut.CreateFirstReminder(CardId, UserId, PrefsWith(), AnchorUtc);

        reminder.ScheduledAt.Should().Be(AnchorUtc.AddMinutes(10));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(8)]
    public void ComputeNextForgotResetsToStageOne(int currentStage)
    {
        var sut = CreateSut();

        var (nextStage, delay) = sut.ComputeNext(currentStage, Rating.Forgot);

        nextStage.Should().Be(1);
        delay.Should().Be(TimeSpan.FromMinutes(10));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(8)]
    public void ComputeNextHardKeepsStageWithRetryInterval(int currentStage)
    {
        var sut = CreateSut();

        var (nextStage, delay) = sut.ComputeNext(currentStage, Rating.Hard);

        nextStage.Should().Be(currentStage);
        delay.Should().Be(TimeSpan.FromHours(1));
    }

    [Fact]
    public void ComputeNextGoodAdvancesOneStage()
    {
        var sut = CreateSut();

        var (nextStage, delay) = sut.ComputeNext(3, Rating.Good);

        nextStage.Should().Be(4);
        delay.Should().Be(TimeSpan.FromDays(3)); // Intervals[3]
    }

    [Fact]
    public void ComputeNextEasyAdvancesTwoStages()
    {
        var sut = CreateSut();

        var (nextStage, delay) = sut.ComputeNext(3, Rating.Easy);

        nextStage.Should().Be(5);
        delay.Should().Be(TimeSpan.FromDays(7)); // Intervals[4]
    }

    [Fact]
    public void ComputeNextGoodCapsAtMaxStage()
    {
        var sut = CreateSut();

        var (nextStage, delay) = sut.ComputeNext(8, Rating.Good);

        nextStage.Should().Be(8);
        delay.Should().Be(TimeSpan.FromDays(90)); // Intervals[7]
    }

    [Theory]
    [InlineData(7)]
    [InlineData(8)]
    public void ComputeNextEasyCapsAtMaxStage(int currentStage)
    {
        var sut = CreateSut();

        var (nextStage, delay) = sut.ComputeNext(currentStage, Rating.Easy);

        nextStage.Should().Be(8);
        delay.Should().Be(TimeSpan.FromDays(90));
    }

    [Fact]
    public void CreateNextReminderAppliesComputedStageAndDelay()
    {
        var sut = CreateSut();

        var reminder = sut.CreateNextReminder(CardId, UserId, currentStage: 3, Rating.Good, PrefsWith(), AnchorUtc);

        reminder.StageNumber.Should().Be(4);
        reminder.ScheduledAt.Should().Be(AnchorUtc.AddDays(3));
    }

    [Fact]
    public void CreateNextReminderForgotResetsToStageOne()
    {
        var sut = CreateSut();

        var reminder = sut.CreateNextReminder(CardId, UserId, currentStage: 6, Rating.Forgot, PrefsWith(), AnchorUtc);

        reminder.StageNumber.Should().Be(1);
        reminder.ScheduledAt.Should().Be(AnchorUtc.AddMinutes(10));
    }

    [Fact]
    public void CreateRetryReminderKeepsStageWithRetryInterval()
    {
        var sut = CreateSut();

        var reminder = sut.CreateRetryReminder(CardId, UserId, currentStage: 4, PrefsWith(), AnchorUtc);

        reminder.StageNumber.Should().Be(4);
        reminder.ScheduledAt.Should().Be(AnchorUtc.AddHours(1));
    }

    [Fact]
    public void CreateFirstReminderConvertsToUserTimezone()
    {
        var sut = CreateSut();

        var utcResult = sut.CreateFirstReminder(CardId, UserId, PrefsWith("UTC"), AnchorUtc);
        var mskResult = sut.CreateFirstReminder(CardId, UserId, PrefsWith("Europe/Moscow"), AnchorUtc);

        utcResult.ScheduledAt.Should().Be(mskResult.ScheduledAt);
    }

    [Fact]
    public void CreateFirstReminderThrowsOnInvalidTimezone()
    {
        var sut = CreateSut();
        var prefs = PrefsWith("Not/A/Zone");

        Action act = () => sut.CreateFirstReminder(CardId, UserId, prefs, AnchorUtc);

        act.Should().Throw<TimeZoneNotFoundException>();
    }

    [Fact]
    public void QuietHoursWindowNonWrappingShiftsToEnd()
    {
        // anchor + 10min (Intervals[0]) = 13:35, inside [13:00, 14:00) → 14:00.
        var anchor = new DateTime(2026, 5, 23, 13, 25, 0, DateTimeKind.Utc);
        var prefs = PrefsWith("UTC", new TimeOnly(13, 0), new TimeOnly(14, 0));
        var sut = CreateSut();

        var reminder = sut.CreateFirstReminder(CardId, UserId, prefs, anchor);

        reminder.ScheduledAt.Should().Be(new DateTime(2026, 5, 23, 14, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void QuietHoursWrappingWindowShiftsToNextMorningEnd()
    {
        // anchor + 10min = 23:30, inside wrapping [22:00, 07:00) after-start half → next day 07:00.
        var anchor = new DateTime(2026, 5, 23, 23, 20, 0, DateTimeKind.Utc);
        var prefs = PrefsWith("UTC", new TimeOnly(22, 0), new TimeOnly(7, 0));
        var sut = CreateSut();

        var reminder = sut.CreateFirstReminder(CardId, UserId, prefs, anchor);

        reminder.ScheduledAt.Should().Be(new DateTime(2026, 5, 24, 7, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void QuietHoursWrappingWindowEarlyMorningShiftsToSameDayEnd()
    {
        // anchor + 10min = 03:00, inside wrapping window before-end half → same day 07:00.
        var anchor = new DateTime(2026, 5, 23, 2, 50, 0, DateTimeKind.Utc);
        var prefs = PrefsWith("UTC", new TimeOnly(22, 0), new TimeOnly(7, 0));
        var sut = CreateSut();

        var reminder = sut.CreateFirstReminder(CardId, UserId, prefs, anchor);

        reminder.ScheduledAt.Should().Be(new DateTime(2026, 5, 23, 7, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void QuietHoursBothNullDoesNotShift()
    {
        var anchor = new DateTime(2026, 5, 23, 23, 30, 0, DateTimeKind.Utc);
        var prefs = PrefsWith("UTC", quietStart: null, quietEnd: null);
        var sut = CreateSut();

        var reminder = sut.CreateFirstReminder(CardId, UserId, prefs, anchor);

        reminder.ScheduledAt.Should().Be(anchor.AddMinutes(10));
    }

    [Fact]
    public void QuietHoursOneNullDoesNotShift()
    {
        var anchor = new DateTime(2026, 5, 23, 23, 30, 0, DateTimeKind.Utc);
        var prefsStartOnly = PrefsWith("UTC", quietStart: new TimeOnly(22, 0), quietEnd: null);
        var prefsEndOnly = PrefsWith("UTC", quietStart: null, quietEnd: new TimeOnly(7, 0));
        var sut = CreateSut();

        sut.CreateFirstReminder(CardId, UserId, prefsStartOnly, anchor)
            .ScheduledAt.Should().Be(anchor.AddMinutes(10));

        sut.CreateFirstReminder(CardId, UserId, prefsEndOnly, anchor)
            .ScheduledAt.Should().Be(anchor.AddMinutes(10));
    }

    [Fact]
    public void EmptyIntervalsThrows()
    {
        var options = new RemindersOptions { Intervals = Array.Empty<TimeSpan>() };
        var sut = CreateSut(options);

        Action act = () => sut.CreateFirstReminder(CardId, UserId, PrefsWith(), AnchorUtc);

        act.Should().Throw<InvalidOperationException>().WithMessage("*at least one*");
    }
}
