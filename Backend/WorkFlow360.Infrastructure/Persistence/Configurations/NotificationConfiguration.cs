using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkFlow360.Domain.Entities;

namespace WorkFlow360.Infrastructure.Persistence.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.Property(n => n.Title).HasMaxLength(150).IsRequired();
        builder.Property(n => n.Message).HasMaxLength(500).IsRequired();
        builder.Property(n => n.LinkUrl).HasMaxLength(300);

        builder.HasIndex(n => new { n.UserId, n.IsRead, n.CreatedAt });

        builder.HasOne(n => n.User)
            .WithMany()
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
