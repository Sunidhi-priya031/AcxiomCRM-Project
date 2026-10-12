using AcxiomCRM.Data;
using AcxiomCRM.Dtos;
using AcxiomCRM.Infrastructure;
using AcxiomCRM.Models;
using AcxiomCRM.Validation;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services;

public class FollowUpSearch
{
    /// <summary>"all", "pending", "upcoming", "overdue" or "today".</summary>
    public string? View { get; set; }
    public FollowUpStatus? Status { get; set; }
    public FollowUpType? Type { get; set; }
    public string? AssignedTo { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }

    /// <summary>Matches subject or the related customer / lead / opportunity name.</summary>
    public string? Related { get; set; }
    public int? CustomerId { get; set; }
    public int? LeadId { get; set; }
    public int? OpportunityId { get; set; }
    public string? Sort { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }
}

public class FollowUpService
{
    private readonly ApplicationDbContext _db;
    private readonly IUserContext _user;
    private readonly AuditService _audit;
    private readonly UserDirectoryService _users;
    private readonly CustomerService _customers;
    private readonly LeadService _leads;
    private readonly OpportunityService _opportunities;

    public FollowUpService(ApplicationDbContext db, IUserContext user, AuditService audit, UserDirectoryService users,
        CustomerService customers, LeadService leads, OpportunityService opportunities)
    {
        _db = db;
        _user = user;
        _audit = audit;
        _users = users;
        _customers = customers;
        _leads = leads;
        _opportunities = opportunities;
    }

    public async Task<IQueryable<FollowUp>> ScopedQueryAsync() =>
        _db.FollowUps.InScope(await _user.GetScopeUserIdsAsync());

    private static IQueryable<FollowUp> WithRelated(IQueryable<FollowUp> query) =>
        query.Include(f => f.Customer)
             .Include(f => f.Lead)
             .Include(f => f.Opportunity)
             .Include(f => f.AssignedToUser);

    public async Task<PagedResult<FollowUp>> SearchAsync(FollowUpSearch s, int upcomingDays = 7)
    {
        var today = DateTime.Today;
        var query = WithRelated(await ScopedQueryAsync()).AsNoTracking();

        switch (s.View)
        {
            case "pending":
                query = query.Where(f => f.Status == FollowUpStatus.Planned);
                break;
            case "today":
                var tomorrow = today.AddDays(1);
                query = query.Where(f => f.Status == FollowUpStatus.Planned && f.FollowUpDate >= today && f.FollowUpDate < tomorrow);
                break;
            case "upcoming":
                var limit = today.AddDays(upcomingDays + 1);
                query = query.Where(f => f.Status == FollowUpStatus.Planned && f.FollowUpDate >= today && f.FollowUpDate < limit);
                break;
            case "overdue":
                query = query.Where(f => f.Status == FollowUpStatus.Planned && f.FollowUpDate < today);
                break;
        }

        if (s.Status.HasValue) query = query.Where(f => f.Status == s.Status.Value);
        if (s.Type.HasValue) query = query.Where(f => f.FollowUpType == s.Type.Value);
        if (!string.IsNullOrWhiteSpace(s.AssignedTo)) query = query.Where(f => f.AssignedTo == s.AssignedTo);
        if (s.From.HasValue)
        {
            var from = s.From.Value.Date;
            query = query.Where(f => f.FollowUpDate >= from);
        }

        if (s.To.HasValue)
        {
            var to = s.To.Value.Date.AddDays(1);
            query = query.Where(f => f.FollowUpDate < to);
        }

        if (s.CustomerId.HasValue) query = query.Where(f => f.CustomerId == s.CustomerId);
        if (s.LeadId.HasValue) query = query.Where(f => f.LeadId == s.LeadId);
        if (s.OpportunityId.HasValue) query = query.Where(f => f.OpportunityId == s.OpportunityId);

        if (!string.IsNullOrWhiteSpace(s.Related))
        {
            var term = s.Related.Trim().ToLower();
            query = query.Where(f =>
                f.Subject.ToLower().Contains(term) ||
                (f.Customer != null && f.Customer.CustomerName.ToLower().Contains(term)) ||
                (f.Lead != null && f.Lead.LeadName.ToLower().Contains(term)) ||
                (f.Opportunity != null && f.Opportunity.OpportunityName.ToLower().Contains(term)));
        }

        query = s.Sort switch
        {
            "date_desc" => query.OrderByDescending(f => f.FollowUpDate),
            "subject" => query.OrderBy(f => f.Subject),
            "subject_desc" => query.OrderByDescending(f => f.Subject),
            "status" => query.OrderBy(f => f.Status),
            "status_desc" => query.OrderByDescending(f => f.Status),
            "type" => query.OrderBy(f => f.FollowUpType),
            "type_desc" => query.OrderByDescending(f => f.FollowUpType),
            _ => query.OrderBy(f => f.FollowUpDate)
        };

        return await query.ToPagedResultAsync(s.Page, s.PageSize);
    }

