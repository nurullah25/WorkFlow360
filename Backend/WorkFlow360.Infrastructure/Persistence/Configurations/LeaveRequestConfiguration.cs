using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkFlow360.Domain.Entities;

namespace WorkFlow360.Infrastructure.Persistence.Configurations;

public class LeaveRequestConfiguration : IEntityTypeConfiguration<LeaveRequest>
{
    public void Configure(EntityTypeBuilder<LeaveRequest> builder)
    {
        builder.ToTable(t => t.HasCheckConstraint("CK_LeaveRequests_Dates", "[EndDate] >= [StartDate]"));

        // The UPDATE includes "WHERE Status = <value read>", so two people reviewing the same request
        // at once can't both succeed; the second gets a concurrency error instead.
        builder.Property(r => r.Status).IsConcurrencyToken();

        builder.Property(r => r.TotalDays).HasPrecision(5, 1);
        builder.Property(r => r.Reason).HasMaxLength(500).IsRequired();
        builder.Property(r => r.ReviewComment).HasMaxLength(500);

        builder.HasIndex(r => new { r.EmployeeId, r.Status });
        builder.HasIndex(r => new { r.Status, r.StartDate });

        builder.HasOne(r => r.Employee)
            .WithMany()
            .HasForeignKey(r => r.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.LeaveType)
            .WithMany()
            .HasForeignKey(r => r.LeaveTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.ReviewedBy)
            .WithMany()
            .HasForeignKey(r => r.ReviewedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
