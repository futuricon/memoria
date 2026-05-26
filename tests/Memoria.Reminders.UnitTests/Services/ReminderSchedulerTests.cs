using FluentAssertions;

using Memoria.Reminders.Options;
using Memoria.Reminders.Services;
using Memoria.Users.Contracts.Dtos;

namespace Memoria.Reminders.UnitTests.Services;

public sealed class ReminderSchedulerTests
{
    private static readonly DateTime AnchorUtc = new(2026, 5, 23, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid CardId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static ReminderScheduler CreateSut(RemindersOptions? options = null) =>
        new(Microsoft.Extensions.Options.Options.Create(options ?? new RemindersOptions()));

    private static UserPreferencesDto PrefsWith(
        string tzId = "UTC",
        TimeOnly? quietStart = null,
        TimeOnly? quietEnd = null) =>
        new(UserId, tzId, quietStart, quietEnd);

    [Fact]
    public void CreateScheduleForReturnsFiveReminders()
    {
        var sut = CreateSut();

        var reminders = sut.CreateScheduleFor(CardId, UserId, PrefsWith(), AnchorUtc);

        reminders.Should().HaveCount(5);
    }

    [Fact]
    public void CreateScheduleForReturnsCorrectStageNumbers()
    {
        var sut = CreateSut();

        var reminders = sut.CreateScheduleFor(CardId, UserId, PrefsWith(), AnchorUtc);

        reminders.Select(r => r.StageNumber).Should().Equal(1, 2, 3, 4, 5);
    }

    [Fact]
    public void CreateScheduleForReturnsCorrectScheduledAtForEachStage()
    {
        var sut = CreateSut();

        var reminders = sut.CreateScheduleFor(CardId, UserId, PrefsWith(), AnchorUtc);

        reminders.Select(r => r.ScheduledAt).Should().Equal(
            new DateTime(2026, 5, 23, 12, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 5, 23, 12, 25, 0, DateTimeKind.Utc),
            new DateTime(2026, 5, 24, 12, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 6, 12, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void CreateScheduleForRespectsCustomIntervals()
    {
        var options = new RemindersOptions
        {
            Intervals = new[]
            {
                TimeSpan.FromMinutes(5),
                TimeSpan.FromMinutes(10),
                TimeSpan.FromMinutes(15),
                TimeSpan.FromMinutes(20),
                TimeSpan.FromMinutes(25),
            },
        };
        var sut = CreateSut(options);

        var reminders = sut.CreateScheduleFor(CardId, UserId, PrefsWith(), AnchorUtc);

        reminders.Select(r => r.ScheduledAt).Should().Equal(
            AnchorUtc.AddMinutes(5),
            AnchorUtc.AddMinutes(10),
            AnchorUtc.AddMinutes(15),
            AnchorUtc.AddMinutes(20),
            AnchorUtc.AddMinutes(25));
    }

    [Fact]
    public void CreateScheduleForConvertsToUserTimezone()
    {
        var sut = CreateSut();

        var utcResult = sut.CreateScheduleFor(CardId, UserId, PrefsWith("UTC"), AnchorUtc);
        var mskResult = sut.CreateScheduleFor(CardId, UserId, PrefsWith("Europe/Moscow"), AnchorUtc);

        utcResult.Select(r => r.ScheduledAt)
            .Should().Equal(mskResult.Select(r => r.ScheduledAt));
    }

    [Fact]
    public void CreateScheduleForThrowsOnInvalidTimezone()
    {
        var sut = CreateSut();
        var prefs = PrefsWith("Not/A/Zone");

        Action act = () => sut.CreateScheduleFor(CardId, UserId, prefs, AnchorUtc);

        act.Should().Throw<TimeZoneNotFoundException>();
    }

    [Fact]
    public void QuietHoursWindowNonWrappingShiftsToEnd()
    {
        var anchor = new DateTime(2026, 5, 23, 13, 30, 0, DateTimeKind.Utc);
        var prefs = PrefsWith("UTC", new TimeOnly(13, 0), new TimeOnly(14, 0));
        var sut = CreateSut();

        var stage1 = sut.CreateScheduleFor(CardId, UserId, prefs, anchor)[0];

        stage1.ScheduledAt.Should().Be(new DateTime(2026, 5, 23, 14, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void QuietHoursWrappingWindowShiftsToNextMorningEnd()
    {
        var anchor = new DateTime(2026, 5, 23, 23, 30, 0, DateTimeKind.Utc);
        var prefs = PrefsWith("UTC", new TimeOnly(22, 0), new TimeOnly(7, 0));
        var sut = CreateSut();

        var stage1 = sut.CreateScheduleFor(CardId, UserId, prefs, anchor)[0];

        stage1.ScheduledAt.Should().Be(new DateTime(2026, 5, 24, 7, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void QuietHoursWrappingWindowEarlyMorningShiftsToSameDayEnd()
    {
        var anchor = new DateTime(2026, 5, 23, 3, 0, 0, DateTimeKind.Utc);
        var prefs = PrefsWith("UTC", new TimeOnly(22, 0), new TimeOnly(7, 0));
        var sut = CreateSut();

        var stage1 = sut.CreateScheduleFor(CardId, UserId, prefs, anchor)[0];

        stage1.ScheduledAt.Should().Be(new DateTime(2026, 5, 23, 7, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void QuietHoursBothNullDoesNotShift()
    {
        var anchor = new DateTime(2026, 5, 23, 23, 30, 0, DateTimeKind.Utc);
        var prefs = PrefsWith("UTC", quietStart: null, quietEnd: null);
        var sut = CreateSut();

        var stage1 = sut.CreateScheduleFor(CardId, UserId, prefs, anchor)[0];

        stage1.ScheduledAt.Should().Be(anchor);
    }

    [Fact]
    public void QuietHoursOneNullDoesNotShift()
    {
        var anchor = new DateTime(2026, 5, 23, 23, 30, 0, DateTimeKind.Utc);
        var prefsStartOnly = PrefsWith("UTC", quietStart: new TimeOnly(22, 0), quietEnd: null);
        var prefsEndOnly = PrefsWith("UTC", quietStart: null, quietEnd: new TimeOnly(7, 0));
        var sut = CreateSut();

        sut.CreateScheduleFor(CardId, UserId, prefsStartOnly, anchor)[0]
            .ScheduledAt.Should().Be(anchor);

        sut.CreateScheduleFor(CardId, UserId, prefsEndOnly, anchor)[0]
            .ScheduledAt.Should().Be(anchor);
    }

    [Fact]
    public void IntervalsWithWrongCountThrows()
    {
        var options = new RemindersOptions
        {
            Intervals = new[] { TimeSpan.Zero, TimeSpan.FromMinutes(25), TimeSpan.FromDays(1) },
        };
        var sut = CreateSut(options);

        Action act = () => sut.CreateScheduleFor(CardId, UserId, PrefsWith(), AnchorUtc);

        act.Should().Throw<InvalidOperationException>().WithMessage("*5*");
    }
}
