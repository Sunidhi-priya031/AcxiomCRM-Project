using AcxiomCRM.Data;
using AcxiomCRM.Dtos;
using AcxiomCRM.Infrastructure;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AcxiomCRM.Controllers;

public class FollowUpsController : CrmController
{
    private readonly FollowUpService _followUps;
    private readonly CustomerService _customers;
    private readonly LeadService _leads;
    private readonly OpportunityService _opportunities;
    private readonly UserDirectoryService _users;
    private readonly IUserContext _user;
    private readonly ApplicationDbContext _db;
    private readonly DashboardSettings _settings;

    public FollowUpsController(FollowUpService followUps, CustomerService customers, LeadService leads, OpportunityService opportunities,
        UserDirectoryService users, IUserContext user, ApplicationDbContext db, IOptions<DashboardSettings> settings)
    {
        _followUps = followUps;
        _customers = customers;
        _leads = leads;
        _opportunities = opportunities;
        _users = users;
        _user = user;
        _db = db;
        _settings = settings.Value;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] FollowUpSearch search)
    {
        var scope = await _user.GetScopeUserIdsAsync();
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);
        var upcomingLimit = today.AddDays(_settings.UpcomingFollowUpDays + 1);
        var planned = _db.FollowUps.InScope(scope).Where(f => f.Status == FollowUpStatus.Planned);

        return View(new FollowUpIndexViewModel
        {
            Search = search,
            Results = await _followUps.SearchAsync(search, _settings.UpcomingFollowUpDays),
            Owners = await _users.GetScopeUsersAsync(),
            PendingCount = await planned.CountAsync(),
            OverdueCount = await planned.CountAsync(f => f.FollowUpDate < today),
            TodayCount = await planned.CountAsync(f => f.FollowUpDate >= today && f.FollowUpDate < tomorrow),
            UpcomingCount = await planned.CountAsync(f => f.FollowUpDate >= today && f.FollowUpDate < upcomingLimit)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var result = await _followUps.GetAsync(id);
        if (Denied(result) is { } denied) return denied;

        var key = id.ToString();
        return View(new FollowUpDetailsViewModel
        {
            FollowUp = result.Value!,
            History = await _db.AuditLogs.AsNoTracking()
                .Where(a => a.EntityName == AuditEntities.FollowUp && a.RecordId == key)
                .OrderByDescending(a => a.AuditLogId).ToListAsync(),
            Reschedule = new FollowUpRescheduleRequest { NewDate = DateTime.Today.AddDays(1).AddHours(10) }
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? customerId, int? leadId, int? opportunityId)
    {
        var model = new FollowUpFormViewModel
        {
            CustomerId = customerId,
            LeadId = leadId,
            OpportunityId = opportunityId,
            RelatedType = leadId.HasValue ? "Lead" : opportunityId.HasValue ? "Opportunity" : "Customer",
            FollowUpType = FollowUpType.Call,
            FollowUpDate = DateTime.Today.AddDays(1).AddHours(10),
            AssignedTo = _user.UserId
        };
        await FillAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(FollowUpFormViewModel model)
    {
        KeepSelectedRelation(model);
        if (ModelState.IsValid)
        {
            var result = await _followUps.CreateAsync(model);
            if (result.Succeeded)
            {
                Success("Follow-up scheduled.");
                return RedirectToAction(nameof(Details), new { id = result.Value!.FollowUpId });
            }

            result.CopyTo(ModelState);
        }

        await FillAsync(model);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var result = await _followUps.GetAsync(id);
        if (Denied(result) is { } denied) return denied;

        var f = result.Value!;
        if (f.Status != FollowUpStatus.Planned)
        {
            Warning($"Only planned follow-ups can be edited. This one is {f.Status}.");
            return RedirectToAction(nameof(Details), new { id });
        }

        var model = new FollowUpFormViewModel
        {
            FollowUpId = f.FollowUpId,
            Subject = f.Subject,
            FollowUpType = f.FollowUpType,
            FollowUpDate = f.FollowUpDate,
            Remarks = f.Remarks,
            CustomerId = f.CustomerId,
            LeadId = f.LeadId,
            OpportunityId = f.OpportunityId,
            RelatedType = f.RelatedType,
            AssignedTo = f.AssignedTo
        };
        await FillAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, FollowUpFormViewModel model)
    {
        KeepSelectedRelation(model);
        if (ModelState.IsValid)
        {
            var result = await _followUps.UpdateAsync(id, model);
            if (Denied(result) is { } denied) return denied;
            if (result.Succeeded)
            {
                Success("Follow-up updated.");
                return RedirectToAction(nameof(Details), new { id });
            }

            result.CopyTo(ModelState);
        }

        model.FollowUpId = id;
        await FillAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(int id, [Bind(Prefix = "Complete")] FollowUpCompleteRequest request)
    {
        if (!ModelState.IsValid)
        {
            Error(ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage ?? "Invalid request.");
            return RedirectToAction(nameof(Details), new { id });
        }

        var result = await _followUps.CompleteAsync(id, request);
        if (Denied(result) is { } denied) return denied;
        if (result.Succeeded) Success("Follow-up completed and logged as an activity.");
        else Error(result.FirstError ?? "The follow-up could not be completed.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reschedule(int id, [Bind(Prefix = "Reschedule")] FollowUpRescheduleRequest request)
    {
        if (!ModelState.IsValid)
        {
            Error(ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage ?? "Invalid request.");
            return RedirectToAction(nameof(Details), new { id });
        }

        var result = await _followUps.RescheduleAsync(id, request);
        if (Denied(result) is { } denied) return denied;
        if (result.Succeeded) Success($"Follow-up rescheduled to {request.NewDate:dd MMM yyyy, hh:mm tt}.");
        else Error(result.FirstError ?? "The follow-up could not be rescheduled.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetStatus(int id, FollowUpStatus status, string? reason)
    {
        var result = await _followUps.SetStatusAsync(id, status, reason?.Length > 500 ? reason[..500] : reason);
        if (Denied(result) is { } denied) return denied;
        if (result.Succeeded) Success($"Follow-up marked {status}.");
        else Error(result.FirstError ?? "The follow-up could not be updated.");
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>Only the related record matching the chosen type is kept, so stale selections in hidden fields are ignored.</summary>
    private void KeepSelectedRelation(FollowUpFormViewModel model)
    {
        if (model.RelatedType != "Customer") { model.CustomerId = null; ModelState.Remove(nameof(model.CustomerId)); }
        if (model.RelatedType != "Lead") { model.LeadId = null; ModelState.Remove(nameof(model.LeadId)); }
        if (model.RelatedType != "Opportunity") { model.OpportunityId = null; ModelState.Remove(nameof(model.OpportunityId)); }
    }

    private async Task FillAsync(FollowUpFormViewModel model)
    {
        var customers = (await _customers.GetActiveLookupAsync())
            .Select(c => new SelectListItem($"{c.CustomerName} ({c.CustomerCode})", c.CustomerId.ToString(), c.CustomerId == model.CustomerId)).ToList();
        var leads = (await _leads.GetOpenLookupAsync())
            .Select(l => new SelectListItem($"{l.LeadName} ({l.LeadCode})", l.LeadId.ToString(), l.LeadId == model.LeadId)).ToList();
        var opportunities = (await _opportunities.GetOpenLookupAsync())
            .Select(o => new SelectListItem($"{o.OpportunityName} - {o.Customer?.CustomerName}", o.OpportunityId.ToString(), o.OpportunityId == model.OpportunityId)).ToList();

        // When editing, keep the currently linked record selectable even if it is no longer active/open.
        if (model.FollowUpId.HasValue)
        {
            if (model.CustomerId.HasValue && customers.All(i => i.Value != model.CustomerId.ToString()))
            {
                var c = await _customers.GetAsync(model.CustomerId.Value);
                if (c.Succeeded) customers.Insert(0, new SelectListItem($"{c.Value!.CustomerName} ({c.Value.CustomerCode})", c.Value.CustomerId.ToString(), true));
            }

            if (model.LeadId.HasValue && leads.All(i => i.Value != model.LeadId.ToString()))
            {
                var l = await _leads.GetAsync(model.LeadId.Value);
                if (l.Succeeded) leads.Insert(0, new SelectListItem($"{l.Value!.LeadName} ({l.Value.LeadCode})", l.Value.LeadId.ToString(), true));
            }

            if (model.OpportunityId.HasValue && opportunities.All(i => i.Value != model.OpportunityId.ToString()))
            {
                var o = await _opportunities.GetAsync(model.OpportunityId.Value);
                if (o.Succeeded) opportunities.Insert(0, new SelectListItem(o.Value!.OpportunityName, o.Value.OpportunityId.ToString(), true));
            }
        }

        model.CustomerOptions = customers;
        model.LeadOptions = leads;
        model.OpportunityOptions = opportunities;

        var assignable = await _users.GetAssignableUsersAsync();
        if (model.AssignedTo != null && assignable.All(u => u.Id != model.AssignedTo))
        {
            var current = (await _users.GetAllUsersAsync()).FirstOrDefault(u => u.Id == model.AssignedTo);
            if (current != null) assignable.Insert(0, current);
        }

        model.AssignableUsers = UserSelect(assignable, model.AssignedTo);
        model.CanChooseAssignee = !_user.IsSalesExecutive;
    }
}
