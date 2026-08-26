using CcCalendar.Core.Recurrence;
using CcCalendar.Core.Schedules;
using CcCalendar.Infrastructure.Calendars;
using CcCalendar.Infrastructure.Persistence;
using CcCalendar.Infrastructure.Tests.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CcCalendar.Infrastructure.Tests.Calendars;

public sealed class IcsCalendarFileServiceTests
{
    [Fact]
    public async Task ExportThenImportPreservesSupportedCalendarSemantics()
    {
        await using var source = new TemporaryCalendarDatabase();
        await using var target = new TemporaryCalendarDatabase();
        await source.InitializeAsync(CancellationToken.None);
        await target.InitializeAsync(CancellationToken.None);
        string icsPath = Path.Combine(Path.GetTempPath(), $"cccalendar-{Guid.NewGuid():N}.ics");

        try
        {
            await SeedSourceCalendarAsync(source);
            var sourceService = new IcsCalendarFileService(source.DatabasePath);
            var targetService = new IcsCalendarFileService(target.DatabasePath);

            await sourceService.ExportAsync(icsPath, CancellationToken.None);
            IcsImportResult result = await targetService.ImportAsync(icsPath, CancellationToken.None);

            string ics = await File.ReadAllTextAsync(icsPath, CancellationToken.None);
            Assert.Contains("BEGIN:VCALENDAR", ics, StringComparison.Ordinal);
            Assert.Contains("RRULE", ics, StringComparison.Ordinal);
            Assert.Contains("EXDATE", ics, StringComparison.Ordinal);
            Assert.Equal(3, result.ImportedEventCount);

            await using CalendarDbContext context = target.CreateContext();
            CalendarEvent timed = await context.CalendarEvents.SingleAsync(
                item => item.Title == "Architecture review");
            CalendarEvent allDay = await context.CalendarEvents.SingleAsync(
                item => item.Title == "Release window");
            RecurrenceSeries recurring = await context.RecurrenceSeries
                .Include(series => series.Exceptions)
                .SingleAsync();

            Assert.Equal(new DateTimeOffset(2026, 8, 17, 2, 0, 0, TimeSpan.Zero), timed.StartAtUtc);
            Assert.Equal(new DateTimeOffset(2026, 8, 17, 3, 30, 0, TimeSpan.Zero), timed.EndAtUtc);
            Assert.Equal("China Standard Time", timed.TimeZoneId);
            Assert.Equal(new DateOnly(2026, 8, 20), allDay.AllDayStart);
            Assert.Equal(new DateOnly(2026, 8, 22), allDay.AllDayEndExclusive);
            Assert.Equal(RecurrenceFrequency.Weekly, recurring.Rule.Frequency);
            Assert.Equal(2, recurring.Rule.Interval);
            Assert.Equal([DayOfWeek.Monday, DayOfWeek.Wednesday], recurring.Rule.DaysOfWeek);
            Assert.Equal(new DateOnly(2026, 10, 28), recurring.Rule.UntilInclusive);
            Assert.True(recurring.Rule.SkipHolidays);
            Assert.Contains(new DateOnly(2026, 9, 2), recurring.ExcludedDates);
        }
        finally
        {
            File.Delete(icsPath);
        }
    }

    [Fact]
    public async Task ImportAcceptsStandardTimedAndAllDayEvents()
    {
        await using var database = new TemporaryCalendarDatabase();
        await database.InitializeAsync(CancellationToken.None);
        string icsPath = Path.Combine(Path.GetTempPath(), $"cccalendar-{Guid.NewGuid():N}.ics");
        const string ics = """
            BEGIN:VCALENDAR
            VERSION:2.0
            PRODID:-//Example//Calendar//EN
            BEGIN:VEVENT
            UID:external-timed@example.test
            DTSTAMP:20260816T000000Z
            DTSTART:20260818T010000Z
            DTEND:20260818T020000Z
            SUMMARY:External meeting
            END:VEVENT
            BEGIN:VEVENT
            UID:external-all-day@example.test
            DTSTAMP:20260816T000000Z
            DTSTART;VALUE=DATE:20260819
            DTEND;VALUE=DATE:20260821
            SUMMARY:External holiday
            END:VEVENT
            END:VCALENDAR
            """;

        try
        {
            await File.WriteAllTextAsync(icsPath, ics, CancellationToken.None);
            var service = new IcsCalendarFileService(database.DatabasePath);

            IcsImportResult result = await service.ImportAsync(icsPath, CancellationToken.None);

            Assert.Equal(2, result.ImportedEventCount);
            await using CalendarDbContext context = database.CreateContext();
            CalendarEvent timed = await context.CalendarEvents.SingleAsync(
                item => item.Title == "External meeting");
            CalendarEvent allDay = await context.CalendarEvents.SingleAsync(
                item => item.Title == "External holiday");
            Assert.Equal(new DateTimeOffset(2026, 8, 18, 1, 0, 0, TimeSpan.Zero), timed.StartAtUtc);
            Assert.Equal("UTC", timed.TimeZoneId);
            Assert.Equal(new DateOnly(2026, 8, 19), allDay.AllDayStart);
            Assert.Equal(new DateOnly(2026, 8, 21), allDay.AllDayEndExclusive);
        }
        finally
        {
            File.Delete(icsPath);
        }
    }

    private static async Task SeedSourceCalendarAsync(TemporaryCalendarDatabase database)
    {
        await using CalendarDbContext context = database.CreateContext();
        CalendarEvent timed = CalendarEvent.CreateTimed(
            "Architecture review",
            null,
            new DateTimeOffset(2026, 8, 17, 10, 0, 0, TimeSpan.FromHours(8)),
            new DateTimeOffset(2026, 8, 17, 11, 30, 0, TimeSpan.FromHours(8)),
            "China Standard Time");
        CalendarEvent allDay = CalendarEvent.CreateAllDay(
            "Release window",
            null,
            new DateOnly(2026, 8, 20),
            new DateOnly(2026, 8, 22));
        CalendarEvent recurringEvent = CalendarEvent.CreateAllDay(
            "Planning cycle",
            null,
            new DateOnly(2026, 8, 17),
            new DateOnly(2026, 8, 18));
        RecurrenceSeries series = RecurrenceSeries.Create(
            recurringEvent.Id,
            RecurrenceRule.Create(
                RecurrenceFrequency.Weekly,
                2,
                [DayOfWeek.Monday, DayOfWeek.Wednesday],
                new DateOnly(2026, 10, 28),
                skipHolidays: true));
        series.Exclude(new DateOnly(2026, 9, 2));

        context.AddRange(timed, allDay, recurringEvent, series);
        await context.SaveChangesAsync(CancellationToken.None);
    }
}
