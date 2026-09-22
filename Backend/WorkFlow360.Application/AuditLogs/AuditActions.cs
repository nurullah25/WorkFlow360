namespace WorkFlow360.Application.AuditLogs;

public static class AuditActions
{
    public const string Login = "Login";
    public const string LoginFailed = "LoginFailed";
    public const string Logout = "Logout";
    public const string SessionsRevoked = "SessionsRevoked";

    public const string UserCreated = "UserCreated";
    public const string UserUpdated = "UserUpdated";
    public const string PasswordReset = "PasswordReset";

    public const string EmployeeCreated = "EmployeeCreated";
    public const string EmployeeUpdated = "EmployeeUpdated";

    public const string DepartmentCreated = "DepartmentCreated";
    public const string DepartmentUpdated = "DepartmentUpdated";
    public const string DesignationCreated = "DesignationCreated";
    public const string DesignationUpdated = "DesignationUpdated";

    public const string ProjectCreated = "ProjectCreated";
    public const string ProjectUpdated = "ProjectUpdated";
    public const string ProjectMemberAdded = "ProjectMemberAdded";
    public const string ProjectMemberRemoved = "ProjectMemberRemoved";
    public const string TaskAssigned = "TaskAssigned";

    public const string LeaveSubmitted = "LeaveSubmitted";
    public const string LeaveApproved = "LeaveApproved";
    public const string LeaveRejected = "LeaveRejected";
    public const string LeaveCancelled = "LeaveCancelled";
    public const string LeaveBalanceAdjusted = "LeaveBalanceAdjusted";
    public const string LeaveBalancesGenerated = "LeaveBalancesGenerated";
    public const string LeaveTypeCreated = "LeaveTypeCreated";
    public const string LeaveTypeUpdated = "LeaveTypeUpdated";
    public const string HolidayCreated = "HolidayCreated";
    public const string HolidayDeleted = "HolidayDeleted";
}
