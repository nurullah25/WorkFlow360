using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkFlow360.Domain.Entities;

namespace WorkFlow360.Infrastructure.Persistence.Configurations;

public class TaskCommentConfiguration : IEntityTypeConfiguration<TaskComment>
{
    public void Configure(EntityTypeBuilder<TaskComment> builder)
    {
        builder.Property(c => c.Body).HasMaxLength(2000).IsRequired();

        builder.HasIndex(c => new { c.TaskId, c.CreatedAt });

        builder.HasOne(c => c.Task)
            .WithMany(t => t.Comments)
            .HasForeignKey(c => c.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Author)
            .WithMany()
            .HasForeignKey(c => c.AuthorUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
