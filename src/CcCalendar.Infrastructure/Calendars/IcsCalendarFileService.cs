using System.Text;
using CcCalendar.Core.Recurrence;
using CcCalendar.Core.Schedules;
using CcCalendar.Infrastructure.Persistence;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Serialization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using DomainCalendarEvent = CcCalendar.Core.Schedules.CalendarEvent;
using DomainRecurrenceRule = CcCalendar.Core.Recurrence.RecurrenceRule;
using IcalCalendarEvent = Ical.Net.CalendarComponents.CalendarEvent;
using IcalRecurrenceRule = Ical.Net.DataTypes.RecurrenceRule;

namespace CcCalendar.Infrastructure.Calendars;

public sealed class IcsCalendarFileService
{
    private const string TimeZonePropertyName = "X-CCCALENDAR-TIME-ZONE-ID";
    private const string SkipHolidaysPropertyName = "X-CCCALENDAR-SKIP-HOLIDAYS";
    private readonly string connectionString;

    public IcsCalendarFileService(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(databasePath),
            Pooling = false,
        };
        connectionString = builder.ToString();
    }

    public async Task ExportAsync(string filePath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        await using CalendarDbContext context = CreateContext();
        DomainCalendarEvent[] calendarEvents = await context.CalendarEvents
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);
        RecurrenceSeries[] recurrenceSeries = await context.RecurrenceSeries
            .AsNoTracking()
            .Include(series => series.Exceptions)
            .ToArrayAsync(cancellationToken);
        Dictionary<Guid, RecurrenceSeries> recurrenceByEventId = recurrenceSeries
            .ToDictionary(series => series.CalendarEventId);
        var calendar = new Calendar
        {
            ProductId = "-//cccalendar//Calendar Export//EN",
            Version = "2.0",
        };

        foreach (DomainCalendarEvent calendarEvent in calendarEvents)
        {
            recurrenceByEventId.TryGetValue(calendarEvent.Id, out RecurrenceSeries? recurrence);
            calendar.Events.Add(CreateIcalEvent(calendarEvent, recurrence));
        }

        string contents = new CalendarSerializer().SerializeToString(calendar)
            ?? throw new InvalidDataException("The calendar could not be serialized.");
        await File.WriteAllTextAsync(filePath, contents, new UTF8Encoding(false), cancellationToken);
    }

    public async Task<IcsImportResult> ImportAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        string contents = await File.ReadAllTextAsync(filePath, cancellationToken);
        Calendar calendar = Calendar.Load(contents)
            ?? throw new InvalidDataException("The file does not contain a calendar.");
        var importedEvents = new List<DomainCalendarEvent>();
        var importedSeries = new List<RecurrenceSeries>();

        foreach (IcalCalendarEvent icalEvent in calendar.Events)
        {
            DomainCalendarEvent calendarEvent = CreateDomainEvent(icalEvent);
            importedEvents.Add(calendarEvent);

            IcalRecurrenceRule? recurrenceRule = icalEvent.RecurrenceRule;
            if (recurrenceRule is not null)
            {
                RecurrenceSeries series = RecurrenceSeries.Create(
                    calendarEvent.Id,
                    CreateDomainRule(icalEvent, recurrenceRule));
                foreach (DateOnly excludedDate in icalEvent.ExceptionDates
                    .GetAllDates()
                    .Select(date => date.Date)
                    .Distinct())
                {
                    series.Exclude(excludedDate);
                }

                importedSeries.Add(series);
            }
        }

        await using CalendarDbContext context = CreateContext();
        context.AddRange(importedEvents);
        context.AddRange(importedSeries);
        await context.SaveChangesAsync(cancellationToken);
        return new IcsImportResult(importedEvents.Count);
    }

    private static IcalCalendarEvent CreateIcalEvent(
        DomainCalendarEvent calendarEvent,
        RecurrenceSeries? recurrence)
    {
        var icalEvent = new IcalCalendarEvent
        {
            Uid = calendarEvent.Id.ToString("D"),
            Summary = calendarEvent.Title,
            DtStamp = CalDateTime.UtcNow,
        };

        if (calendarEvent.IsAllDay)
        {
            icalEvent.DtStart = new CalDateTime(calendarEvent.AllDayStart!.Value);
            icalEvent.DtEnd = new CalDateTime(calendarEvent.AllDayEndExclusive!.Value);
        }
        else
        {
            icalEvent.DtStart = CreateUtcDateTime(calendarEvent.StartAtUtc!.Value);
            icalEvent.DtEnd = CreateUtcDateTime(calendarEvent.EndAtUtc!.Value);
            icalEvent.Properties.Add(new CalendarProperty(
                TimeZonePropertyName,
                calendarEvent.TimeZoneId!));
        }

        if (recurrence is not null)
        {
            icalEvent.RecurrenceRule = CreateIcalRule(recurrence.Rule);
            icalEvent.ExceptionDates.AddRange(
                recurrence.ExcludedDates.Select(date => new CalDateTime(date)));
            if (recurrence.SkipHolidays)
            {
                icalEvent.Properties.Add(new CalendarProperty(SkipHolidaysPropertyName, "TRUE"));
            }
        }

        return icalEvent;
    }

    private static DomainCalendarEvent CreateDomainEvent(IcalCalendarEvent icalEvent)
    {
        CalDateTime start = icalEvent.DtStart
            ?? throw new InvalidDataException("A calendar event has no start time.");
        string title = string.IsNullOrWhiteSpace(icalEvent.Summary)
            ? "未命名日程"
            : icalEvent.Summary;
        if (!start.HasTime)
        {
            DateOnly startDate = start.Date;
            DateOnly endDateExclusive = icalEvent.DtEnd is null
                ? startDate.AddDays(1)
                : icalEvent.DtEnd.Date;
            return DomainCalendarEvent.CreateAllDay(title, null, startDate, endDateExclusive);
        }

        if (icalEvent.DtEnd is null)
        {
            throw new InvalidDataException($"Timed event '{title}' has no end time.");
        }

        string timeZoneId = ReadCustomProperty(icalEvent, TimeZonePropertyName)
            ?? start.TzId
            ?? "UTC";
        return DomainCalendarEvent.CreateTimed(
            title,
            null,
            CreateUtcOffset(start),
            CreateUtcOffset(icalEvent.DtEnd),
            timeZoneId);
    }

    private static DomainRecurrenceRule CreateDomainRule(
        IcalCalendarEvent icalEvent,
        IcalRecurrenceRule recurrencePattern)
    {
        RecurrenceFrequency frequency = recurrencePattern.Frequency switch
        {
            FrequencyType.Daily => RecurrenceFrequency.Daily,
            FrequencyType.Weekly => RecurrenceFrequency.Weekly,
            FrequencyType.Monthly => RecurrenceFrequency.Monthly,
            FrequencyType.Yearly => RecurrenceFrequency.Yearly,
            _ => throw new InvalidDataException(
                $"Unsupported recurrence frequency: {recurrencePattern.Frequency}."),
        };
        DateOnly? untilInclusive = recurrencePattern.Until is null
            ? null
            : recurrencePattern.Until.Date;
        bool skipHolidays = string.Equals(
            ReadCustomProperty(icalEvent, SkipHolidaysPropertyName),
            "TRUE",
            StringComparison.OrdinalIgnoreCase);

        return DomainRecurrenceRule.Create(
            frequency,
            recurrencePattern.Interval,
            recurrencePattern.ByDay.Select(day => day.DayOfWeek),
            untilInclusive,
            skipHolidays);
    }

    private static IcalRecurrenceRule CreateIcalRule(DomainRecurrenceRule rule)
    {
        var pattern = new IcalRecurrenceRule
        {
            Frequency = rule.Frequency switch
            {
                RecurrenceFrequency.Daily => FrequencyType.Daily,
                RecurrenceFrequency.Weekly => FrequencyType.Weekly,
                RecurrenceFrequency.Monthly => FrequencyType.Monthly,
                RecurrenceFrequency.Yearly => FrequencyType.Yearly,
                _ => throw new ArgumentOutOfRangeException(nameof(rule)),
            },
            Interval = rule.Interval,
        };
        pattern.ByDay.AddRange(rule.DaysOfWeek.Select(day => new WeekDay(day)));
        if (rule.UntilInclusive.HasValue)
        {
            pattern.Until = new CalDateTime(rule.UntilInclusive.Value);
        }

        return pattern;
    }

    private static CalDateTime CreateUtcDateTime(DateTimeOffset instant)
    {
        return new CalDateTime(instant.UtcDateTime, "UTC", true);
    }

    private static DateTimeOffset CreateUtcOffset(CalDateTime calendarDateTime)
    {
        return new DateTimeOffset(
            DateTime.SpecifyKind(calendarDateTime.AsUtc, DateTimeKind.Utc));
    }

    private static string? ReadCustomProperty(
        CalendarComponent calendarComponent,
        string propertyName)
    {
        return calendarComponent.Properties[propertyName]?.Value?.ToString();
    }

    private CalendarDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CalendarDbContext>()
            .UseSqlite(connectionString)
            .Options;
        return new CalendarDbContext(options);
    }
}
