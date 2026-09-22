using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using WorkFlow360.Domain.Entities;

namespace WorkFlow360.Application.Common;

public interface IAppDbContext
{
    DbSet<Role> Roles { get; }
    DbSet<User> Users { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<Department> Departments { get; }
    DbSet<Designation> Designations { get; }
    DbSet<Employee> Employees { get; }
    DbSet<Project> Projects { get; }
    DbSet<ProjectMember> ProjectMembers { get; }
    DbSet<ProjectTask> ProjectTasks { get; }
    DbSet<TaskComment> TaskComments { get; }
    DbSet<TaskHistoryEntry> TaskHistory { get; }
    DbSet<AttendanceRecord> AttendanceRecords { get; }
    DbSet<Holiday> Holidays { get; }
    DbSet<LeaveType> LeaveTypes { get; }
    DbSet<LeaveBalance> LeaveBalances { get; }
    DbSet<LeaveRequest> LeaveRequests { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<AuditLog> AuditLogs { get; }

    /// <summary>Used for explicit transactions, e.g. when an audit entry needs the id of a row inserted in the same operation.</summary>
    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
