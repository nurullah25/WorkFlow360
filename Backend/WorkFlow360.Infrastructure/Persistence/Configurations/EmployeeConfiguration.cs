using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkFlow360.Domain.Entities;

namespace WorkFlow360.Infrastructure.Persistence.Configurations;

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.Property(e => e.EmployeeCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.FirstName).HasMaxLength(50).IsRequired();
        builder.Property(e => e.LastName).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Email).HasMaxLength(256).IsRequired();
        builder.Property(e => e.Phone).HasMaxLength(30);

        builder.Ignore(e => e.FullName);
        builder.Ignore(e => e.IsCurrent);

        builder.HasIndex(e => e.EmployeeCode).IsUnique();
        builder.HasIndex(e => e.Email).IsUnique();
        builder.HasIndex(e => e.UserId).IsUnique().HasFilter("[UserId] IS NOT NULL");

        builder.HasOne(e => e.User)
            .WithOne(u => u.Employee)
            .HasForeignKey<Employee>(e => e.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Department)
            .WithMany(d => d.Employees)
            .HasForeignKey(e => e.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Designation)
            .WithMany()
            .HasForeignKey(e => e.DesignationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Manager)
            .WithMany(m => m.DirectReports)
            .HasForeignKey(e => e.ManagerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
