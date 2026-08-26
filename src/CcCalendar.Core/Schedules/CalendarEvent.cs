using CcCalendar.Core.Recycling;

namespace CcCalendar.Core.Schedules;

public sealed class CalendarEvent : IRecyclableEntity
{
    private CalendarEvent()
    {
        Title = null!;
    }

    public Guid Id { get; private set; }

    public string Title { get; private set; }

    public Guid? ProjectId { get; private set; }

    public string? Location { get; private set; }

    public string? MeetingInvitationText { get; private set; }

    public DateOnly? MeetingInvitationExpiresOn { get; private set; }

    public int? ReminderLeadMinutes { get; private set; }

    public bool IsAllDay { get; private set; }

    public DateTimeOffset? StartAtUtc { get; private set; }

    public DateTimeOffset? EndAtUtc { get; private set; }

    public string? TimeZoneId { get; private set; }

    public DateOnly? AllDayStart { get; private set; }

    public DateOnly? AllDayEndExclusive { get; private set; }

    public DateTimeOffset? DeletedAtUtc { get; private set; }

    public bool IsDeleted => DeletedAtUtc.HasValue;

    public TimeSpan? Duration => StartAtUtc.HasValue && EndAtUtc.HasValue
        ? EndAtUtc - StartAtUtc
        : null;

    public int? AllDayLength => AllDayStart.HasValue && AllDayEndExclusive.HasValue
        ? AllDayEndExclusive.Value.DayNumber - AllDayStart.Value.DayNumber
        : null;

    public static CalendarEvent CreateTimed(
        string title,
        Guid? projectId,
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        string timeZoneId,
        string? location = null,
        string? meetingInvitationText = null,
        int? reminderLeadMinutes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);

        DateTimeOffset startAtUtc = startAt.ToUniversalTime();
        DateTimeOffset endAtUtc = endAt.ToUniversalTime();

        if (endAtUtc <= startAtUtc)
        {
            throw new ArgumentException("The end time must be after the start time.", nameof(endAt));
        }

        return new CalendarEvent
        {
            Id = Guid.NewGuid(),
            Title = title.Trim(),
            ProjectId = projectId,
            Location = NormalizeLocation(location),
            StartAtUtc = startAtUtc,
            EndAtUtc = endAtUtc,
            TimeZoneId = timeZoneId.Trim(),
            MeetingInvitationText = NormalizeInvitation(meetingInvitationText),
            ReminderLeadMinutes = NormalizeReminderLeadMinutes(reminderLeadMinutes),
            MeetingInvitationExpiresOn = string.IsNullOrWhiteSpace(meetingInvitationText)
                ? null
                // Keep the original invitation through the seventh day after the meeting.
                // The expiration date is exclusive: cleanup starts on day eight.
                : DateOnly.FromDateTime(startAt.DateTime).AddDays(8),
        };
    }

    public static CalendarEvent CreateAllDay(
        string title,
        Guid? projectId,
        DateOnly startDate,
        DateOnly endDateExclusive)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        if (endDateExclusive <= startDate)
        {
            throw new ArgumentException("The exclusive end date must be after the start date.", nameof(endDateExclusive));
        }

        return new CalendarEvent
        {
            Id = Guid.NewGuid(),
            Title = title.Trim(),
            ProjectId = projectId,
            IsAllDay = true,
            AllDayStart = startDate,
            AllDayEndExclusive = endDateExclusive,
        };
    }

    public void UpdateDetails(string title, string? location)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        Title = title.Trim();
        Location = NormalizeLocation(location);
    }

    public void RescheduleTimed(DateTimeOffset startAt, DateTimeOffset endAt, string timeZoneId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);

        DateTimeOffset startAtUtc = startAt.ToUniversalTime();
        DateTimeOffset endAtUtc = endAt.ToUniversalTime();

        if (endAtUtc <= startAtUtc)
        {
            throw new ArgumentException("The end time must be after the start time.", nameof(endAt));
        }

        IsAllDay = false;
        AllDayStart = null;
        AllDayEndExclusive = null;
        StartAtUtc = startAtUtc;
        EndAtUtc = endAtUtc;
        TimeZoneId = timeZoneId.Trim();
    }

    public void UpdateReminderLeadMinutes(int? reminderLeadMinutes)
    {
        ReminderLeadMinutes = NormalizeReminderLeadMinutes(reminderLeadMinutes);
    }

    public void ClearExpiredMeetingInvitation(DateOnly currentDate)
    {
        if (MeetingInvitationExpiresOn is { } expiresOn && currentDate >= expiresOn)
        {
            MeetingInvitationText = null;
            MeetingInvitationExpiresOn = null;
        }
    }

    public void RescheduleAllDay(DateOnly startDate, DateOnly endDateExclusive)
    {
        if (endDateExclusive <= startDate)
        {
            throw new ArgumentException("The exclusive end date must be after the start date.", nameof(endDateExclusive));
        }

        IsAllDay = true;
        StartAtUtc = null;
        EndAtUtc = null;
        TimeZoneId = null;
        AllDayStart = startDate;
        AllDayEndExclusive = endDateExclusive;
    }

    private static string? NormalizeLocation(string? location)
    {
        string trimmed = location?.Trim() ?? string.Empty;
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static string? NormalizeInvitation(string? text)
    {
        string trimmed = text?.Trim() ?? string.Empty;
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static int? NormalizeReminderLeadMinutes(int? minutes)
    {
        if (minutes is null)
        {
            return null;
        }

        if (minutes is < 1 or > 1440)
        {
            throw new ArgumentOutOfRangeException(nameof(minutes), "会议提醒时间必须在 1 到 1440 分钟之间。");
        }

        return minutes;
    }

    public void MoveToRecycleBin(DateTimeOffset deletedAtUtc)
    {
        DeletedAtUtc = deletedAtUtc.ToUniversalTime();
    }

    public void RestoreFromRecycleBin()
    {
        DeletedAtUtc = null;
    }
}
