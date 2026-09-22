namespace WorkFlow360.Domain.Entities;

public static class RoleNames
{
    public const string Admin = "Admin";
    public const string HR = "HR";
    public const string Manager = "Manager";
    public const string Employee = "Employee";

    /// <summary>For [Authorize(Roles = ...)], which takes a comma-separated list.</summary>
    public const string AdminOrHR = Admin + "," + HR;
    public const string AdminOrManager = Admin + "," + Manager;

    public static readonly IReadOnlyList<string> All = [Admin, HR, Manager, Employee];
}
