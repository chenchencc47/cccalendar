using CcCalendar.Core.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CcCalendar.Infrastructure.Persistence.Configurations;

internal sealed class ParticipantConfiguration : IEntityTypeConfiguration<Participant>
{
    public void Configure(EntityTypeBuilder<Participant> builder)
    {
        builder.ToTable("Participants");
        builder.HasKey(participant => participant.Id);
        builder.Property(participant => participant.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(participant => participant.Role).HasMaxLength(100).IsRequired();
    }
}
