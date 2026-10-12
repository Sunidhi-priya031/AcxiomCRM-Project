using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers;

/// <summary>Roles and the permission matrix. Role assignment happens on the user edit form.</summary>
[Authorize(Roles = AppRoles.Admin)]
public class RolesController : CrmController
{
    private readonly UserAdminService _admin;

    public RolesController(UserAdminService admin)
    {
        _admin = admin;
    }

    [HttpGet]
    public async Task<IActionResult> Index() => View(await _admin.GetRolesAsync());
}
