using CcCalendar.Core.Projects;
using CcCalendar.Core.Todos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CcCalendar.Infrastructure.Persistence.Configurations;

internal sealed class TodoItemConfiguration : IEntityTypeConfiguration<TodoItem>
{
    public void Configure(EntityTypeBuilder<TodoItem> builder)
    {
        builder.ToTable("Todos");
        builder.HasKey(todo => todo.Id);
        builder.Property(todo => todo.Title).HasMaxLength(500).IsRequired();
        builder.Property(todo => todo.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Ignore(todo => todo.ProgressPercent);
        builder.Ignore(todo => todo.IsDeleted);
        builder.HasQueryFilter(todo => todo.DeletedAtUtc == null);

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(todo => todo.ProjectId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(todo => todo.Subtasks)
            .WithOne()
            .HasForeignKey("TodoItemId")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(todo => todo.Subtasks).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(todo => todo.Dependencies)
            .WithOne()
            .HasForeignKey("TodoItemId")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(todo => todo.Dependencies).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
