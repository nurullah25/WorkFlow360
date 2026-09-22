namespace WorkFlow360.Domain.Entities;

public class TaskHistoryEntry
{
    public int Id { get; set; }
    public int TaskId { get; set; }
    public int ChangedByUserId { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime ChangedAt { get; set; }

    public ProjectTask Task { get; set; } = null!;
    public User ChangedBy { get; set; } = null!;
}
