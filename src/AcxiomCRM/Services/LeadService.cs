using AcxiomCRM.Data;
using AcxiomCRM.Dtos;
using AcxiomCRM.Infrastructure;
using AcxiomCRM.Models;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services;

public class LeadSearch
{
    /// <summary>Matches lead name, company, email, phone or code.</summary>
    public string? Q { get; set; }
    public LeadStatus? Status { get; set; }
    public LeadSource? Source { get; set; }
    public LeadPriority? Priority { get; set; }
    public string? AssignedTo { get; set; }
    public string? Sort { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }
}

public record LeadConversionResult(Lead Lead, Customer Customer, Opportunity? Opportunity, bool CustomerCreated);

public class LeadService
{
    private readonly ApplicationDbContext _db;
    private readonly IUserContext _user;
    private readonly AuditService _audit;
    private readonly UserDirectoryService _users;
    private readonly CustomerService _customers;

    public LeadService(ApplicationDbContext db, IUserContext user, AuditService audit, UserDirectoryService users, CustomerService customers)
    {
        _db = db;
        _user = user;
        _audit = audit;
        _users = users;
        _customers = customers;
    }

    public async Task<IQueryable<Lead>> ScopedQueryAsync() =>
        _db.Leads.InScope(await _user.GetScopeUserIdsAsync());

    public async Task<PagedResult<Lead>> SearchAsync(LeadSearch s)
    {
        var query = (await ScopedQueryAsync()).Include(l => l.AssignedToUser).AsNoTracking();

        if (!string.IsNullOrWhiteSpace(s.Q))
        {
            var term = s.Q.Trim().ToLower();
            query = query.Where(l =>
                l.LeadName.ToLower().Contains(term) ||
                l.LeadCode.ToLower().Contains(term) ||
                (l.CompanyName != null && l.CompanyName.ToLower().Contains(term)) ||
                (l.Email != null && l.Email.ToLower().Contains(term)) ||
                (l.Phone != null && l.Phone.Contains(term)));
        }

        if (s.Status.HasValue) query = query.Where(l => l.Status == s.Status.Value);
        if (s.Source.HasValue) query = query.Where(l => l.Source == s.Source.Value);
        if (s.Priority.HasValue) query = query.Where(l => l.Priority == s.Priority.Value);
        if (!string.IsNullOrWhiteSpace(s.AssignedTo)) query = query.Where(l => l.AssignedTo == s.AssignedTo);

        query = s.Sort switch
        {
            "name" => query.OrderBy(l => l.LeadName),
            "name_desc" => query.OrderByDescending(l => l.LeadName),
            "company" => query.OrderBy(l => l.CompanyName),
            "company_desc" => query.OrderByDescending(l => l.CompanyName),
            "status" => query.OrderBy(l => l.Status),
            "status_desc" => query.OrderByDescending(l => l.Status),
            "value" => query.OrderBy(l => l.ExpectedValue),
            "value_desc" => query.OrderByDescending(l => l.ExpectedValue),
            "created" => query.OrderBy(l => l.CreatedDate),
            _ => query.OrderByDescending(l => l.CreatedDate)
        };

        return await query.ToPagedResultAsync(s.Page, s.PageSize);
    }

    public async Task<ServiceResult<Lead>> GetAsync(int id)
    {
        var lead = await _db.Leads
            .Include(l => l.AssignedToUser)
            .Include(l => l.CreatedByUser)
            .Include(l => l.ConvertedCustomer)
            .FirstOrDefaultAsync(l => l.LeadId == id);
        if (lead == null)
        {
            return ServiceResult<Lead>.NotFound();
        }

        var scope = await _user.GetScopeUserIdsAsync();
        return ScopeExtensions.IsInScope(scope, lead.AssignedTo, lead.CreatedBy)
            ? ServiceResult<Lead>.Ok(lead)
            : ServiceResult<Lead>.Forbidden("You do not have access to this lead.");
    }

    public async Task<List<Lead>> GetOpenLookupAsync()
    {
        return await (await ScopedQueryAsync())
            .AsNoTracking()
            .Where(l => l.Status != LeadStatus.Converted && l.Status != LeadStatus.Lost)
            .OrderBy(l => l.LeadName)
            .ToListAsync();
    }

    public async Task<ServiceResult<Lead>> CreateAsync(LeadRequest request)
    {
        var errors = new ValidationErrors();
        if (!LeadWorkflow.InitialStatuses.Contains(request.Status!.Value))
        {
            errors.Add(nameof(LeadRequest.Status), "A new lead must start as New, Contacted, Qualified or Unqualified. Use Convert for conversions.");
        }

        var (assignee, assignError) = await _users.ResolveAssigneeAsync(request.AssignedTo);
        if (assignError != null)
        {
            errors.Add(nameof(LeadRequest.AssignedTo), assignError);
        }

        if (errors.Any)
        {
            return ServiceResult<Lead>.Invalid(errors.Errors);
        }

        var lead = new Lead
        {
            LeadCode = "TMP-" + Guid.NewGuid().ToString("N")[..12],
            CreatedBy = _user.UserId,
            CreatedDate = DateTime.Now,
            AssignedTo = assignee!
        };
        Apply(lead, request);

        _db.Leads.Add(lead);
        await _db.SaveChangesAsync();
        lead.LeadCode = $"LEAD-{lead.LeadId:D5}";
        await _db.SaveChangesAsync();

        await _audit.LogAsync(AuditActions.Create, AuditEntities.Lead, lead.LeadId, null, AuditSnapshot.Of(lead));
        return ServiceResult<Lead>.Ok(lead);
    }

