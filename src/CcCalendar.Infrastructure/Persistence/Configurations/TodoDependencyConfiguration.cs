using CcCalendar.Core.Todos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CcCalendar.Infrastructure.Persistence.Configurations;

internal sealed class TodoDependencyConfiguration : IEntityTypeConfiguration<TodoDependency>
{
    public void Configure(EntityTypeBuilder<TodoDependency> builder)
    {
        builder.ToTable("TodoDependencies");
        builder.HasKey(dependency => dependency.Id);
        builder.HasIndex("TodoItemId", nameof(TodoDependency.DependsOnTodoItemId)).IsUnique();

        builder.HasOne<TodoItem>()
            .WithMany()
            .HasForeignKey(dependency => dependency.DependsOnTodoItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
