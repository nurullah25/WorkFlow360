namespace WorkFlow360.Domain.Entities;

public class ProjectMember
{
    public int ProjectId { get; set; }
    public int EmployeeId { get; set; }
    public string? RoleInProject { get; set; }
    public DateTime AddedAt { get; set; }

    public Project Project { get; set; } = null!;
    public Employee Employee { get; set; } = null!;
}
