using CcCalendar.Core.Records;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CcCalendar.Infrastructure.Persistence.Configurations;

internal sealed class RecordAttachmentConfiguration : IEntityTypeConfiguration<RecordAttachment>
{
    public void Configure(EntityTypeBuilder<RecordAttachment> builder)
    {
        builder.ToTable("RecordAttachments");
        builder.HasKey(attachment => attachment.Id);
        builder.Property(attachment => attachment.FileName).HasMaxLength(260).IsRequired();
        builder.Property(attachment => attachment.RelativePath).HasMaxLength(1000).IsRequired();
        builder.Property(attachment => attachment.MediaType).HasMaxLength(200).IsRequired();
    }
}
