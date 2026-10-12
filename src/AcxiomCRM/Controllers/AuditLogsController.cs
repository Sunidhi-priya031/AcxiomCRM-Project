using AcxiomCRM.Data;
using AcxiomCRM.Infrastructure;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

/// <summary>Read-only audit log viewer (Admin). There are deliberately no edit or delete actions.</summary>
[Authorize(Roles = AppRoles.Admin)]
public class AuditLogsController : CrmController
{
    private readonly ReportService _reports;
    private readonly UserDirectoryService _users;
    private readonly ApplicationDbContext _db;

    public AuditLogsController(ReportService reports, UserDirectoryService users, ApplicationDbContext db)
    {
        _reports = reports;
        _users = users;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] ReportFilter filter)
    {
        filter.PageSize ??= 25;
        return View(new AuditIndexViewModel
        {
            Filter = filter,
            Results = await _reports.FilterAudit(filter).ToPagedResultAsync(filter.Page, filter.PageSize),
            Users = await _users.GetAllUsersAsync()
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(long id)
    {
        var entry = await _db.AuditLogs.AsNoTracking().FirstOrDefaultAsync(a => a.AuditLogId == id);
        return entry == null ? NotFound() : View(entry);
    }
}
