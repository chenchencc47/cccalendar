namespace CcCalendar.Core.Recycling;

public interface IRecyclableEntity
{
    DateTimeOffset? DeletedAtUtc { get; }

    bool IsDeleted { get; }

    void MoveToRecycleBin(DateTimeOffset deletedAtUtc);

    void RestoreFromRecycleBin();
}
