using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using WorkFlow360.Application.Attendance;
using WorkFlow360.Application.AuditLogs;
using WorkFlow360.Application.Auth;
using WorkFlow360.Application.Common;
using WorkFlow360.Application.Dashboard;
using WorkFlow360.Application.Employees;
using WorkFlow360.Application.Leave;
using WorkFlow360.Application.Notifications;
using WorkFlow360.Application.Organisation;
using WorkFlow360.Application.Projects;
using WorkFlow360.Application.Tasks;
using WorkFlow360.Application.Users;

namespace WorkFlow360.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<CompanyTime>();
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.AddScoped<AuditTrail>();
        services.AddScoped<BusinessCalendar>();
        services.AddScoped<AttendanceService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<NotificationService>();
        services.AddScoped<AuditLogService>();
        services.AddScoped<LeaveService>();
        services.AddScoped<LeaveTypeService>();
        services.AddScoped<LeaveBalanceService>();
        services.AddScoped<HolidayService>();
        services.AddScoped<NotificationSender>();
        services.AddScoped<ProjectAccess>();
        services.AddScoped<ProjectService>();
        services.AddScoped<TaskService>();
        services.AddScoped<AuthService>();
        services.AddScoped<DepartmentService>();
        services.AddScoped<DesignationService>();
        services.AddScoped<EmployeeService>();
        services.AddScoped<UserService>();

        return services;
    }
}
