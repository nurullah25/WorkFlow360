using WorkFlow360.Domain.Enums;

namespace WorkFlow360.Domain.Entities;

public class Project : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int ManagerId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public ProjectStatus Status { get; set; } = ProjectStatus.Planned;
    public int CreatedByUserId { get; set; }

    public Employee Manager { get; set; } = null!;
    public ICollection<ProjectMember> Members { get; set; } = new List<ProjectMember>();
    public ICollection<ProjectTask> Tasks { get; set; } = new List<ProjectTask>();

    public bool IsClosed => Status is ProjectStatus.Completed or ProjectStatus.Cancelled;
}
