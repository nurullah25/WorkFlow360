namespace WorkFlow360.Domain.Entities;

public class AttendanceRecord
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public DateOnly WorkDate { get; set; }
    public DateTime CheckInAt { get; set; }
    public DateTime? CheckOutAt { get; set; }
    public bool IsLate { get; set; }
    public int? WorkedMinutes { get; set; }

    public Employee Employee { get; set; } = null!;
}
