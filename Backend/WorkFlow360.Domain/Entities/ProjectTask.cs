using WorkFlow360.Domain.Enums;

namespace WorkFlow360.Domain.Entities;

public class ProjectTask : AuditableEntity
{
    public int ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? AssigneeId { get; set; }
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    public ProjectTaskStatus Status { get; set; } = ProjectTaskStatus.Todo;
    public DateOnly? DueDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int CreatedByUserId { get; set; }

    public Project Project { get; set; } = null!;
    public Employee? Assignee { get; set; }
    public ICollection<TaskComment> Comments { get; set; } = new List<TaskComment>();
    public ICollection<TaskHistoryEntry> History { get; set; } = new List<TaskHistoryEntry>();
}
