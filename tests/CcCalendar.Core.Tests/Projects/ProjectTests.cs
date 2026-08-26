using CcCalendar.Core.Projects;

namespace CcCalendar.Core.Tests.Projects;

public sealed class ProjectTests
{
    [Fact]
    public void CreateInitializesAPlannedProject()
    {
        var project = Project.Create(
            "  cccalendar  ",
            "#246BCE",
            new DateOnly(2026, 8, 16),
            new DateOnly(2026, 12, 31));

        Assert.NotEqual(Guid.Empty, project.Id);
        Assert.Equal("cccalendar", project.Name);
        Assert.Equal("#246BCE", project.Color);
        Assert.Equal(ProjectStatus.Planned, project.Status);
        Assert.Empty(project.Milestones);
        Assert.Empty(project.Participants);
        Assert.Null(project.ArchivedAtUtc);
    }

    [Fact]
    public void CreateRejectsDueDateBeforeStartDate()
    {
        Action create = () => Project.Create(
            "cccalendar",
            "#246BCE",
            new DateOnly(2026, 8, 17),
            new DateOnly(2026, 8, 16));

        Assert.Throws<ArgumentException>(create);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateRejectsBlankName(string name)
    {
        Action create = () => Project.Create(name, "#246BCE", null, null);

        Assert.Throws<ArgumentException>(create);
    }

    [Theory]
    [InlineData("246BCE")]
    [InlineData("#GGGGGG")]
    [InlineData("#1234")]
    public void CreateRejectsInvalidColor(string color)
    {
        Action create = () => Project.Create("cccalendar", color, null, null);

        Assert.Throws<ArgumentException>(create);
    }

    [Fact]
    public void AddMilestoneAddsMilestoneToProject()
    {
        Project project = CreateProject();

        Milestone milestone = project.AddMilestone("First usable calendar", new DateOnly(2026, 9, 1));

        Assert.Single(project.Milestones, milestone);
        Assert.False(milestone.IsCompleted);
    }

    [Fact]
    public void CompleteMilestoneRecordsCompletionTimestamp()
    {
        Project project = CreateProject();
        Milestone milestone = project.AddMilestone("First usable calendar", new DateOnly(2026, 9, 1));
        var completedAtUtc = new DateTimeOffset(2026, 8, 30, 9, 0, 0, TimeSpan.Zero);

        milestone.Complete(completedAtUtc);

        Assert.True(milestone.IsCompleted);
        Assert.Equal(completedAtUtc, milestone.CompletedAtUtc);
    }

    [Fact]
    public void AddParticipantRejectsDuplicateDisplayName()
    {
        Project project = CreateProject();
        project.AddParticipant("MSN", "Owner");

        Action addDuplicate = () => project.AddParticipant(" msn ", "Developer");

        Assert.Throws<InvalidOperationException>(addDuplicate);
    }

    [Fact]
    public void ArchiveRecordsTimestampAndStatus()
    {
        Project project = CreateProject();
        var archivedAtUtc = new DateTimeOffset(2026, 8, 16, 8, 30, 0, TimeSpan.Zero);

        project.Archive(archivedAtUtc);

        Assert.Equal(ProjectStatus.Archived, project.Status);
        Assert.Equal(archivedAtUtc, project.ArchivedAtUtc);
    }

    private static Project CreateProject()
    {
        return Project.Create("cccalendar", "#246BCE", null, null);
    }
}
