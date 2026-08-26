using CcCalendar.Core.Recurrence;
using CcCalendar.Core.Schedules;
using Microsoft.EntityFrameworkCore;

namespace CcCalendar.Infrastructure.Tests.Persistence;

public sealed class RecurrencePersistenceTests
{
    [Fact]
    public async Task RecurrenceSeriesRoundTripsWithExcludedDates()
    {
        await using var database = new TemporaryCalendarDatabase();
        await database.InitializeAsync(CancellationToken.None);
        Guid seriesId;

        await using (var writeContext = database.CreateContext())
        {
            CalendarEvent calendarEvent = CalendarEvent.CreateAllDay(
                "Workday planning",
                null,
                new DateOnly(2026, 8, 17),
                new DateOnly(2026, 8, 18));
            RecurrenceSeries series = RecurrenceSeries.Create(calendarEvent.Id, RecurrenceRule.Workdays(null));
            series.Exclude(new DateOnly(2026, 8, 19));
            seriesId = series.Id;

            writeContext.AddRange(calendarEvent, series);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        await using var readContext = database.CreateContext();
        RecurrenceSeries actual = await readContext.RecurrenceSeries
            .Include(series => series.Exceptions)
            .SingleAsync(series => series.Id == seriesId);

        Assert.True(actual.Rule.SkipHolidays);
        Assert.Contains(new DateOnly(2026, 8, 19), actual.ExcludedDates);
    }
}
