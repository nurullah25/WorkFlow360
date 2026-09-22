namespace WorkFlow360.Domain.Entities;

public class Designation
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
