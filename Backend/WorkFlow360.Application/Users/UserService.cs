using Microsoft.EntityFrameworkCore;
using WorkFlow360.Application.AuditLogs;
using WorkFlow360.Application.Auth;
using WorkFlow360.Application.Common;
using WorkFlow360.Application.Common.Exceptions;
using WorkFlow360.Domain.Entities;

namespace WorkFlow360.Application.Users;

public class UserService
{
    private readonly IAppDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly AuditTrail _auditTrail;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _clock;

    public UserService(
        IAppDbContext db,
        IPasswordHasher passwordHasher,
        AuditTrail auditTrail,
        ICurrentUser currentUser,
        TimeProvider clock)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _auditTrail = auditTrail;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<PagedResult<UserListItemDto>> ListAsync(UserQuery query, CancellationToken cancellationToken)
    {
        var users = _db.Users.AsNoTracking();

        if (query.SearchTerm is { } term)
            users = users.Where(u => u.FullName.Contains(term) || u.Email.Contains(term));

        if (!string.IsNullOrWhiteSpace(query.Role))
            users = users.Where(u => u.Role.Name == query.Role);

        if (query.IsActive.HasValue)
            users = users.Where(u => u.IsActive == query.IsActive);

        users = query.SortBy?.ToLowerInvariant() switch
        {
            "email" => users.OrderByDirection(u => u.Email, query.Descending),
            "role" => users.OrderByDirection(u => u.Role.Name, query.Descending).ThenBy(u => u.FullName),
            "lastloginat" => users.OrderByDirection(u => u.LastLoginAt, query.Descending),
            _ => users.OrderByDirection(u => u.FullName, query.Descending)
        };

        return await users
            .Select(u => new UserListItemDto(
                u.Id,
                u.Email,
                u.FullName,
                u.Role.Name,
                u.IsActive,
                u.LastLoginAt,
                u.Employee != null ? u.Employee.Id : null,
                u.Employee != null ? u.Employee.EmployeeCode : null))
            .ToPagedResultAsync(query, cancellationToken);
    }

    public async Task<UserListItemDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        if (await _db.Users.AnyAsync(u => u.Email == email, cancellationToken))
            throw new ConflictException($"A user with email {email} already exists.");

        var user = new User
        {
            Email = email,
            FullName = request.FullName.Trim(),
            PasswordHash = _passwordHasher.Hash(request.Password),
            RoleId = await GetRoleIdAsync(request.Role, cancellationToken)
        };
        _db.Users.Add(user);

        if (request.EmployeeId.HasValue)
        {
            var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == request.EmployeeId, cancellationToken)
                ?? throw new NotFoundException(nameof(Employee), request.EmployeeId);

            if (employee.UserId != null)
                throw new ConflictException($"{employee.FullName} already has a user account.");
            if (!employee.IsCurrent)
                throw new BusinessRuleException($"{employee.FullName} is no longer employed.");

            employee.User = user;
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        _auditTrail.Record(AuditActions.UserCreated, nameof(User), user.Id, new { user.Email, request.Role, request.EmployeeId });
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetAsync(user.Id, cancellationToken);
    }

    public async Task<UserListItemDto> UpdateAsync(int id, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(User), id);

        var losesAdminAccess = user.Role.Name == RoleNames.Admin && user.IsActive
            && (request.Role != RoleNames.Admin || !request.IsActive);

        if (losesAdminAccess)
        {
            if (id == _currentUser.UserId)
                throw new BusinessRuleException("You cannot remove your own admin access. Ask another administrator.");

            var otherActiveAdmins = await _db.Users.CountAsync(
                u => u.Id != id && u.IsActive && u.Role.Name == RoleNames.Admin, cancellationToken);
            if (otherActiveAdmins == 0)
                throw new BusinessRuleException("At least one active administrator is required.");
        }

        if (id == _currentUser.UserId && !request.IsActive)
            throw new BusinessRuleException("You cannot deactivate your own account.");

        var changes = new Dictionary<string, object?>();
        if (user.Role.Name != request.Role)
        {
            changes["Role"] = new { From = user.Role.Name, To = request.Role };
            user.RoleId = await GetRoleIdAsync(request.Role, cancellationToken);
        }

        if (user.IsActive != request.IsActive)
        {
            changes["IsActive"] = request.IsActive;
            user.IsActive = request.IsActive;

            if (!request.IsActive)
                await _db.RevokeAllSessionsAsync(user.Id, _clock.GetUtcNow().UtcDateTime, cancellationToken);
        }

        user.FullName = request.FullName.Trim();

        _auditTrail.Record(AuditActions.UserUpdated, nameof(User), id, changes);
        await _db.SaveChangesAsync(cancellationToken);

        return await GetAsync(id, cancellationToken);
    }

    public async Task ResetPasswordAsync(int id, ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(User), id);

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);

        // Anyone signed in with the old password is signed out.
        await _db.RevokeAllSessionsAsync(user.Id, _clock.GetUtcNow().UtcDateTime, cancellationToken);

        _auditTrail.Record(AuditActions.PasswordReset, nameof(User), id);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<int> GetRoleIdAsync(string roleName, CancellationToken cancellationToken) =>
        await _db.Roles.Where(r => r.Name == roleName).Select(r => (int?)r.Id).FirstOrDefaultAsync(cancellationToken)
            ?? throw new BusinessRuleException($"Unknown role '{roleName}'.");

    private async Task<UserListItemDto> GetAsync(int id, CancellationToken cancellationToken) =>
        await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new UserListItemDto(
                u.Id,
                u.Email,
                u.FullName,
                u.Role.Name,
                u.IsActive,
                u.LastLoginAt,
                u.Employee != null ? u.Employee.Id : null,
                u.Employee != null ? u.Employee.EmployeeCode : null))
            .FirstAsync(cancellationToken);
}
