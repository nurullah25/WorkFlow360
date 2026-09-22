using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkFlow360.Domain.Entities;

namespace WorkFlow360.Infrastructure.Persistence.Configurations;

public class TaskHistoryEntryConfiguration : IEntityTypeConfiguration<TaskHistoryEntry>
{
    public void Configure(EntityTypeBuilder<TaskHistoryEntry> builder)
    {
        builder.ToTable("TaskHistory");

        builder.Property(h => h.FieldName).HasMaxLength(50).IsRequired();
        builder.Property(h => h.OldValue).HasMaxLength(500);
        builder.Property(h => h.NewValue).HasMaxLength(500);

        builder.HasIndex(h => new { h.TaskId, h.ChangedAt });

        builder.HasOne(h => h.Task)
            .WithMany(t => t.History)
            .HasForeignKey(h => h.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(h => h.ChangedBy)
            .WithMany()
            .HasForeignKey(h => h.ChangedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
