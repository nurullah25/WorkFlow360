using WorkFlow360.Domain.Entities;
using WorkFlow360.Domain.Enums;

namespace WorkFlow360.Application.Employees;

public static class EmployeeQueryExtensions
{
    /// <summary>SQL-translatable version of <see cref="Employee.IsCurrent"/>.</summary>
    public static IQueryable<Employee> WhereCurrent(this IQueryable<Employee> query) =>
        query.Where(e => e.Status != EmploymentStatus.Resigned && e.Status != EmploymentStatus.Terminated);
}