    public async Task<ServiceResult<Lead>> UpdateAsync(int id, LeadRequest request)
    {
        var found = await GetAsync(id);
        if (!found.Succeeded)
        {
            return found;
        }

        var lead = found.Value!;
        var errors = new ValidationErrors();
        if (lead.Status == LeadStatus.Converted)
        {
            return ServiceResult<Lead>.Invalid(string.Empty, "Converted leads cannot be edited.");
        }

        var newStatus = request.Status!.Value;
        if (newStatus == LeadStatus.Converted && lead.Status != LeadStatus.Converted)
        {
            errors.Add(nameof(LeadRequest.Status), "Use the Convert action to convert a qualified lead.");
        }
        else if (!LeadWorkflow.CanTransition(lead.Status, newStatus))
        {
            errors.Add(nameof(LeadRequest.Status), $"Lead status cannot change from {lead.Status} to {newStatus}.");
        }

        var (assignee, assignError) = await _users.ResolveAssigneeAsync(request.AssignedTo, lead.AssignedTo);
        if (assignError != null)
        {
            errors.Add(nameof(LeadRequest.AssignedTo), assignError);
        }

        if (errors.Any)
        {
            return ServiceResult<Lead>.Invalid(errors.Errors);
        }

        var before = AuditSnapshot.Of(lead);
        Apply(lead, request);
        lead.AssignedTo = assignee!;
        lead.ModifiedDate = DateTime.Now;
        await _db.SaveChangesAsync();

        var (oldValues, newValues) = AuditSnapshot.Diff(before, AuditSnapshot.Of(lead));
        if (newValues.Count > 0)
        {
            var action = oldValues.ContainsKey(nameof(Lead.Status)) ? AuditActions.StatusChange : AuditActions.Update;
            await _audit.LogAsync(action, AuditEntities.Lead, lead.LeadId, oldValues, newValues);
        }

        return ServiceResult<Lead>.Ok(lead);
    }

