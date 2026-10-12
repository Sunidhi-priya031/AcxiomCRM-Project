using AcxiomCRM.Data;
using AcxiomCRM.Infrastructure;
using AcxiomCRM.Models;

namespace AcxiomCRM.Services;

/// <summary>
/// Writes append-only audit entries. Callers pass entity snapshots or small dictionaries - never
/// passwords, hashes or tokens (AuditSnapshot strips such fields defensively as well).
/// </summary>
public class AuditService
{
    private readonly ApplicationDbContext _db;
    private readonly IUserContext _user;
    private readonly ILogger<AuditService> _logger;

    public AuditService(ApplicationDbContext db, IUserContext user, ILogger<AuditService> logger)
    {
        _db = db;
        _user = user;
        _logger = logger;
    }

    public async Task LogAsync(
        string action,
        string entityName,
        object? recordId,
        object? oldValue = null,
        object? newValue = null,
        bool success = true,
        string? details = null,
        string? userId = null,
        string? userName = null)
    {
        try
        {
            var entry = new AuditLog
            {
                UserId = userId ?? (string.IsNullOrEmpty(_user.UserId) ? null : _user.UserId),
                UserName = Truncate(userName ?? _user.UserName, 256),
                Action = action,
                EntityName = entityName,
                RecordId = recordId?.ToString(),
                OldValue = AuditSnapshot.ToJson(oldValue),
                NewValue = AuditSnapshot.ToJson(newValue),
                Result = success ? AuditResults.Success : AuditResults.Failure,
                Details = Truncate(details, 500),
                CreatedDate = DateTime.Now,
                IpAddress = Truncate(_user.IpAddress, 45)
            };
            _db.AuditLogs.Add(entry);
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Auditing must not break the business operation that already succeeded, but it must be visible to operators.
            _logger.LogError(ex, "Failed to write audit entry {Action} {Entity} {RecordId}", action, entityName, recordId);
        }
    }

    private static string? Truncate(string? value, int max) =>
        value == null || value.Length <= max ? value : value[..max];
}
