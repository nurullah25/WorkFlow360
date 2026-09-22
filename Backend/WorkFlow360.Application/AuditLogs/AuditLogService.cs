using System.Reflection;
using Microsoft.EntityFrameworkCore;
using WorkFlow360.Application.Common;

namespace WorkFlow360.Application.AuditLogs;

public class AuditLogQuery : PagedQuery
{
    public string? Action { get; set; }
    public string? EntityType { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public record AuditLogDto(
    long Id,
    DateTime CreatedAt,
    int? UserId,
    string? UserName,
    string? UserEmail,
    string Action,
    string EntityType,
    int? EntityId,
    string? Details,
    string? IpAddress);

public class AuditLogService
{
    // The action names come from code, so the filter list doesn't need a DISTINCT over a table that only grows.
    private static readonly IReadOnlyList<string> KnownActions = typeof(AuditActions)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.IsLiteral)
        .Select(f => (string)f.GetRawConstantValue()!)
        .Order()
        .ToList();

    private readonly IAppDbContext _db;
    private readonly CompanyTime _companyTime;

    public AuditLogService(IAppDbContext db, CompanyTime companyTime)
    {
        _db = db;
        _companyTime = companyTime;
    }

    public IReadOnlyList<string> GetActions() => KnownActions;

    public async Task<PagedResult<AuditLogDto>> ListAsync(AuditLogQuery query, CancellationToken cancellationToken)
    {
        var logs = _db.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Action))
            logs = logs.Where(a => a.Action == query.Action);

        if (!string.IsNullOrWhiteSpace(query.EntityType))
            logs = logs.Where(a => a.EntityType == query.EntityType);

        // From/To are company-calendar dates; CreatedAt is UTC.
        if (query.From.HasValue)
        {
            var fromUtc = _companyTime.ToUtc(query.From.Value, TimeOnly.MinValue);
            logs = logs.Where(a => a.CreatedAt >= fromUtc);
        }
        if (query.To.HasValue)
        {
            var toUtc = _companyTime.ToUtc(query.To.Value.AddDays(1), TimeOnly.MinValue);
            logs = logs.Where(a => a.CreatedAt < toUtc);
        }

        if (query.SearchTerm is { } term)
        {
            logs = logs.Where(a => a.User != null && (a.User.FullName.Contains(term) || a.User.Email.Contains(term)));
        }

        return await logs
            .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id)
            .Select(a => new AuditLogDto(
                a.Id,
                a.CreatedAt,
                a.UserId,
                a.User != null ? a.User.FullName : null,
                a.User != null ? a.User.Email : null,
                a.Action,
                a.EntityType,
                a.EntityId,
                a.Details,
                a.IpAddress))
            .ToPagedResultAsync(query, cancellationToken);
    }
}