    public async Task<ServiceResult<FollowUp>> GetAsync(int id)
    {
        var followUp = await WithRelated(_db.FollowUps)
            .Include(f => f.CreatedByUser)
            .FirstOrDefaultAsync(f => f.FollowUpId == id);
        if (followUp == null)
        {
            return ServiceResult<FollowUp>.NotFound();
        }

        var scope = await _user.GetScopeUserIdsAsync();
        return ScopeExtensions.IsInScope(scope, followUp.AssignedTo, followUp.CreatedBy)
            ? ServiceResult<FollowUp>.Ok(followUp)
            : ServiceResult<FollowUp>.Forbidden("You do not have access to this follow-up.");
    }

    /// <summary>Planned follow-ups for one user, used by the header notifications.</summary>
    public async Task<(int OverdueCount, int TodayCount, List<FollowUp> Items)> GetNotificationsAsync(string userId, int take = 6)
    {
        var tomorrow = DateTime.Today.AddDays(1);
        var query = _db.FollowUps.AsNoTracking()
            .Where(f => f.AssignedTo == userId && f.Status == FollowUpStatus.Planned && f.FollowUpDate < tomorrow);
        var overdue = await query.CountAsync(f => f.FollowUpDate < DateTime.Today);
        var total = await query.CountAsync();
        var items = await WithRelated(query).OrderBy(f => f.FollowUpDate).Take(take).ToListAsync();
        return (overdue, total - overdue, items);
    }

    private async Task<ValidationErrors> ValidateRelationsAsync(FollowUpRequest r, FollowUp? existing)
    {
        var errors = new ValidationErrors();
        var linked = (r.CustomerId.HasValue ? 1 : 0) + (r.LeadId.HasValue ? 1 : 0) + (r.OpportunityId.HasValue ? 1 : 0);
        if (linked != 1)
        {
            errors.Add(string.Empty, "Link the follow-up to exactly one customer, lead or opportunity.");
            return errors;
        }

        // Unchanged links on an existing follow-up do not need re-checking.
        if (existing != null && existing.CustomerId == r.CustomerId && existing.LeadId == r.LeadId && existing.OpportunityId == r.OpportunityId)
        {
            return errors;
        }

        if (r.CustomerId.HasValue)
        {
            var customer = await _customers.GetAsync(r.CustomerId.Value);
            if (!customer.Succeeded) errors.Add(nameof(FollowUpRequest.CustomerId), "Select a valid customer you have access to.");
            else if (customer.Value!.Status != CustomerStatus.Active) errors.Add(nameof(FollowUpRequest.CustomerId), "The selected customer is inactive.");
        }

        if (r.LeadId.HasValue)
        {
            var lead = await _leads.GetAsync(r.LeadId.Value);
            if (!lead.Succeeded) errors.Add(nameof(FollowUpRequest.LeadId), "Select a valid lead you have access to.");
            else if (LeadWorkflow.IsFinal(lead.Value!.Status)) errors.Add(nameof(FollowUpRequest.LeadId), "Follow-ups cannot be scheduled for converted or lost leads.");
        }

        if (r.OpportunityId.HasValue)
        {
            var opportunity = await _opportunities.GetAsync(r.OpportunityId.Value);
            if (!opportunity.Succeeded) errors.Add(nameof(FollowUpRequest.OpportunityId), "Select a valid opportunity you have access to.");
            else if (!opportunity.Value!.IsOpen) errors.Add(nameof(FollowUpRequest.OpportunityId), "Follow-ups cannot be scheduled for closed opportunities.");
        }

        return errors;
    }

    /// <summary>Follow-up workflow steps 1-3: create, set date, assign user. New follow-ups are always Planned.</summary>
    public async Task<ServiceResult<FollowUp>> CreateAsync(FollowUpRequest request)
    {
        var errors = await ValidateRelationsAsync(request, null);
        if (request.FollowUpDate.HasValue && request.FollowUpDate.Value.Date < DateTime.Today)
        {
            errors.Add(nameof(FollowUpRequest.FollowUpDate), ValidationRules.FollowUpDateMessage);
        }

        var (assignee, assignError) = await _users.ResolveAssigneeAsync(request.AssignedTo);
        if (assignError != null)
        {
            errors.Add(nameof(FollowUpRequest.AssignedTo), assignError);
        }

        if (errors.Any)
        {
            return ServiceResult<FollowUp>.Invalid(errors.Errors);
        }

        var followUp = new FollowUp
        {
            Status = FollowUpStatus.Planned,
            AssignedTo = assignee!,
            CreatedBy = _user.UserId,
            CreatedDate = DateTime.Now
        };
        Apply(followUp, request);
        _db.FollowUps.Add(followUp);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(AuditActions.Create, AuditEntities.FollowUp, followUp.FollowUpId, null, AuditSnapshot.Of(followUp));
        return ServiceResult<FollowUp>.Ok(followUp);
    }

