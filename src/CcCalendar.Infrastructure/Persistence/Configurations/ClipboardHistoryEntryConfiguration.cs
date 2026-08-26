using CcCalendar.Core.Tools;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CcCalendar.Infrastructure.Persistence.Configurations;

internal sealed class ClipboardHistoryEntryConfiguration
    : IEntityTypeConfiguration<ClipboardHistoryEntry>
{
    public void Configure(EntityTypeBuilder<ClipboardHistoryEntry> builder)
    {
        builder.ToTable("ClipboardHistoryEntries");
        builder.HasKey(entry => entry.Id);
        builder.HasIndex(entry => entry.CreatedAtUtc);
        builder.HasIndex(entry => entry.IsFavorite);
        builder.Property(entry => entry.Preview).HasMaxLength(120);
        builder.Property(entry => entry.CreatedAtUtc).HasConversion<UtcDateTimeOffsetConverter>();
    }
}
