using AcxiomCRM.Data;
using AcxiomCRM.Dtos;
using AcxiomCRM.Infrastructure;
using AcxiomCRM.Models;
using AcxiomCRM.Validation;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services;

public class OpportunitySearch
{
    /// <summary>Matches opportunity name or customer name.</summary>
    public string? Q { get; set; }
    public OpportunityStage? Stage { get; set; }
    public OpportunityStatus? Status { get; set; }
    public int? CustomerId { get; set; }
    public string? AssignedTo { get; set; }
    public string? Sort { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }
}

public class OpportunityService
{
    private readonly ApplicationDbContext _db;
    private readonly IUserContext _user;
    private readonly AuditService _audit;
    private readonly UserDirectoryService _users;
    private readonly CustomerService _customers;
    private readonly LeadService _leads;

    public OpportunityService(ApplicationDbContext db, IUserContext user, AuditService audit, UserDirectoryService users,
        CustomerService customers, LeadService leads)
    {
        _db = db;
        _user = user;
        _audit = audit;
        _users = users;
        _customers = customers;
        _leads = leads;
    }

    public async Task<IQueryable<Opportunity>> ScopedQueryAsync() =>
        _db.Opportunities.InScope(await _user.GetScopeUserIdsAsync());

    public async Task<PagedResult<Opportunity>> SearchAsync(OpportunitySearch s)
    {
        var query = (await ScopedQueryAsync())
            .Include(o => o.Customer)
            .Include(o => o.AssignedToUser)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(s.Q))
        {
            var term = s.Q.Trim().ToLower();
            query = query.Where(o =>
                o.OpportunityName.ToLower().Contains(term) ||
                (o.Customer != null && o.Customer.CustomerName.ToLower().Contains(term)));
        }

        if (s.Stage.HasValue) query = query.Where(o => o.Stage == s.Stage.Value);
        if (s.Status.HasValue) query = query.Where(o => o.Status == s.Status.Value);
        if (s.CustomerId.HasValue) query = query.Where(o => o.CustomerId == s.CustomerId.Value);
        if (!string.IsNullOrWhiteSpace(s.AssignedTo)) query = query.Where(o => o.AssignedTo == s.AssignedTo);

        query = s.Sort switch
        {
            "name" => query.OrderBy(o => o.OpportunityName),
            "name_desc" => query.OrderByDescending(o => o.OpportunityName),
            "amount" => query.OrderBy(o => o.Amount),
            "amount_desc" => query.OrderByDescending(o => o.Amount),
            "close" => query.OrderBy(o => o.ExpectedCloseDate),
            "close_desc" => query.OrderByDescending(o => o.ExpectedCloseDate),
            "stage" => query.OrderBy(o => o.Stage),
            "stage_desc" => query.OrderByDescending(o => o.Stage),
            "probability" => query.OrderBy(o => o.Probability),
            "probability_desc" => query.OrderByDescending(o => o.Probability),
            "created" => query.OrderBy(o => o.CreatedDate),
            _ => query.OrderByDescending(o => o.CreatedDate)
        };

