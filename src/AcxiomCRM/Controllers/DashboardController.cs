using AcxiomCRM.Infrastructure;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers;

public class DashboardController : CrmController
{
    private readonly DashboardService _dashboard;

    public DashboardController(DashboardService dashboard)
    {
        _dashboard = dashboard;
    }

    /// <summary>Role-based dashboard. Period: all, today, week, month or custom (with from/to).</summary>
    [HttpGet]
    public async Task<IActionResult> Index(string? period, DateTime? from, DateTime? to)
    {
        var range = DateRange.Resolve(period, from, to);
        ViewBag.From = from;
        ViewBag.To = to;
        return View(await _dashboard.BuildAsync(range));
    }
}