    /// <summary>Edits a planned follow-up. A changed date counts as a reschedule.</summary>
    public async Task<ServiceResult<FollowUp>> UpdateAsync(int id, FollowUpRequest request)
    {
        var found = await GetAsync(id);
        if (!found.Succeeded)
        {
            return found;
        }

        var followUp = found.Value!;
        if (followUp.Status != FollowUpStatus.Planned)
        {
            return ServiceResult<FollowUp>.Invalid(string.Empty, $"Only planned follow-ups can be edited. This one is {followUp.Status}.");
        }

        var errors = await ValidateRelationsAsync(request, followUp);
        if (request.FollowUpDate.HasValue && request.FollowUpDate.Value.Date < DateTime.Today)
        {
            errors.Add(nameof(FollowUpRequest.FollowUpDate), ValidationRules.FollowUpDateMessage);
        }

        var (assignee, assignError) = await _users.ResolveAssigneeAsync(request.AssignedTo, followUp.AssignedTo);
        if (assignError != null)
        {
            errors.Add(nameof(FollowUpRequest.AssignedTo), assignError);
        }

        if (errors.Any)
        {
            return ServiceResult<FollowUp>.Invalid(errors.Errors);
        }

        var before = AuditSnapshot.Of(followUp);
        Apply(followUp, request);
        followUp.AssignedTo = assignee!;
        followUp.ModifiedDate = DateTime.Now;
        await _db.SaveChangesAsync();

        var (oldValues, newValues) = AuditSnapshot.Diff(before, AuditSnapshot.Of(followUp));
        if (newValues.Count > 0)
        {
            var action = oldValues.ContainsKey(nameof(FollowUp.FollowUpDate)) ? AuditActions.Reschedule : AuditActions.Update;
            await _audit.LogAsync(action, AuditEntities.FollowUp, id, oldValues, newValues);
        }

        return ServiceResult<FollowUp>.Ok(followUp);
    }

    public async Task<ServiceResult<FollowUp>> RescheduleAsync(int id, FollowUpRescheduleRequest request)
    {
        var found = await GetAsync(id);
        if (!found.Succeeded)
        {
            return found;
        }

        var followUp = found.Value!;
        if (followUp.Status is not (FollowUpStatus.Planned or FollowUpStatus.Missed))
        {
            return ServiceResult<FollowUp>.Invalid(string.Empty, "Only planned or missed follow-ups can be rescheduled.");
        }

        if (!request.NewDate.HasValue || request.NewDate.Value.Date < DateTime.Today)
        {
            return ServiceResult<FollowUp>.Invalid(nameof(FollowUpRescheduleRequest.NewDate), ValidationRules.FollowUpDateMessage);
        }

        var oldValues = new { FollowUpDate = followUp.FollowUpDate, Status = followUp.Status.ToString() };
        followUp.FollowUpDate = request.NewDate.Value;
        followUp.Status = FollowUpStatus.Planned;
        followUp.ModifiedDate = DateTime.Now;
        if (!string.IsNullOrWhiteSpace(request.Reason))
        {
            AppendRemark(followUp, $"Rescheduled: {request.Reason.Trim()}");
        }

        await _db.SaveChangesAsync();
        await _audit.LogAsync(AuditActions.Reschedule, AuditEntities.FollowUp, id, oldValues,
            new { FollowUpDate = followUp.FollowUpDate, Status = followUp.Status.ToString() }, details: CustomerService.Clean(request.Reason));
        return ServiceResult<FollowUp>.Ok(followUp);
    }

