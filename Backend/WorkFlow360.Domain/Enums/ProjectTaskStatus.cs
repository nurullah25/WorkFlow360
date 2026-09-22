namespace WorkFlow360.Domain.Enums;

// Not named TaskStatus to avoid clashing with System.Threading.Tasks.TaskStatus.
public enum ProjectTaskStatus
{
    Todo,
    InProgress,
    InReview,
    Done,
    Cancelled
}
