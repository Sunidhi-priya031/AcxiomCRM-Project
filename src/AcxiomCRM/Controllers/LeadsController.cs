using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

public class LeadsController : CrmController
{
    private readonly LeadService _leads;
    private readonly CustomerService _customers;
    private readonly UserDirectoryService _users;
    private readonly IUserContext _user;
    private readonly ApplicationDbContext _db;

    public LeadsController(LeadService leads, CustomerService customers, UserDirectoryService users, IUserContext user, ApplicationDbContext db)
    {
        _leads = leads;
        _customers = customers;
        _users = users;
        _user = user;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] LeadSearch search)
    {
        return View(new LeadIndexViewModel
        {
            Search = search,
            Results = await _leads.SearchAsync(search),
            Owners = await _users.GetScopeUsersAsync(),
            CanDelete = !_user.IsSalesExecutive
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var result = await _leads.GetAsync(id);
        if (Denied(result) is { } denied) return denied;

        var scope = await _user.GetScopeUserIdsAsync();
        var key = id.ToString();
        var lead = result.Value!;
        return View(new LeadDetailsViewModel
        {
            Lead = lead,
            Opportunities = await _db.Opportunities.InScope(scope).AsNoTracking().Include(o => o.Customer)
                .Where(o => o.LeadId == id).ToListAsync(),
            FollowUps = await _db.FollowUps.InScope(scope).AsNoTracking().Include(f => f.AssignedToUser)
                .Where(f => f.LeadId == id).OrderByDescending(f => f.FollowUpDate).ToListAsync(),
            Activities = await _db.Activities.InScope(scope).AsNoTracking().Include(a => a.AssignedToUser)
                .Where(a => a.LeadId == id).OrderByDescending(a => a.ActivityDate).ToListAsync(),
            History = await _db.AuditLogs.AsNoTracking()
                .Where(a => a.EntityName == AuditEntities.Lead && a.RecordId == key)
                .OrderByDescending(a => a.AuditLogId).Take(25).ToListAsync(),
            CanDelete = !_user.IsSalesExecutive,
            NextStatuses = LeadWorkflow.AllowedTargets(lead.Status).Where(s => s != lead.Status)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new LeadFormViewModel
        {
            Status = LeadStatus.New,
            Priority = LeadPriority.Medium,
            Source = LeadSource.Website,
            ExpectedValue = 0,
            AssignedTo = _user.UserId
        };
        await FillAsync(model, null);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(LeadFormViewModel model)
    {
        if (ModelState.IsValid)
        {
            var result = await _leads.CreateAsync(model);
            if (result.Succeeded)
            {
                Success($"Lead {result.Value!.LeadCode} created.");
                return RedirectToAction(nameof(Details), new { id = result.Value.LeadId });
            }

            result.CopyTo(ModelState);
        }

        await FillAsync(model, null);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var result = await _leads.GetAsync(id);
        if (Denied(result) is { } denied) return denied;

        var l = result.Value!;
        if (l.Status == LeadStatus.Converted)
        {
            Warning("Converted leads are read-only.");
            return RedirectToAction(nameof(Details), new { id });
        }

        var model = new LeadFormViewModel
        {
            LeadId = l.LeadId,
            LeadCode = l.LeadCode,
            LeadName = l.LeadName,
            Email = l.Email,
            Phone = l.Phone,
            CompanyName = l.CompanyName,
            Source = l.Source,
            Status = l.Status,
            Priority = l.Priority,
            ExpectedValue = l.ExpectedValue,
            Notes = l.Notes,
            AssignedTo = l.AssignedTo
        };
        await FillAsync(model, l.Status);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, LeadFormViewModel model)
    {
        var current = await _leads.GetAsync(id);
        if (Denied(current) is { } denied) return denied;

        if (ModelState.IsValid)
        {
            var result = await _leads.UpdateAsync(id, model);
            if (result.Succeeded)
            {
                Success("Lead updated.");
                return RedirectToAction(nameof(Details), new { id });
            }

            result.CopyTo(ModelState);
        }

        model.LeadId = id;
        model.LeadCode = current.Value!.LeadCode;
        await FillAsync(model, current.Value.Status);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Convert(int id)
    {
        var result = await _leads.GetAsync(id);
        if (Denied(result) is { } denied) return denied;

        var lead = result.Value!;
        if (lead.Status != LeadStatus.Qualified)
        {
            Warning("Only qualified leads can be converted. Move the lead to Qualified first.");
            return RedirectToAction(nameof(Details), new { id });
        }

        var model = new LeadConvertViewModel
        {
            CustomerName = lead.LeadName,
            Email = lead.Email,
            Phone = lead.Phone,
            CompanyName = lead.CompanyName,
            OpportunityName = string.IsNullOrEmpty(lead.CompanyName) ? $"{lead.LeadName} - new business" : $"{lead.CompanyName} - new business",
            Amount = lead.ExpectedValue > 0 ? lead.ExpectedValue : null,
            Probability = OpportunityWorkflow.DefaultProbability(OpportunityStage.Qualification),
            ExpectedCloseDate = DateTime.Today.AddDays(30),
            Stage = OpportunityStage.Qualification
        };
        await FillConvertAsync(model, lead);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Convert(int id, LeadConvertViewModel model)
    {
        var current = await _leads.GetAsync(id);
        if (Denied(current) is { } denied) return denied;

        if (ModelState.IsValid)
        {
            var result = await _leads.ConvertAsync(id, model);
            if (result.Succeeded)
            {
                var r = result.Value!;
                Success($"Lead converted to customer {r.Customer.CustomerCode}" + (r.Opportunity != null ? " and a new opportunity was created." : "."));
                return r.Opportunity != null
                    ? RedirectToAction("Details", "Opportunities", new { id = r.Opportunity.OpportunityId })
                    : RedirectToAction("Details", "Customers", new { id = r.Customer.CustomerId });
            }

            result.CopyTo(ModelState);
        }

        await FillConvertAsync(model, current.Value!);
        return View(model);
    }

    [HttpGet]
    [Authorize(Roles = AppRoles.AdminOrManager)]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _leads.GetAsync(id);
        if (Denied(result) is { } denied) return denied;

        var related = await _leads.CountRelatedAsync(id);
        return View(new LeadDeleteViewModel
        {
            Lead = result.Value!,
            FollowUps = related.FollowUps,
            Activities = related.Activities,
            Blocked = result.Value!.Status == LeadStatus.Converted || related.Opportunities > 0
        });
    }

    [HttpPost, ActionName(nameof(Delete))]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.AdminOrManager)]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var result = await _leads.DeleteAsync(id);
        if (Denied(result) is { } denied) return denied;
        if (!result.Succeeded)
        {
            Error(result.FirstError ?? "The lead could not be deleted.");
            return RedirectToAction(nameof(Details), new { id });
        }

        Success("Lead deleted.");
        return RedirectToAction(nameof(Index));
    }

    private async Task FillAsync(LeadFormViewModel model, LeadStatus? currentStatus)
    {
        var statuses = currentStatus.HasValue ? LeadWorkflow.AllowedTargets(currentStatus.Value) : LeadWorkflow.InitialStatuses;
        model.StatusOptions = EnumSelect(statuses, model.Status);
        var assignable = await _users.GetAssignableUsersAsync();
        if (model.AssignedTo != null && assignable.All(u => u.Id != model.AssignedTo))
        {
            var current = (await _users.GetAllUsersAsync()).FirstOrDefault(u => u.Id == model.AssignedTo);
            if (current != null) assignable.Insert(0, current);
        }

        model.AssignableUsers = UserSelect(assignable, model.AssignedTo);
        model.CanChooseAssignee = !_user.IsSalesExecutive;
    }

    private async Task FillConvertAsync(LeadConvertViewModel model, Models.Lead lead)
    {
        model.Lead = lead;
        model.Customers = (await _customers.GetActiveLookupAsync())
            .Select(c => new SelectListItem($"{c.CustomerName} ({c.CustomerCode})", c.CustomerId.ToString(), c.CustomerId == model.ExistingCustomerId));
        model.StageOptions = EnumSelect(OpportunityWorkflow.OpenStages, model.Stage);
    }
}
