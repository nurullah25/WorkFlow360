using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkFlow360.Domain.Entities;

namespace WorkFlow360.Infrastructure.Persistence.Configurations;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable(t => t.HasCheckConstraint("CK_Projects_EndDate", "[EndDate] IS NULL OR [EndDate] >= [StartDate]"));

        builder.Property(p => p.Code).HasMaxLength(20).IsRequired();
        builder.Property(p => p.Name).HasMaxLength(150).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(2000);

        builder.Ignore(p => p.IsClosed);

        builder.HasIndex(p => p.Code).IsUnique();
        builder.HasIndex(p => p.Status);

        builder.HasOne(p => p.Manager)
            .WithMany()
            .HasForeignKey(p => p.ManagerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(p => p.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
