using AcxiomCRM.Data;
using AcxiomCRM.Dtos;
using AcxiomCRM.Infrastructure;
using AcxiomCRM.Models;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services;

public class ActivitySearch
{
    public ActivityType? Type { get; set; }
    public ActivityStatus? Status { get; set; }
    public string? AssignedTo { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }

    /// <summary>Matches subject or related customer / lead name.</summary>
    public string? Q { get; set; }
    public int? CustomerId { get; set; }
    public int? LeadId { get; set; }
    public string? Sort { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }
}

public class ActivityService
{
    private readonly ApplicationDbContext _db;
    private readonly IUserContext _user;
    private readonly AuditService _audit;
    private readonly UserDirectoryService _users;
    private readonly CustomerService _customers;
    private readonly LeadService _leads;

    public ActivityService(ApplicationDbContext db, IUserContext user, AuditService audit, UserDirectoryService users,
        CustomerService customers, LeadService leads)
    {
        _db = db;
        _user = user;
        _audit = audit;
        _users = users;
        _customers = customers;
        _leads = leads;
    }

    public async Task<IQueryable<Activity>> ScopedQueryAsync() =>
        _db.Activities.InScope(await _user.GetScopeUserIdsAsync());

    public async Task<PagedResult<Activity>> SearchAsync(ActivitySearch s)
    {
        var query = (await ScopedQueryAsync())
            .Include(a => a.Customer)
            .Include(a => a.Lead)
            .Include(a => a.AssignedToUser)
            .AsNoTracking();

        if (s.Type.HasValue) query = query.Where(a => a.ActivityType == s.Type.Value);
        if (s.Status.HasValue) query = query.Where(a => a.Status == s.Status.Value);
        if (!string.IsNullOrWhiteSpace(s.AssignedTo)) query = query.Where(a => a.AssignedTo == s.AssignedTo);
        if (s.CustomerId.HasValue) query = query.Where(a => a.CustomerId == s.CustomerId);
        if (s.LeadId.HasValue) query = query.Where(a => a.LeadId == s.LeadId);
        if (s.From.HasValue)
        {
            var from = s.From.Value.Date;
            query = query.Where(a => a.ActivityDate >= from);
        }

        if (s.To.HasValue)
        {
            var to = s.To.Value.Date.AddDays(1);
            query = query.Where(a => a.ActivityDate < to);
        }

        if (!string.IsNullOrWhiteSpace(s.Q))
        {
            var term = s.Q.Trim().ToLower();
            query = query.Where(a =>
                a.Subject.ToLower().Contains(term) ||
                (a.Customer != null && a.Customer.CustomerName.ToLower().Contains(term)) ||
                (a.Lead != null && a.Lead.LeadName.ToLower().Contains(term)));
        }

        query = s.Sort switch
        {
            "date" => query.OrderBy(a => a.ActivityDate),
            "type" => query.OrderBy(a => a.ActivityType),
            "type_desc" => query.OrderByDescending(a => a.ActivityType),
            "subject" => query.OrderBy(a => a.Subject),
            "subject_desc" => query.OrderByDescending(a => a.Subject),
            "status" => query.OrderBy(a => a.Status),
            "status_desc" => query.OrderByDescending(a => a.Status),
            _ => query.OrderByDescending(a => a.ActivityDate)
        };

        return await query.ToPagedResultAsync(s.Page, s.PageSize);
    }

    public async Task<ServiceResult<Activity>> GetAsync(int id)
    {
        var activity = await _db.Activities
            .Include(a => a.Customer)
            .Include(a => a.Lead)
            .Include(a => a.AssignedToUser)
            .Include(a => a.CreatedByUser)
            .FirstOrDefaultAsync(a => a.ActivityId == id);
        if (activity == null)
        {
            return ServiceResult<Activity>.NotFound();
        }

        var scope = await _user.GetScopeUserIdsAsync();
        return ScopeExtensions.IsInScope(scope, activity.AssignedTo, activity.CreatedBy)
            ? ServiceResult<Activity>.Ok(activity)
            : ServiceResult<Activity>.Forbidden("You do not have access to this activity.");
    }

