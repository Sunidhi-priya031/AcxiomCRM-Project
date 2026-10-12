using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

public class OpportunitiesController : CrmController
{
    private readonly OpportunityService _opportunities;
    private readonly CustomerService _customers;
    private readonly LeadService _leads;
    private readonly UserDirectoryService _users;
    private readonly IUserContext _user;
    private readonly ApplicationDbContext _db;

    public OpportunitiesController(OpportunityService opportunities, CustomerService customers, LeadService leads,
        UserDirectoryService users, IUserContext user, ApplicationDbContext db)
    {
        _opportunities = opportunities;
        _customers = customers;
        _leads = leads;
        _users = users;
        _user = user;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] OpportunitySearch search)
    {
        return View(new OpportunityIndexViewModel
        {
            Search = search,
            Results = await _opportunities.SearchAsync(search),
            Owners = await _users.GetScopeUsersAsync(),
            CanDelete = !_user.IsSalesExecutive
        });
    }

    /// <summary>Sales pipeline board: open opportunities by stage.</summary>
    [HttpGet]
    public async Task<IActionResult> Pipeline(string? assignedTo)
    {
        var open = await _opportunities.GetPipelineAsync(assignedTo);
        var model = new PipelineViewModel
        {
            AssignedTo = assignedTo,
            Owners = await _users.GetScopeUsersAsync(),
            Columns = OpportunityWorkflow.OpenStages.Select(s => new PipelineColumn(s, open.Where(o => o.Stage == s).ToList())).ToList(),
            TotalAmount = open.Sum(o => o.Amount),
            TotalWeighted = open.Sum(o => o.WeightedAmount)
        };
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var result = await _opportunities.GetAsync(id);
        if (Denied(result) is { } denied) return denied;

        var scope = await _user.GetScopeUserIdsAsync();
        var key = id.ToString();
        return View(new OpportunityDetailsViewModel
        {
            Opportunity = result.Value!,
            FollowUps = await _db.FollowUps.InScope(scope).AsNoTracking().Include(f => f.AssignedToUser)
                .Where(f => f.OpportunityId == id).OrderByDescending(f => f.FollowUpDate).ToListAsync(),
            History = await _db.AuditLogs.AsNoTracking()
                .Where(a => a.EntityName == AuditEntities.Opportunity && a.RecordId == key)
                .OrderByDescending(a => a.AuditLogId).Take(25).ToListAsync(),
            CanDelete = !_user.IsSalesExecutive
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? customerId)
    {
        var model = new OpportunityFormViewModel
        {
            CustomerId = customerId,
            Stage = OpportunityStage.Qualification,
            Probability = OpportunityWorkflow.DefaultProbability(OpportunityStage.Qualification),
            ExpectedCloseDate = DateTime.Today.AddDays(30),
            AssignedTo = _user.UserId
        };
        await FillAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(OpportunityFormViewModel model)
    {
        if (ModelState.IsValid)
        {
            var result = await _opportunities.CreateAsync(model);
            if (result.Succeeded)
            {
                Success("Opportunity created.");
                return RedirectToAction(nameof(Details), new { id = result.Value!.OpportunityId });
            }

            result.CopyTo(ModelState);
        }

        await FillAsync(model);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var result = await _opportunities.GetAsync(id);
        if (Denied(result) is { } denied) return denied;

        var o = result.Value!;
        if (!o.IsOpen)
        {
            Warning($"This opportunity is {o.Status} and can no longer be edited.");
            return RedirectToAction(nameof(Details), new { id });
        }

        var model = new OpportunityFormViewModel
        {
            OpportunityId = o.OpportunityId,
            OpportunityName = o.OpportunityName,
            CustomerId = o.CustomerId,
            LeadId = o.LeadId,
            Amount = o.Amount,
            Stage = o.Stage,
            Probability = o.Probability,
            ExpectedCloseDate = o.ExpectedCloseDate,
            Source = o.Source,
            Notes = o.Notes,
            AssignedTo = o.AssignedTo
        };
        await FillAsync(model, o);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, OpportunityFormViewModel model)
    {
        var current = await _opportunities.GetAsync(id);
        if (Denied(current) is { } denied) return denied;

        if (ModelState.IsValid)
        {
            var result = await _opportunities.UpdateAsync(id, model);
            if (result.Succeeded)
            {
                Success("Opportunity updated.");
                return RedirectToAction(nameof(Details), new { id });
            }

            result.CopyTo(ModelState);
        }

        model.OpportunityId = id;
        await FillAsync(model, current.Value);
        return View(model);
    }

    /// <summary>Marks an open opportunity Won or Lost.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Close(int id, OpportunityStage outcome, string? remarks)
    {
        var result = await _opportunities.CloseAsync(id, outcome, remarks);
        if (Denied(result) is { } denied) return denied;
        if (result.Succeeded) Success($"Opportunity marked {outcome}.");
        else Error(result.FirstError ?? "The opportunity could not be updated.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    [Authorize(Roles = AppRoles.AdminOrManager)]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _opportunities.GetAsync(id);
        if (Denied(result) is { } denied) return denied;
        ViewBag.FollowUps = await _db.FollowUps.CountAsync(f => f.OpportunityId == id);
        return View(result.Value);
    }

    [HttpPost, ActionName(nameof(Delete))]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.AdminOrManager)]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var result = await _opportunities.DeleteAsync(id);
        if (Denied(result) is { } denied) return denied;
        if (!result.Succeeded)
        {
            Error(result.FirstError ?? "The opportunity could not be deleted.");
            return RedirectToAction(nameof(Details), new { id });
        }

        Success("Opportunity deleted.");
        return RedirectToAction(nameof(Index));
    }

    private async Task FillAsync(OpportunityFormViewModel model, Opportunity? existing = null)
    {
        var customers = await _customers.GetActiveLookupAsync();
        if (existing?.Customer != null && customers.All(c => c.CustomerId != existing.CustomerId))
        {
            customers.Insert(0, existing.Customer);
        }

        model.CustomerOptions = customers.Select(c =>
            new SelectListItem($"{c.CustomerName} ({c.CustomerCode})", c.CustomerId.ToString(), c.CustomerId == model.CustomerId));

        var leads = await _leads.GetOpenLookupAsync();
        if (existing?.Lead != null && leads.All(l => l.LeadId != existing.LeadId))
        {
            leads.Insert(0, existing.Lead);
        }

        model.LeadOptions = leads.Select(l =>
            new SelectListItem($"{l.LeadName} ({l.LeadCode})", l.LeadId.ToString(), l.LeadId == model.LeadId));

        // New opportunities start in an open stage; Won/Lost are reached through the Close action or the edit form.
        model.StageOptions = existing == null
            ? EnumSelect(OpportunityWorkflow.OpenStages, model.Stage)
            : EnumSelect<OpportunityStage>(null, model.Stage);

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
