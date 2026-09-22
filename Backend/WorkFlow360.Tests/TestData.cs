using WorkFlow360.Domain.Entities;
using WorkFlow360.Domain.Enums;
using WorkFlow360.Infrastructure.Persistence;

namespace WorkFlow360.Tests;

/// <summary>Builders for the rows most tests need. Role ids match the seeded Roles table.</summary>
public static class TestData
{
    public const int AdminRoleId = 1;
    public const int HrRoleId = 2;
    public const int ManagerRoleId = 3;
    public const int EmployeeRoleId = 4;

    public static User AddUser(AppDbContext db, string email, int roleId = EmployeeRoleId)
    {
        var user = new User { Email = email, FullName = email.Split('@')[0], PasswordHash = "not-used", RoleId = roleId };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    public static Department AddDepartment(AppDbContext db, string code = "ENG", string name = "Engineering")
    {
        var department = new Department { Code = code, Name = name };
        db.Departments.Add(department);
        db.SaveChanges();
        return department;
    }

    public static Designation AddDesignation(AppDbContext db, string title = "Software Engineer")
    {
        var designation = new Designation { Title = title };
        db.Designations.Add(designation);
        db.SaveChanges();
        return designation;
    }

    public static Employee AddEmployee(
        AppDbContext db,
        Department department,
        Designation designation,
        string code,
        Employee? manager = null,
        User? user = null)
    {
        var employee = new Employee
        {
            EmployeeCode = code,
            FirstName = "Test",
            LastName = code,
            Email = $"{code.ToLowerInvariant()}@test.local",
            DepartmentId = department.Id,
            DesignationId = designation.Id,
            ManagerId = manager?.Id,
            UserId = user?.Id,
            JoiningDate = new DateOnly(2024, 1, 1),
            Status = EmploymentStatus.Active
        };
        db.Employees.Add(employee);
        db.SaveChanges();
        return employee;
    }
}
