using WorkFlow360.Domain.Enums;

namespace WorkFlow360.Domain.Entities;

public class Employee : AuditableEntity
{
    public string EmployeeCode { get; set; } = string.Empty;
    public int? UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public int DepartmentId { get; set; }
    public int DesignationId { get; set; }
    public int? ManagerId { get; set; }
    public DateOnly JoiningDate { get; set; }
    public EmploymentStatus Status { get; set; } = EmploymentStatus.Probation;

    public User? User { get; set; }
    public Department Department { get; set; } = null!;
    public Designation Designation { get; set; } = null!;
    public Employee? Manager { get; set; }
    public ICollection<Employee> DirectReports { get; set; } = new List<Employee>();

    public string FullName => $"{FirstName} {LastName}";

    public bool IsCurrent => Status is not (EmploymentStatus.Resigned or EmploymentStatus.Terminated);
}
