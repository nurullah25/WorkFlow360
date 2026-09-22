namespace WorkFlow360.Domain.Entities;

public class TaskComment
{
    public int Id { get; set; }
    public int TaskId { get; set; }
    public int AuthorUserId { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public ProjectTask Task { get; set; } = null!;
    public User Author { get; set; } = null!;
}