    private async Task<ValidationErrors> ValidateAsync(ActivityRequest r, Activity? existing)
    {
        var errors = new ValidationErrors();
        var linked = (r.CustomerId.HasValue ? 1 : 0) + (r.LeadId.HasValue ? 1 : 0);
        if (linked != 1)
        {
            errors.Add(string.Empty, "Link the activity to exactly one customer or lead.");
            return errors;
        }

        if (existing != null && existing.CustomerId == r.CustomerId && existing.LeadId == r.LeadId)
        {
            return errors;
        }

        if (r.CustomerId.HasValue && !(await _customers.GetAsync(r.CustomerId.Value)).Succeeded)
        {
            errors.Add(nameof(ActivityRequest.CustomerId), "Select a valid customer you have access to.");
        }

        if (r.LeadId.HasValue && !(await _leads.GetAsync(r.LeadId.Value)).Succeeded)
        {
            errors.Add(nameof(ActivityRequest.LeadId), "Select a valid lead you have access to.");
        }

        return errors;
    }

    public async Task<ServiceResult<Activity>> CreateAsync(ActivityRequest request)
    {
        var errors = await ValidateAsync(request, null);
        var (assignee, assignError) = await _users.ResolveAssigneeAsync(request.AssignedTo);
        if (assignError != null) errors.Add(nameof(ActivityRequest.AssignedTo), assignError);
        if (errors.Any) return ServiceResult<Activity>.Invalid(errors.Errors);

        var activity = new Activity { AssignedTo = assignee!, CreatedBy = _user.UserId, CreatedDate = DateTime.Now };
        Apply(activity, request);
        _db.Activities.Add(activity);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(AuditActions.Create, AuditEntities.Activity, activity.ActivityId, null, AuditSnapshot.Of(activity));
        return ServiceResult<Activity>.Ok(activity);
    }

    public async Task<ServiceResult<Activity>> UpdateAsync(int id, ActivityRequest request)
    {
        var found = await GetAsync(id);
        if (!found.Succeeded) return found;

        var activity = found.Value!;
        var errors = await ValidateAsync(request, activity);
        var (assignee, assignError) = await _users.ResolveAssigneeAsync(request.AssignedTo, activity.AssignedTo);
        if (assignError != null) errors.Add(nameof(ActivityRequest.AssignedTo), assignError);
        if (errors.Any) return ServiceResult<Activity>.Invalid(errors.Errors);

        var before = AuditSnapshot.Of(activity);
        Apply(activity, request);
        activity.AssignedTo = assignee!;
        activity.ModifiedDate = DateTime.Now;
        await _db.SaveChangesAsync();

        var (oldValues, newValues) = AuditSnapshot.Diff(before, AuditSnapshot.Of(activity));
        if (newValues.Count > 0)
        {
            var action = oldValues.ContainsKey(nameof(Activity.Status)) ? AuditActions.StatusChange : AuditActions.Update;
            await _audit.LogAsync(action, AuditEntities.Activity, id, oldValues, newValues);
        }

        return ServiceResult<Activity>.Ok(activity);
    }

    public async Task<ServiceResult> DeleteAsync(int id)
    {
        if (_user.IsSalesExecutive)
        {
            return ServiceResult.Forbidden("Only Admins and Managers can delete activities.");
        }

        var found = await GetAsync(id);
        if (!found.Succeeded) return found;

        var snapshot = AuditSnapshot.Of(found.Value!);
        _db.Activities.Remove(found.Value!);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(AuditActions.Delete, AuditEntities.Activity, id, snapshot, null);
        return ServiceResult.Ok();
    }

    private static void Apply(Activity a, ActivityRequest r)
    {
        a.ActivityType = r.ActivityType!.Value;
        a.Subject = r.Subject!.Trim();
        a.Description = CustomerService.Clean(r.Description);
        a.ActivityDate = r.ActivityDate!.Value;
        a.CustomerId = r.CustomerId;
        a.LeadId = r.LeadId;
        a.Status = r.Status!.Value;
    }
}
