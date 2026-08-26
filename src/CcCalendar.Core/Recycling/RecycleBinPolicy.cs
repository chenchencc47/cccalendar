namespace CcCalendar.Core.Recycling;

public static class RecycleBinPolicy
{
    public static readonly TimeSpan DefaultRetention = TimeSpan.FromDays(30);

    public static bool CanPurge(DateTimeOffset deletedAtUtc, DateTimeOffset nowUtc)
    {
        return nowUtc.ToUniversalTime() - deletedAtUtc.ToUniversalTime() >= DefaultRetention;
    }
}
