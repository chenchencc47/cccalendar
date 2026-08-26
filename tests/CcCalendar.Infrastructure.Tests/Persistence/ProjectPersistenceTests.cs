using CcCalendar.Core.Projects;
using Microsoft.EntityFrameworkCore;

namespace CcCalendar.Infrastructure.Tests.Persistence;

public sealed class ProjectPersistenceTests
{
    [Fact]
    public async Task ProjectRoundTripsWithMilestonesAndParticipants()
    {
        await using var database = new TemporaryCalendarDatabase();
        await database.InitializeAsync(CancellationToken.None);

        Guid projectId;

        await using (var writeContext = database.CreateContext())
        {
            Project project = Project.Create(
                "cccalendar",
                "#246BCE",
                new DateOnly(2026, 8, 16),
                new DateOnly(2026, 12, 31));
            project.AddMilestone("Local planning workflow", new DateOnly(2026, 9, 1));
            project.AddParticipant("MSN", "Owner");
            projectId = project.Id;

            writeContext.Projects.Add(project);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        await using var readContext = database.CreateContext();
        Project actual = await readContext.Projects
            .Include(project => project.Milestones)
            .Include(project => project.Participants)
            .SingleAsync(project => project.Id == projectId);

        Assert.Equal("cccalendar", actual.Name);
        Assert.Single(actual.Milestones);
        Assert.Single(actual.Participants);
    }
}
