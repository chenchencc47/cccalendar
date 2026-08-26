using CcCalendar.Core.Schedules;
using CcCalendar.Core.Todos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CcCalendar.Infrastructure.Persistence.Configurations;

internal sealed class TimeBlockConfiguration : IEntityTypeConfiguration<TimeBlock>
{
    public void Configure(EntityTypeBuilder<TimeBlock> builder)
    {
        builder.ToTable("TimeBlocks");
        builder.HasKey(block => block.Id);
        builder.Property(block => block.TimeZoneId).HasMaxLength(200).IsRequired();
        builder.Ignore(block => block.Duration);

        builder.HasOne<TodoItem>()
            .WithMany()
            .HasForeignKey(block => block.TodoItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
