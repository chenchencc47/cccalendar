namespace CcCalendar.Core.Todos;

public sealed class TodoDependency
{
    private TodoDependency()
    {
    }

    internal TodoDependency(Guid dependsOnTodoItemId)
    {
        Id = Guid.NewGuid();
        DependsOnTodoItemId = dependsOnTodoItemId;
    }

    public Guid Id { get; private set; }

    public Guid DependsOnTodoItemId { get; private set; }
}
