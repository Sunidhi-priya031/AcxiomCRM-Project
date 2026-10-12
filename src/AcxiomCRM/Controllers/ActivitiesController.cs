using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

public class ActivitiesController : CrmController
{
    private readonly ActivityService _activities;
    private readonly CustomerService _customers;
    private readonly LeadService _leads;
    private readonly UserDirectoryService _users;
    private readonly IUserContext _user;

    public ActivitiesController(ActivityService activities, CustomerService customers, LeadService leads,
        UserDirectoryService users, IUserContext user)
    {
        _activities = activities;
        _customers = customers;
        _leads = leads;
        _users = users;
        _user = user;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] ActivitySearch search)
    {
        return View(new ActivityIndexViewModel
        {
            Search = search,
            Results = await _activities.SearchAsync(search),
            Owners = await _users.GetScopeUsersAsync(),
            CanDelete = !_user.IsSalesExecutive
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create(ActivityType? type, int? customerId, int? leadId)
    {
        var model = new ActivityFormViewModel
        {
            ActivityType = type ?? ActivityType.Call,
            ActivityDate = DateTime.Now.Date.AddHours(DateTime.Now.Hour),
            Status = ActivityStatus.Completed,
            CustomerId = customerId,
            LeadId = leadId,
            RelatedType = leadId.HasValue ? "Lead" : "Customer",
            AssignedTo = _user.UserId
        };
        await FillAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ActivityFormViewModel model)
    {
        KeepSelectedRelation(model);
        if (ModelState.IsValid)
        {
            var result = await _activities.CreateAsync(model);
            if (result.Succeeded)
            {
                Success($"{model.ActivityType} activity logged.");
                return RedirectToAction(nameof(Index));
            }

            result.CopyTo(ModelState);
        }

        await FillAsync(model);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var result = await _activities.GetAsync(id);
        if (Denied(result) is { } denied) return denied;

        var a = result.Value!;
        var model = new ActivityFormViewModel
        {
            ActivityId = a.ActivityId,
            ActivityType = a.ActivityType,
            Subject = a.Subject,
            Description = a.Description,
            ActivityDate = a.ActivityDate,
            CustomerId = a.CustomerId,
            LeadId = a.LeadId,
            RelatedType = a.LeadId.HasValue ? "Lead" : "Customer",
            Status = a.Status,
            AssignedTo = a.AssignedTo
        };
        await FillAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ActivityFormViewModel model)
    {
        KeepSelectedRelation(model);
        if (ModelState.IsValid)
        {
            var result = await _activities.UpdateAsync(id, model);
            if (Denied(result) is { } denied) return denied;
            if (result.Succeeded)
            {
                Success("Activity updated.");
                return RedirectToAction(nameof(Index));
            }

            result.CopyTo(ModelState);
        }

        model.ActivityId = id;
        await FillAsync(model);
        return View(model);
    }

    [HttpGet]
    [Authorize(Roles = AppRoles.AdminOrManager)]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _activities.GetAsync(id);
        if (Denied(result) is { } denied) return denied;
        return View(result.Value);
    }

    [HttpPost, ActionName(nameof(Delete))]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.AdminOrManager)]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var result = await _activities.DeleteAsync(id);
        if (Denied(result) is { } denied) return denied;
        if (result.Succeeded) Success("Activity deleted.");
        else Error(result.FirstError ?? "The activity could not be deleted.");
        return RedirectToAction(nameof(Index));
    }

    private void KeepSelectedRelation(ActivityFormViewModel model)
    {
        if (model.RelatedType != "Customer") { model.CustomerId = null; ModelState.Remove(nameof(model.CustomerId)); }
        if (model.RelatedType != "Lead") { model.LeadId = null; ModelState.Remove(nameof(model.LeadId)); }
    }

    private async Task FillAsync(ActivityFormViewModel model)
    {
        var customers = (await _customers.GetActiveLookupAsync())
            .Select(c => new SelectListItem($"{c.CustomerName} ({c.CustomerCode})", c.CustomerId.ToString(), c.CustomerId == model.CustomerId)).ToList();
        // Activities may be logged against any lead in scope (including converted ones) for history.
        var leads = (await (await _leads.ScopedQueryAsync()).OrderBy(l => l.LeadName).Select(l => new { l.LeadId, l.LeadName, l.LeadCode }).ToListAsync())
            .Select(l => new SelectListItem($"{l.LeadName} ({l.LeadCode})", l.LeadId.ToString(), l.LeadId == model.LeadId)).ToList();

        if (model.CustomerId.HasValue && customers.All(i => i.Value != model.CustomerId.ToString()))
        {
            var c = await _customers.GetAsync(model.CustomerId.Value);
            if (c.Succeeded) customers.Insert(0, new SelectListItem($"{c.Value!.CustomerName} ({c.Value.CustomerCode})", c.Value.CustomerId.ToString(), true));
        }

        model.CustomerOptions = customers;
        model.LeadOptions = leads;

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
