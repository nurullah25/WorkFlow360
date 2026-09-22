using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkFlow360.Domain.Entities;

namespace WorkFlow360.Infrastructure.Persistence.Configurations;

public class ProjectTaskConfiguration : IEntityTypeConfiguration<ProjectTask>
{
    public void Configure(EntityTypeBuilder<ProjectTask> builder)
    {
        builder.ToTable("ProjectTasks");

        builder.Property(t => t.Title).HasMaxLength(200).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(4000);

        builder.HasIndex(t => new { t.ProjectId, t.Status });
        builder.HasIndex(t => new { t.AssigneeId, t.Status });
        builder.HasIndex(t => new { t.Status, t.DueDate });

        builder.HasOne(t => t.Project)
            .WithMany(p => p.Tasks)
            .HasForeignKey(t => t.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Assignee)
            .WithMany()
            .HasForeignKey(t => t.AssigneeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(t => t.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
