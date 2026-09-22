using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkFlow360.Domain.Entities;

namespace WorkFlow360.Infrastructure.Persistence.Configurations;

public class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.Property(d => d.Code).HasMaxLength(20).IsRequired();
        builder.Property(d => d.Name).HasMaxLength(100).IsRequired();

        builder.HasIndex(d => d.Code).IsUnique();
        builder.HasIndex(d => d.Name).IsUnique();
    }
}
