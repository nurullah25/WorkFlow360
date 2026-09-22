using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using WorkFlow360.Application.AuditLogs;
using WorkFlow360.Application.Common;
using WorkFlow360.Application.Common.Exceptions;
using WorkFlow360.Application.Notifications;
using WorkFlow360.Domain.Entities;
using WorkFlow360.Domain.Enums;

namespace WorkFlow360.Application.Leave;

/// <summary>
/// Leave requests: submit → approve / reject by the employee's manager (HR when they have none) → cancel.
/// Balances only change when a request is approved or approved leave is cancelled; pending requests
/// are still counted when checking what's available, so an employee can't over-book with several requests.
/// </summary>
public class LeaveService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly AuditTrail _auditTrail;
    private readonly NotificationSender _notifications;
    private readonly CompanyTime _companyTime;
    private readonly BusinessCalendar _calendar;

    public LeaveService(
        IAppDbContext db,
        ICurrentUser currentUser,
        AuditTrail auditTrail,
        NotificationSender notifications,
        CompanyTime companyTime,
        BusinessCalendar calendar)
    {
        _db = db;
        _currentUser = currentUser;
        _auditTrail = auditTrail;
        _notifications = notifications;
        _companyTime = companyTime;
        _calendar = calendar;
    }

    public async Task<PagedResult<LeaveRequestDto>> ListAsync(LeaveRequestQuery query, CancellationToken cancellationToken)
    {
        var requests = ApplyScope(_db.LeaveRequests.AsNoTracking(), query.Scope);

        if (query.Status.HasValue)
            requests = requests.Where(r => r.Status == query.Status);
        if (query.LeaveTypeId.HasValue)
            requests = requests.Where(r => r.LeaveTypeId == query.LeaveTypeId);
        if (query.Year.HasValue)
        {
            var (yearStart, yearEnd) = YearRange(query.Year.Value);
            requests = requests.Where(r => r.StartDate >= yearStart && r.StartDate <= yearEnd);
        }
        if (query.SearchTerm is { } term)
        {
            requests = requests.Where(r =>
                (r.Employee.FirstName + " " + r.Employee.LastName).Contains(term) || r.Employee.EmployeeCode.Contains(term));
        }

        // Approvals are worked oldest-first; history is read newest-first.
        requests = query.Scope == LeaveScopes.Approvals
            ? requests.OrderBy(r => r.StartDate).ThenBy(r => r.Id)
            : requests.OrderByDescending(r => r.StartDate).ThenByDescending(r => r.Id);

        var page = await requests.Select(ToRow).ToPagedResultAsync(query, cancellationToken);

        return new PagedResult<LeaveRequestDto>(
            page.Items.Select(WithPermissions).ToList(), page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<IReadOnlyList<LeaveBalanceDto>> GetMyBalancesAsync(int? year, CancellationToken cancellationToken)
    {
        var employeeId = RequireEmployeeId();
        var balanceYear = year ?? _companyTime.Today.Year;
        var (yearStart, yearEnd) = YearRange(balanceYear);

        var balances = await _db.LeaveBalances
            .AsNoTracking()
            .Where(b => b.EmployeeId == employeeId && b.Year == balanceYear)
            .OrderBy(b => b.LeaveType.Name)
            .Select(b => new
            {
                b.Id,
                b.LeaveTypeId,
                b.LeaveType.Code,
                b.LeaveType.Name,
                b.AllocatedDays,
                b.UsedDays,
                PendingDays = _db.LeaveRequests
                    .Where(r => r.EmployeeId == employeeId
                        && r.LeaveTypeId == b.LeaveTypeId
                        && r.Status == LeaveStatus.Pending
                        && r.StartDate >= yearStart && r.StartDate <= yearEnd)
                    .Sum(r => (decimal?)r.TotalDays) ?? 0
            })
            .ToListAsync(cancellationToken);

        return balances
            .Select(b => new LeaveBalanceDto(
                b.Id, b.LeaveTypeId, b.Code, b.Name, balanceYear,
                b.AllocatedDays, b.UsedDays, b.PendingDays, b.AllocatedDays - b.UsedDays - b.PendingDays))
            .ToList();
    }

    /// <summary>Lets the request form show "3 working days, 12 available" before submitting.</summary>
    public async Task<LeavePreviewDto> PreviewAsync(int leaveTypeId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken)
    {
        var employeeId = RequireEmployeeId();
        var workingDays = await _calendar.CountWorkingDaysAsync(startDate, endDate, cancellationToken);
        var balance = await _db.LeaveBalances.AsNoTracking().FirstOrDefaultAsync(b =>
            b.EmployeeId == employeeId && b.LeaveTypeId == leaveTypeId && b.Year == startDate.Year, cancellationToken);

        decimal? available = balance == null
            ? null
            : balance.AllocatedDays - balance.UsedDays - await GetPendingDaysAsync(employeeId, leaveTypeId, startDate.Year, null, cancellationToken);

        return new LeavePreviewDto(workingDays, available);
    }

    public async Task<LeaveRequestDto> SubmitAsync(SubmitLeaveRequest request, CancellationToken cancellationToken)
    {
        var employeeId = RequireEmployeeId();
        var employee = await _db.Employees.FirstAsync(e => e.Id == employeeId, cancellationToken);

        if (!employee.IsCurrent)
            throw new BusinessRuleException("Former employees can't request leave.");

        var leaveType = await _db.LeaveTypes.FirstOrDefaultAsync(t => t.Id == request.LeaveTypeId && t.IsActive, cancellationToken)
            ?? throw new BusinessRuleException("The selected leave type doesn't exist or is no longer offered.");

        if (request.StartDate < _companyTime.Today)
            throw new BusinessRuleException("Leave can't start in the past.");

        if (request.StartDate.Year != request.EndDate.Year)
            throw new BusinessRuleException("Leave balances are yearly, so leave that crosses into a new year must be requested in two parts.");

        var workingDays = await _calendar.CountWorkingDaysAsync(request.StartDate, request.EndDate, cancellationToken);
        if (workingDays == 0)
            throw new BusinessRuleException("The selected dates are all weekends or public holidays.");

        var overlaps = await _db.LeaveRequests.AnyAsync(r =>
            r.EmployeeId == employeeId
            && (r.Status == LeaveStatus.Pending || r.Status == LeaveStatus.Approved)
            && r.StartDate <= request.EndDate
            && r.EndDate >= request.StartDate, cancellationToken);
        if (overlaps)
            throw new BusinessRuleException("You already have leave requested or approved for some of these dates.");

        await EnsureBalanceCoversAsync(employeeId, leaveType, request.StartDate.Year, workingDays, excludeRequestId: null, cancellationToken);

        var leave = new LeaveRequest
        {
            EmployeeId = employeeId,
            LeaveTypeId = leaveType.Id,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            TotalDays = workingDays,
            Reason = request.Reason.Trim(),
            Status = LeaveStatus.Pending
        };
        _db.LeaveRequests.Add(leave);

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        _auditTrail.Record(AuditActions.LeaveSubmitted, nameof(LeaveRequest), leave.Id,
            new { leaveType.Code, leave.StartDate, leave.EndDate, leave.TotalDays });
        _notifications.Notify(
            await GetApproverUserIdsAsync(employee, cancellationToken),
            NotificationType.LeaveSubmitted,
            "Leave request to review",
            $"{employee.FullName} requested {workingDays} day(s) of {leaveType.Name} from {leave.StartDate:d MMM}.",
            "/leave?tab=approvals");

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetAsync(leave.Id, cancellationToken);
    }

    public async Task<LeaveRequestDto> ApproveAsync(int id, ApproveLeaveRequest request, CancellationToken cancellationToken)
    {
        var leave = await GetReviewableAsync(id, cancellationToken);

        var balance = await EnsureBalanceCoversAsync(
            leave.EmployeeId, leave.LeaveType, leave.StartDate.Year, leave.TotalDays, excludeRequestId: leave.Id, cancellationToken);

        balance.UsedDays += leave.TotalDays;
        MarkReviewed(leave, LeaveStatus.Approved, request.Comment);

        _auditTrail.Record(AuditActions.LeaveApproved, nameof(LeaveRequest), id, new { leave.EmployeeId, leave.TotalDays });
        _notifications.Notify(
            leave.Employee.UserId,
            NotificationType.LeaveApproved,
            "Leave approved",
            $"Your {leave.LeaveType.Name} from {leave.StartDate:d MMM} to {leave.EndDate:d MMM} was approved.",
            "/leave");

        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<LeaveRequestDto> RejectAsync(int id, RejectLeaveRequest request, CancellationToken cancellationToken)
    {
        var leave = await GetReviewableAsync(id, cancellationToken);

        MarkReviewed(leave, LeaveStatus.Rejected, request.Comment);

        _auditTrail.Record(AuditActions.LeaveRejected, nameof(LeaveRequest), id, new { leave.EmployeeId, reason = request.Comment });
        _notifications.Notify(
            leave.Employee.UserId,
            NotificationType.LeaveRejected,
            "Leave not approved",
            $"Your {leave.LeaveType.Name} from {leave.StartDate:d MMM} was not approved: {request.Comment.Trim()}",
            "/leave");

        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<LeaveRequestDto> CancelAsync(int id, CancellationToken cancellationToken)
    {
        var leave = await _db.LeaveRequests
            .Include(r => r.Employee)
            .Include(r => r.LeaveType)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(LeaveRequest), id);

        if (leave.EmployeeId != _currentUser.EmployeeId)
            throw new ForbiddenAccessException("You can only cancel your own leave requests.");

        if (!CanCancel(leave.EmployeeId, leave.Status, leave.StartDate))
        {
            throw new BusinessRuleException(leave.Status == LeaveStatus.Approved
                ? "Leave that has already started can't be cancelled. Please contact HR."
                : $"This request is already {leave.Status.ToString().ToLowerInvariant()}.");
        }

        var wasApproved = leave.Status == LeaveStatus.Approved;
        if (wasApproved)
        {
            var balance = await _db.LeaveBalances.FirstAsync(b =>
                b.EmployeeId == leave.EmployeeId && b.LeaveTypeId == leave.LeaveTypeId && b.Year == leave.StartDate.Year, cancellationToken);
            balance.UsedDays -= leave.TotalDays;
        }

        leave.Status = LeaveStatus.Cancelled;

        _auditTrail.Record(AuditActions.LeaveCancelled, nameof(LeaveRequest), id, new { wasApproved, leave.TotalDays });
        _notifications.Notify(
            await GetApproverUserIdsAsync(leave.Employee, cancellationToken),
            NotificationType.LeaveCancelled,
            "Leave cancelled",
            $"{leave.Employee.FullName} cancelled their {leave.LeaveType.Name} from {leave.StartDate:d MMM}.",
            "/leave?tab=team");

        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    private IQueryable<LeaveRequest> ApplyScope(IQueryable<LeaveRequest> requests, string scope)
    {
        var me = _currentUser.EmployeeId;
        var isAdmin = _currentUser.IsInRole(RoleNames.Admin);
        var isHr = _currentUser.IsInRole(RoleNames.HR);

        return scope switch
        {
            LeaveScopes.Approvals => requests.Where(r => r.Status == LeaveStatus.Pending
                && r.EmployeeId != me
                && (isAdmin || (me != null && r.Employee.ManagerId == me) || (isHr && r.Employee.ManagerId == null))),

            LeaveScopes.Team when isAdmin || isHr => requests,
            LeaveScopes.Team => requests.Where(r => me != null && r.Employee.ManagerId == me),

            _ => requests.Where(r => r.EmployeeId == me)
        };
    }

    /// <summary>
    /// The direct manager reviews; HR covers employees without a manager; Admin can step in for anyone.
    /// Nobody reviews their own request.
    /// </summary>
    private bool CanReview(int requestEmployeeId, int? employeeManagerId)
    {
        if (requestEmployeeId == _currentUser.EmployeeId)
            return false;

        return _currentUser.IsInRole(RoleNames.Admin)
            || (employeeManagerId != null && employeeManagerId == _currentUser.EmployeeId)
            || (employeeManagerId == null && _currentUser.IsInRole(RoleNames.HR));
    }

    private bool CanCancel(int requestEmployeeId, LeaveStatus status, DateOnly startDate) =>
        requestEmployeeId == _currentUser.EmployeeId
        && (status == LeaveStatus.Pending || (status == LeaveStatus.Approved && startDate > _companyTime.Today));

    private async Task<LeaveRequest> GetReviewableAsync(int id, CancellationToken cancellationToken)
    {
        var leave = await _db.LeaveRequests
            .Include(r => r.Employee)
            .Include(r => r.LeaveType)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(LeaveRequest), id);

        if (leave.EmployeeId == _currentUser.EmployeeId)
            throw new ForbiddenAccessException("You can't review your own leave request.");

        if (!CanReview(leave.EmployeeId, leave.Employee.ManagerId))
            throw new ForbiddenAccessException("Only the employee's manager (or HR, if they have no manager) can review this request.");

        if (leave.Status != LeaveStatus.Pending)
            throw new BusinessRuleException($"This request has already been {leave.Status.ToString().ToLowerInvariant()}.");

        return leave;
    }

    private async Task<LeaveBalance> EnsureBalanceCoversAsync(
        int employeeId, LeaveType leaveType, int year, decimal days, int? excludeRequestId, CancellationToken cancellationToken)
    {
        var balance = await _db.LeaveBalances.FirstOrDefaultAsync(b =>
            b.EmployeeId == employeeId && b.LeaveTypeId == leaveType.Id && b.Year == year, cancellationToken)
            ?? throw new BusinessRuleException($"There is no {leaveType.Name} balance for {year}. Please contact HR.");

        var pending = await GetPendingDaysAsync(employeeId, leaveType.Id, year, excludeRequestId, cancellationToken);
        var available = balance.AllocatedDays - balance.UsedDays - pending;

        if (days > available)
            throw new BusinessRuleException(
                $"Not enough {leaveType.Name}: {available:0.#} day(s) available (after pending requests), {days:0.#} requested.");

        return balance;
    }

    private async Task<decimal> GetPendingDaysAsync(
        int employeeId, int leaveTypeId, int year, int? excludeRequestId, CancellationToken cancellationToken)
    {
        var (yearStart, yearEnd) = YearRange(year);

        return await _db.LeaveRequests
            .Where(r => r.EmployeeId == employeeId
                && r.LeaveTypeId == leaveTypeId
                && r.Status == LeaveStatus.Pending
                && r.Id != excludeRequestId
                && r.StartDate >= yearStart && r.StartDate <= yearEnd)
            .SumAsync(r => (decimal?)r.TotalDays, cancellationToken) ?? 0;
    }

    private async Task<List<int?>> GetApproverUserIdsAsync(Employee employee, CancellationToken cancellationToken)
    {
        if (employee.ManagerId.HasValue)
            return [await _notifications.GetUserIdAsync(employee.ManagerId, cancellationToken)];

        return await _db.Users
            .Where(u => u.IsActive && u.Role.Name == RoleNames.HR && u.Id != employee.UserId)
            .Select(u => (int?)u.Id)
            .ToListAsync(cancellationToken);
    }

    private void MarkReviewed(LeaveRequest leave, LeaveStatus status, string? comment)
    {
        leave.Status = status;
        leave.ReviewedByUserId = _currentUser.UserId;
        leave.ReviewedAt = _companyTime.UtcNow;
        leave.ReviewComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
    }

    private async Task<LeaveRequestDto> GetAsync(int id, CancellationToken cancellationToken)
    {
        var row = await _db.LeaveRequests.AsNoTracking().Where(r => r.Id == id).Select(ToRow).FirstAsync(cancellationToken);
        return WithPermissions(row);
    }

    /// <summary>The employee's manager id travels with each row so review permissions can be worked out per request.</summary>
    private sealed record LeaveRow(LeaveRequestDto Request, int? EmployeeManagerId);

    private static readonly Expression<Func<LeaveRequest, LeaveRow>> ToRow = r => new LeaveRow(
        new LeaveRequestDto(
            r.Id,
            r.EmployeeId,
            r.Employee.FirstName + " " + r.Employee.LastName,
            r.Employee.EmployeeCode,
            r.LeaveTypeId,
            r.LeaveType.Name,
            r.StartDate,
            r.EndDate,
            r.TotalDays,
            r.Reason,
            r.Status,
            r.ReviewedBy != null ? r.ReviewedBy.FullName : null,
            r.ReviewedAt,
            r.ReviewComment,
            r.CreatedAt,
            false,
            false),
        r.Employee.ManagerId);

    private LeaveRequestDto WithPermissions(LeaveRow row) => row.Request with
    {
        CanReview = row.Request.Status == LeaveStatus.Pending && CanReview(row.Request.EmployeeId, row.EmployeeManagerId),
        CanCancel = CanCancel(row.Request.EmployeeId, row.Request.Status, row.Request.StartDate)
    };

    private int RequireEmployeeId() =>
        _currentUser.EmployeeId ?? throw new BusinessRuleException("Only accounts linked to an employee record can use leave.");

    private static (DateOnly Start, DateOnly End) YearRange(int year) => (new DateOnly(year, 1, 1), new DateOnly(year, 12, 31));
}
