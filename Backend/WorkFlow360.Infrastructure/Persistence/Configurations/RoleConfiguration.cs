using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkFlow360.Domain.Entities;

namespace WorkFlow360.Infrastructure.Persistence.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Name).HasMaxLength(50).IsRequired();
        builder.HasIndex(r => r.Name).IsUnique();

        builder.HasData(
            new Role { Id = 1, Name = RoleNames.Admin },
            new Role { Id = 2, Name = RoleNames.HR },
            new Role { Id = 3, Name = RoleNames.Manager },
            new Role { Id = 4, Name = RoleNames.Employee });
    }
}
