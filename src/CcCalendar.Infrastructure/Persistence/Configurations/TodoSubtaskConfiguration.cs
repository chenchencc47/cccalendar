using CcCalendar.Core.Todos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CcCalendar.Infrastructure.Persistence.Configurations;

internal sealed class TodoSubtaskConfiguration : IEntityTypeConfiguration<TodoSubtask>
{
    public void Configure(EntityTypeBuilder<TodoSubtask> builder)
    {
        builder.ToTable("TodoSubtasks");
        builder.HasKey(subtask => subtask.Id);
        builder.Property(subtask => subtask.Title).HasMaxLength(500).IsRequired();
        builder.Ignore(subtask => subtask.IsCompleted);
    }
}