        return await query.ToPagedResultAsync(s.Page, s.PageSize);
    }

    public async Task<ServiceResult<Opportunity>> GetAsync(int id)
    {
        var opportunity = await _db.Opportunities
            .Include(o => o.Customer)
            .Include(o => o.Lead)
            .Include(o => o.AssignedToUser)
            .Include(o => o.CreatedByUser)
            .FirstOrDefaultAsync(o => o.OpportunityId == id);
        if (opportunity == null)
        {
            return ServiceResult<Opportunity>.NotFound();
        }

        var scope = await _user.GetScopeUserIdsAsync();
        return ScopeExtensions.IsInScope(scope, opportunity.AssignedTo, opportunity.CreatedBy)
            ? ServiceResult<Opportunity>.Ok(opportunity)
            : ServiceResult<Opportunity>.Forbidden("You do not have access to this opportunity.");
    }

    public async Task<List<Opportunity>> GetOpenLookupAsync()
    {
        return await (await ScopedQueryAsync())
            .Include(o => o.Customer)
            .AsNoTracking()
            .Where(o => o.Status == OpportunityStatus.Open)
            .OrderBy(o => o.OpportunityName)
            .ToListAsync();
    }

    /// <summary>Open opportunities grouped by stage for the pipeline board.</summary>
    public async Task<List<Opportunity>> GetPipelineAsync(string? assignedTo)
    {
        var query = (await ScopedQueryAsync())
            .Include(o => o.Customer)
            .Include(o => o.AssignedToUser)
            .AsNoTracking()
            .Where(o => o.Status == OpportunityStatus.Open);
        if (!string.IsNullOrWhiteSpace(assignedTo))
        {
            query = query.Where(o => o.AssignedTo == assignedTo);
        }

        return await query.OrderBy(o => o.ExpectedCloseDate).ToListAsync();
    }

    /// <summary>Business rules shared by create and update (repeats the attribute rules so service callers are always covered).</summary>
    private async Task<ValidationErrors> ValidateAsync(OpportunityRequest r, Opportunity? existing)
    {
        var errors = new ValidationErrors();
        var stage = r.Stage!.Value;
        var active = !Opportunity.IsClosedStage(stage);

        if (r.Amount is null or < 0 || (active && r.Amount <= 0))
        {
            errors.Add(nameof(OpportunityRequest.Amount), ValidationRules.AmountMessage);
        }

        if (r.Probability is null or < 0 or > 100)
        {
            errors.Add(nameof(OpportunityRequest.Probability), ValidationRules.ProbabilityMessage);
        }

        if (active && r.ExpectedCloseDate.HasValue && r.ExpectedCloseDate.Value.Date < DateTime.Today)
        {
            errors.Add(nameof(OpportunityRequest.ExpectedCloseDate), ValidationRules.CloseDateMessage);
        }

        if (existing == null && !active)
        {
            errors.Add(nameof(OpportunityRequest.Stage), "A new opportunity must start in an open pipeline stage.");
        }

        if (existing != null && !OpportunityWorkflow.CanTransition(existing.Stage, stage))
        {
            errors.Add(nameof(OpportunityRequest.Stage), $"This opportunity is already {existing.Stage} and cannot be moved to {stage}.");
        }

        // Referenced records must exist and be within the caller's scope.
        if (r.CustomerId.HasValue && (existing == null || existing.CustomerId != r.CustomerId))
        {
            var customer = await _customers.GetAsync(r.CustomerId.Value);
            if (!customer.Succeeded)
            {
                errors.Add(nameof(OpportunityRequest.CustomerId), "Select a valid customer you have access to.");
            }
            else if (customer.Value!.Status != CustomerStatus.Active)
            {
                errors.Add(nameof(OpportunityRequest.CustomerId), "Opportunities cannot be added to an inactive customer.");
            }
        }

        if (r.LeadId.HasValue && (existing == null || existing.LeadId != r.LeadId))
        {
            var lead = await _leads.GetAsync(r.LeadId.Value);
            if (!lead.Succeeded)
            {
                errors.Add(nameof(OpportunityRequest.LeadId), "Select a valid lead you have access to.");
            }
        }

        return errors;
    }

    public async Task<ServiceResult<Opportunity>> CreateAsync(OpportunityRequest request)
    {
        var errors = await ValidateAsync(request, null);
        var (assignee, assignError) = await _users.ResolveAssigneeAsync(request.AssignedTo);
        if (assignError != null)
        {
            errors.Add(nameof(OpportunityRequest.AssignedTo), assignError);
        }

        if (errors.Any)
        {
            return ServiceResult<Opportunity>.Invalid(errors.Errors);
        }

        var opportunity = new Opportunity
        {
            CreatedBy = _user.UserId,
            CreatedDate = DateTime.Now,
            AssignedTo = assignee!
        };
        Apply(opportunity, request);

        _db.Opportunities.Add(opportunity);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(AuditActions.Create, AuditEntities.Opportunity, opportunity.OpportunityId, null, AuditSnapshot.Of(opportunity));
        return ServiceResult<Opportunity>.Ok(opportunity);
    }

    public async Task<ServiceResult<Opportunity>> UpdateAsync(int id, OpportunityRequest request)
    {
        var found = await GetAsync(id);
        if (!found.Succeeded)
        {
            return found;
        }

        var opportunity = found.Value!;
        if (!opportunity.IsOpen)
        {
            return ServiceResult<Opportunity>.Invalid(string.Empty, $"This opportunity is closed ({opportunity.Status}) and can no longer be edited.");
        }

        var errors = await ValidateAsync(request, opportunity);
        var (assignee, assignError) = await _users.ResolveAssigneeAsync(request.AssignedTo, opportunity.AssignedTo);
        if (assignError != null)
        {
            errors.Add(nameof(OpportunityRequest.AssignedTo), assignError);
        }

        if (errors.Any)
        {
            return ServiceResult<Opportunity>.Invalid(errors.Errors);
        }

        var before = AuditSnapshot.Of(opportunity);
        Apply(opportunity, request);
        opportunity.AssignedTo = assignee!;
        opportunity.ModifiedDate = DateTime.Now;
        await _db.SaveChangesAsync();

        var (oldValues, newValues) = AuditSnapshot.Diff(before, AuditSnapshot.Of(opportunity));
        if (newValues.Count > 0)
        {
            var action = oldValues.ContainsKey(nameof(Opportunity.Stage)) ? AuditActions.StatusChange : AuditActions.Update;
            await _audit.LogAsync(action, AuditEntities.Opportunity, opportunity.OpportunityId, oldValues, newValues);
        }

        return ServiceResult<Opportunity>.Ok(opportunity);
    }

    /// <summary>Opportunity workflow steps 6-7: mark Won or Lost and capture the final outcome.</summary>
    public async Task<ServiceResult<Opportunity>> CloseAsync(int id, OpportunityStage outcome, string? remarks)
    {
        if (!Opportunity.IsClosedStage(outcome))
        {
            return ServiceResult<Opportunity>.Invalid(string.Empty, "Outcome must be Won or Lost.");
        }

        var found = await GetAsync(id);
        if (!found.Succeeded)
        {
            return found;
        }

        var opportunity = found.Value!;
        if (!opportunity.IsOpen)
        {
            return ServiceResult<Opportunity>.Invalid(string.Empty, $"This opportunity is already {opportunity.Status}.");
        }

        if (outcome == OpportunityStage.Won && opportunity.Amount <= 0)
        {
            return ServiceResult<Opportunity>.Invalid(string.Empty, ValidationRules.AmountMessage);
        }

        if (remarks != null && remarks.Length > 500)
        {
            return ServiceResult<Opportunity>.Invalid("remarks", "Remarks cannot exceed 500 characters.");
        }

        var before = AuditSnapshot.Of(opportunity);
        SetStage(opportunity, outcome);
        if (!string.IsNullOrWhiteSpace(remarks))
        {
            var note = $"[{DateTime.Now:dd MMM yyyy}] {outcome}: {remarks.Trim()}";
            opportunity.Notes = string.IsNullOrEmpty(opportunity.Notes) ? note : $"{opportunity.Notes}\n{note}";
            if (opportunity.Notes.Length > ValidationRules.NotesLength)
            {
                opportunity.Notes = opportunity.Notes[^ValidationRules.NotesLength..];
            }
        }

        opportunity.ModifiedDate = DateTime.Now;
        await _db.SaveChangesAsync();

        var (oldValues, newValues) = AuditSnapshot.Diff(before, AuditSnapshot.Of(opportunity));
        await _audit.LogAsync(AuditActions.StatusChange, AuditEntities.Opportunity, id, oldValues, newValues,
            details: $"Opportunity marked {outcome}.");
        return ServiceResult<Opportunity>.Ok(opportunity);
    }

    public async Task<ServiceResult> DeleteAsync(int id)
    {
        if (_user.IsSalesExecutive)
        {
            return ServiceResult.Forbidden("Only Admins and Managers can delete opportunities.");
        }

        var found = await GetAsync(id);
        if (!found.Succeeded)
        {
            return found;
        }

        var opportunity = found.Value!;
        var snapshot = AuditSnapshot.Of(opportunity);
        var followUps = await _db.FollowUps.Where(f => f.OpportunityId == id).ToListAsync();
        _db.FollowUps.RemoveRange(followUps);
        _db.Opportunities.Remove(opportunity);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(AuditActions.Delete, AuditEntities.Opportunity, id, snapshot, null,
            details: followUps.Count > 0 ? $"Removed with {followUps.Count} follow-up(s)." : null);
        return ServiceResult.Ok();
    }

    private static void Apply(Opportunity o, OpportunityRequest r)
    {
        o.OpportunityName = r.OpportunityName!.Trim();
        o.CustomerId = r.CustomerId!.Value;
        o.LeadId = r.LeadId;
        o.Amount = r.Amount!.Value;
        o.Probability = r.Probability!.Value;
        o.ExpectedCloseDate = r.ExpectedCloseDate!.Value.Date;
        o.Source = r.Source;
        o.Notes = CustomerService.Clean(r.Notes);
        SetStage(o, r.Stage!.Value);
    }

    private static void SetStage(Opportunity o, OpportunityStage stage)
    {
        var wasOpen = o.Status == OpportunityStatus.Open;
        o.Stage = stage;
        o.Status = Opportunity.StatusForStage(stage);
        if (stage == OpportunityStage.Won)
        {
            o.Probability = 100;
        }
        else if (stage == OpportunityStage.Lost)
        {
            o.Probability = 0;
        }

        if (wasOpen && o.Status != OpportunityStatus.Open)
        {
            o.ClosedDate = DateTime.Now;
        }
    }
}
