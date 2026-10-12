using AcxiomCRM.Infrastructure;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AcxiomCRM.Controllers;

/// <summary>Common helpers for the MVC controllers.</summary>
public abstract class CrmController : Controller
{
    protected void Success(string message) => TempData["Success"] = message;
    protected void Error(string message) => TempData["Error"] = message;
    protected void Warning(string message) => TempData["Warning"] = message;

    /// <summary>Maps NotFound / Forbidden service results to the proper HTTP responses.</summary>
    protected IActionResult? Denied(ServiceResult result) => result.Status switch
    {
        ServiceStatus.NotFound => NotFound(),
        ServiceStatus.Forbidden => Forbid(),
        _ => null
    };

    protected static SelectList UserSelect(IEnumerable<UserOption> users, string? selected) =>
        new(users.Select(u => new { u.Id, Name = u.IsActive ? u.FullName : u.FullName + " (inactive)" }), "Id", "Name", selected);

    protected static IEnumerable<SelectListItem> EnumSelect<TEnum>(IEnumerable<TEnum>? only = null, TEnum? selected = null)
        where TEnum : struct, Enum =>
        (only ?? Enum.GetValues<TEnum>()).Select(v => new SelectListItem(v.GetDisplayName(), v.ToString(), selected.HasValue && selected.Value.Equals(v)));
}