    /// <summary>
    /// Lead-to-customer workflow step 5: converts a Qualified lead into a new or existing customer and,
    /// optionally, an opportunity. Everything happens in one transaction and is recorded in the audit log.
    /// </summary>
    public async Task<ServiceResult<LeadConversionResult>> ConvertAsync(int id, LeadConversionRequest request)
    {
        var found = await GetAsync(id);
        if (!found.Succeeded)
        {
            return ServiceResult<LeadConversionResult>.From(found);
        }

        var lead = found.Value!;
        if (lead.Status != LeadStatus.Qualified)
        {
            return ServiceResult<LeadConversionResult>.Invalid(string.Empty, "Only qualified leads can be converted.");
        }

        var errors = new ValidationErrors();
        Customer? customer = null;
        if (request.CreateNewCustomer)
        {
            var duplicates = await _customers.CheckDuplicatesAsync(request.Email, request.Phone, null);
            if (duplicates.Any)
            {
                return ServiceResult<LeadConversionResult>.Conflict(duplicates.Errors);
            }
        }
        else
        {
            var existing = request.ExistingCustomerId.HasValue ? await _customers.GetAsync(request.ExistingCustomerId.Value) : null;
            if (existing == null || !existing.Succeeded)
            {
                errors.Add(nameof(LeadConversionRequest.ExistingCustomerId), "Select a valid customer you have access to.");
            }
            else if (existing.Value!.Status != CustomerStatus.Active)
            {
                errors.Add(nameof(LeadConversionRequest.ExistingCustomerId), "The selected customer is inactive.");
            }
            else
            {
                customer = existing.Value;
            }
        }

        if (request.CreateOpportunity && request.Stage.HasValue && Opportunity.IsClosedStage(request.Stage.Value))
        {
            errors.Add(nameof(LeadConversionRequest.Stage), "A new opportunity must start in an open pipeline stage.");
        }

        if (errors.Any)
        {
            return ServiceResult<LeadConversionResult>.Invalid(errors.Errors);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync();

        var customerCreated = false;
        if (customer == null)
        {
            customer = new Customer
            {
                CustomerCode = "TMP-" + Guid.NewGuid().ToString("N")[..12],
                CustomerName = request.CustomerName!.Trim(),
                Email = CustomerService.NormalizeEmail(request.Email),
                Phone = CustomerService.NormalizePhone(request.Phone),
                CompanyName = CustomerService.Clean(request.CompanyName),
                Address = CustomerService.Clean(request.Address),
                City = CustomerService.Clean(request.City),
                State = CustomerService.Clean(request.State),
                Status = CustomerStatus.Active,
                Notes = $"Converted from lead {lead.LeadCode}.",
                AssignedTo = lead.AssignedTo,
                CreatedBy = _user.UserId,
                CreatedDate = DateTime.Now
            };
            var saved = await _customers.SaveNewAsync(customer);
            if (!saved.Succeeded)
            {
                return ServiceResult<LeadConversionResult>.From(saved);
            }

            customerCreated = true;
        }

        Opportunity? opportunity = null;
        if (request.CreateOpportunity)
        {
            var stage = request.Stage ?? OpportunityStage.Qualification;
            opportunity = new Opportunity
            {
                OpportunityName = request.OpportunityName!.Trim(),
                CustomerId = customer.CustomerId,
                LeadId = lead.LeadId,
                Amount = request.Amount!.Value,
                Probability = request.Probability!.Value,
                ExpectedCloseDate = request.ExpectedCloseDate!.Value.Date,
                Stage = stage,
                Status = Opportunity.StatusForStage(stage),
                Source = lead.Source,
                AssignedTo = lead.AssignedTo,
                CreatedBy = _user.UserId,
                CreatedDate = DateTime.Now
            };
            _db.Opportunities.Add(opportunity);
        }

        var before = new { Status = lead.Status.ToString() };
        lead.Status = LeadStatus.Converted;
        lead.ConvertedDate = DateTime.Now;
        lead.ConvertedCustomerId = customer.CustomerId;
        lead.ModifiedDate = DateTime.Now;
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        if (customerCreated)
        {
            await _audit.LogAsync(AuditActions.Create, AuditEntities.Customer, customer.CustomerId, null, AuditSnapshot.Of(customer),
                details: $"Created from lead {lead.LeadCode}.");
        }

        if (opportunity != null)
        {
            await _audit.LogAsync(AuditActions.Create, AuditEntities.Opportunity, opportunity.OpportunityId, null, AuditSnapshot.Of(opportunity),
                details: $"Created from lead {lead.LeadCode}.");
        }

        await _audit.LogAsync(AuditActions.Convert, AuditEntities.Lead, lead.LeadId, before,
            new { Status = nameof(LeadStatus.Converted), CustomerId = customer.CustomerId, OpportunityId = opportunity?.OpportunityId },
            details: $"Lead converted to customer {customer.CustomerCode}" + (opportunity != null ? " with an opportunity." : "."));

        return ServiceResult<LeadConversionResult>.Ok(new LeadConversionResult(lead, customer, opportunity, customerCreated));
    }

    public async Task<(int FollowUps, int Activities, int Opportunities)> CountRelatedAsync(int leadId)
    {
        return (await _db.FollowUps.CountAsync(f => f.LeadId == leadId),
                await _db.Activities.CountAsync(a => a.LeadId == leadId),
                await _db.Opportunities.CountAsync(o => o.LeadId == leadId));
    }

    /// <summary>Deletes a lead and its follow-ups/activities. Admin and Manager only; converted leads are kept for history.</summary>
    public async Task<ServiceResult> DeleteAsync(int id)
    {
        if (_user.IsSalesExecutive)
        {
            return ServiceResult.Forbidden("Only Admins and Managers can delete leads.");
        }

        var found = await GetAsync(id);
        if (!found.Succeeded)
        {
            return found;
        }

        var lead = found.Value!;
        var related = await CountRelatedAsync(id);
        if (lead.Status == LeadStatus.Converted || related.Opportunities > 0)
        {
            return ServiceResult.Invalid(string.Empty, "Converted leads and leads linked to opportunities cannot be deleted.");
        }

        var snapshot = AuditSnapshot.Of(lead);
        _db.FollowUps.RemoveRange(await _db.FollowUps.Where(f => f.LeadId == id).ToListAsync());
        _db.Activities.RemoveRange(await _db.Activities.Where(a => a.LeadId == id).ToListAsync());
        _db.Leads.Remove(lead);
        await _db.SaveChangesAsync();

        await _audit.LogAsync(AuditActions.Delete, AuditEntities.Lead, id, snapshot, null,
            details: $"Removed with {related.FollowUps} follow-up(s) and {related.Activities} activity record(s).");
        return ServiceResult.Ok();
    }

    private static void Apply(Lead lead, LeadRequest r)
    {
        lead.LeadName = r.LeadName!.Trim();
        lead.Email = string.IsNullOrWhiteSpace(r.Email) ? null : CustomerService.NormalizeEmail(r.Email);
        lead.Phone = CustomerService.Clean(r.Phone);
        lead.CompanyName = CustomerService.Clean(r.CompanyName);
        lead.Source = r.Source!.Value;
        lead.Status = r.Status!.Value;
        lead.Priority = r.Priority!.Value;
        lead.ExpectedValue = r.ExpectedValue!.Value;
        lead.Notes = CustomerService.Clean(r.Notes);
    }
}
