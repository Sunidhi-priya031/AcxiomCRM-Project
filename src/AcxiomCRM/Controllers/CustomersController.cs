using AcxiomCRM.Data;
using AcxiomCRM.Infrastructure;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

public class CustomersController : CrmController
{
    private readonly CustomerService _customers;
    private readonly UserDirectoryService _users;
    private readonly IUserContext _user;
    private readonly ApplicationDbContext _db;

    public CustomersController(CustomerService customers, UserDirectoryService users, IUserContext user, ApplicationDbContext db)
    {
        _customers = customers;
        _users = users;
        _user = user;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] CustomerSearch search)
    {
        return View(new CustomerIndexViewModel
        {
            Search = search,
            Results = await _customers.SearchAsync(search),
            Owners = await _users.GetScopeUsersAsync(),
            CanDelete = !_user.IsSalesExecutive
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var result = await _customers.GetAsync(id);
        if (Denied(result) is { } denied) return denied;

        var customer = result.Value!;
        var scope = await _user.GetScopeUserIdsAsync();
        var key = id.ToString();
        return View(new CustomerDetailsViewModel
        {
            Customer = customer,
            Opportunities = await _db.Opportunities.InScope(scope).AsNoTracking().Include(o => o.AssignedToUser)
                .Where(o => o.CustomerId == id).OrderByDescending(o => o.CreatedDate).ToListAsync(),
            FollowUps = await _db.FollowUps.InScope(scope).AsNoTracking().Include(f => f.AssignedToUser)
                .Where(f => f.CustomerId == id).OrderByDescending(f => f.FollowUpDate).Take(20).ToListAsync(),
            Activities = await _db.Activities.InScope(scope).AsNoTracking().Include(a => a.AssignedToUser)
                .Where(a => a.CustomerId == id).OrderByDescending(a => a.ActivityDate).Take(20).ToListAsync(),
            SourceLeads = await _db.Leads.InScope(scope).AsNoTracking().Where(l => l.ConvertedCustomerId == id).ToListAsync(),
            History = await _db.AuditLogs.AsNoTracking()
                .Where(a => a.EntityName == AuditEntities.Customer && a.RecordId == key)
                .OrderByDescending(a => a.AuditLogId).Take(25).ToListAsync(),
            CanDelete = !_user.IsSalesExecutive
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new CustomerFormViewModel { AssignedTo = _user.UserId };
        await FillAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CustomerFormViewModel model)
    {
        if (ModelState.IsValid)
        {
            var result = await _customers.CreateAsync(model);
            if (result.Succeeded)
            {
                Success($"Customer {result.Value!.CustomerCode} created.");
                return RedirectToAction(nameof(Details), new { id = result.Value.CustomerId });
            }

            result.CopyTo(ModelState);
        }

        await FillAsync(model);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var result = await _customers.GetAsync(id);
        if (Denied(result) is { } denied) return denied;

        var c = result.Value!;
        var model = new CustomerFormViewModel
        {
            CustomerId = c.CustomerId,
            CustomerCode = c.CustomerCode,
            CustomerName = c.CustomerName,
            Email = c.Email,
            Phone = c.Phone,
            CompanyName = c.CompanyName,
            Address = c.Address,
            City = c.City,
            State = c.State,
            Status = c.Status,
            Notes = c.Notes,
            AssignedTo = c.AssignedTo
        };
        await FillAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CustomerFormViewModel model)
    {
        if (ModelState.IsValid)
        {
            var result = await _customers.UpdateAsync(id, model);
            if (Denied(result) is { } denied) return denied;
            if (result.Succeeded)
            {
                Success("Customer updated.");
                return RedirectToAction(nameof(Details), new { id });
            }

            result.CopyTo(ModelState);
        }

        model.CustomerId = id;
        await FillAsync(model);
        return View(model);
    }

    [HttpGet]
    [Authorize(Roles = AppRoles.AdminOrManager)]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _customers.GetAsync(id);
        if (Denied(result) is { } denied) return denied;
        return View(new CustomerDeleteViewModel { Customer = result.Value!, RelatedCount = await _customers.CountRelatedAsync(id) });
    }

    [HttpPost, ActionName(nameof(Delete))]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.AdminOrManager)]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var result = await _customers.DeleteAsync(id);
        if (Denied(result) is { } denied) return denied;
        if (!result.Succeeded)
        {
            Error(result.FirstError ?? "The customer could not be deleted.");
            return RedirectToAction(nameof(Details), new { id });
        }

        if (result.Value)
        {
            Success("Customer deleted.");
            return RedirectToAction(nameof(Index));
        }

        Warning("The customer has related records, so it was deactivated instead of deleted.");
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>Remote validation used by the form for immediate duplicate feedback (the server re-checks on save).</summary>
    [HttpGet]
    public async Task<IActionResult> CheckEmail(string? email, int? customerId)
    {
        var errors = await _customers.CheckDuplicatesAsync(email, null, customerId);
        return Json(errors.Any ? "A customer with this email already exists." : (object)true);
    }

    [HttpGet]
    public async Task<IActionResult> CheckPhone(string? phone, int? customerId)
    {
        var errors = await _customers.CheckDuplicatesAsync(null, phone, customerId);
        return Json(errors.Any ? "A customer with this phone number already exists." : (object)true);
    }

    private async Task FillAsync(CustomerFormViewModel model)
    {
        var assignable = await _users.GetAssignableUsersAsync();
        // Keep the current owner selectable even if outside the caller's assignable set.
        if (model.AssignedTo != null && assignable.All(u => u.Id != model.AssignedTo))
        {
            var current = (await _users.GetAllUsersAsync()).FirstOrDefault(u => u.Id == model.AssignedTo);
            if (current != null) assignable.Insert(0, current);
        }

        model.AssignableUsers = UserSelect(assignable, model.AssignedTo);
        model.CanChooseAssignee = !_user.IsSalesExecutive;
    }
}
