using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkFlow360.Domain.Entities;

namespace WorkFlow360.Infrastructure.Persistence.Configurations;

public class LeaveTypeConfiguration : IEntityTypeConfiguration<LeaveType>
{
    public void Configure(EntityTypeBuilder<LeaveType> builder)
    {
        builder.Property(t => t.Code).HasMaxLength(10).IsRequired();
        builder.Property(t => t.Name).HasMaxLength(50).IsRequired();
        builder.Property(t => t.DefaultDaysPerYear).HasPrecision(5, 1);

        builder.HasIndex(t => t.Code).IsUnique();

        builder.HasData(
            new LeaveType { Id = 1, Code = "AL", Name = "Annual Leave", DefaultDaysPerYear = 18, IsPaid = true, IsActive = true },
            new LeaveType { Id = 2, Code = "SL", Name = "Sick Leave", DefaultDaysPerYear = 14, IsPaid = true, IsActive = true },
            new LeaveType { Id = 3, Code = "CL", Name = "Casual Leave", DefaultDaysPerYear = 10, IsPaid = true, IsActive = true });
    }
}