    /// <summary>
    /// Follow-up workflow steps 4-6: complete the follow-up, update the related CRM record (an activity is
    /// logged and a New lead moves to Contacted) and audit the action.
    /// </summary>
    public async Task<ServiceResult<FollowUp>> CompleteAsync(int id, FollowUpCompleteRequest request)
    {
        var found = await GetAsync(id);
        if (!found.Succeeded)
        {
            return found;
        }

        var followUp = found.Value!;
        if (followUp.Status != FollowUpStatus.Planned)
        {
            return ServiceResult<FollowUp>.Invalid(string.Empty, $"Only planned follow-ups can be completed. This one is {followUp.Status}.");
        }

        var before = new { Status = followUp.Status.ToString() };
        followUp.Status = FollowUpStatus.Completed;
        followUp.CompletedDate = DateTime.Now;
        followUp.ModifiedDate = DateTime.Now;
        if (!string.IsNullOrWhiteSpace(request.Outcome))
        {
            AppendRemark(followUp, $"Outcome: {request.Outcome.Trim()}");
        }

        // Record the interaction on the related customer or lead.
        var customerId = followUp.CustomerId ?? followUp.Opportunity?.CustomerId;
        var activity = new Activity
        {
            ActivityType = followUp.FollowUpType switch
            {
                FollowUpType.Call => ActivityType.Call,
                FollowUpType.Email => ActivityType.Email,
                _ => ActivityType.Meeting
            },
            Subject = followUp.Subject,
            Description = CustomerService.Clean(request.Outcome) ?? "Follow-up completed.",
            ActivityDate = DateTime.Now,
            CustomerId = followUp.LeadId.HasValue ? null : customerId,
            LeadId = followUp.LeadId,
            AssignedTo = followUp.AssignedTo,
            Status = ActivityStatus.Completed,
            CreatedBy = _user.UserId,
            CreatedDate = DateTime.Now
        };
        _db.Activities.Add(activity);

        string? leadChange = null;
        if (followUp.Lead != null && followUp.Lead.Status == LeadStatus.New)
        {
            followUp.Lead.Status = LeadStatus.Contacted;
            followUp.Lead.ModifiedDate = DateTime.Now;
            leadChange = followUp.Lead.LeadCode;
        }

        await _db.SaveChangesAsync();

        await _audit.LogAsync(AuditActions.Complete, AuditEntities.FollowUp, id, before,
            new { Status = nameof(FollowUpStatus.Completed), followUp.CompletedDate, ActivityId = activity.ActivityId },
            details: CustomerService.Clean(request.Outcome));
        if (leadChange != null)
        {
            await _audit.LogAsync(AuditActions.StatusChange, AuditEntities.Lead, followUp.LeadId,
                new { Status = nameof(LeadStatus.New) }, new { Status = nameof(LeadStatus.Contacted) },
                details: "Lead contacted via completed follow-up.");
        }

        return ServiceResult<FollowUp>.Ok(followUp);
    }

    /// <summary>Marks a planned follow-up as Missed or Cancelled.</summary>
    public async Task<ServiceResult<FollowUp>> SetStatusAsync(int id, FollowUpStatus status, string? reason)
    {
        if (status is not (FollowUpStatus.Missed or FollowUpStatus.Cancelled))
        {
            return ServiceResult<FollowUp>.Invalid(string.Empty, "Invalid status change.");
        }

        var found = await GetAsync(id);
        if (!found.Succeeded)
        {
            return found;
        }

        var followUp = found.Value!;
        if (followUp.Status != FollowUpStatus.Planned)
        {
            return ServiceResult<FollowUp>.Invalid(string.Empty, $"Only planned follow-ups can be marked {status}.");
        }

        var before = followUp.Status.ToString();
        followUp.Status = status;
        followUp.ModifiedDate = DateTime.Now;
        if (!string.IsNullOrWhiteSpace(reason))
        {
            AppendRemark(followUp, $"{status}: {reason.Trim()}");
        }

        await _db.SaveChangesAsync();
        await _audit.LogAsync(AuditActions.StatusChange, AuditEntities.FollowUp, id,
            new { Status = before }, new { Status = status.ToString() }, details: CustomerService.Clean(reason));
        return ServiceResult<FollowUp>.Ok(followUp);
    }

    private static void Apply(FollowUp f, FollowUpRequest r)
    {
        f.Subject = r.Subject!.Trim();
        f.FollowUpType = r.FollowUpType!.Value;
        f.FollowUpDate = r.FollowUpDate!.Value;
        f.Remarks = CustomerService.Clean(r.Remarks);
        f.CustomerId = r.CustomerId;
        f.LeadId = r.LeadId;
        f.OpportunityId = r.OpportunityId;
    }

    private static void AppendRemark(FollowUp f, string text)
    {
        var line = $"[{DateTime.Now:dd MMM yyyy HH:mm}] {text}";
        f.Remarks = string.IsNullOrEmpty(f.Remarks) ? line : $"{f.Remarks}\n{line}";
        if (f.Remarks.Length > ValidationRules.NotesLength)
        {
            f.Remarks = f.Remarks[^ValidationRules.NotesLength..];
        }
    }
}
