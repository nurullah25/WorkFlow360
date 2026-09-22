using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkFlow360.Domain.Entities;

namespace WorkFlow360.Infrastructure.Persistence.Configurations;

public class LeaveBalanceConfiguration : IEntityTypeConfiguration<LeaveBalance>
{
    public void Configure(EntityTypeBuilder<LeaveBalance> builder)
    {
        builder.ToTable(t => t.HasCheckConstraint("CK_LeaveBalances_UsedDays", "[UsedDays] >= 0"));

        builder.Property(b => b.AllocatedDays).HasPrecision(5, 1);
        builder.Property(b => b.UsedDays).HasPrecision(5, 1).IsConcurrencyToken();

        builder.HasIndex(b => new { b.EmployeeId, b.LeaveTypeId, b.Year }).IsUnique();

        builder.HasOne(b => b.Employee)
            .WithMany()
            .HasForeignKey(b => b.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.LeaveType)
            .WithMany()
            .HasForeignKey(b => b.LeaveTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
