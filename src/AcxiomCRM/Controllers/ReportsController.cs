using AcxiomCRM.Infrastructure;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers;

/// <summary>
/// Reports. Every report is scoped to the caller's records; the User Activity report is limited to
/// Admin/Manager and the Audit report to Admin. Add <c>export=csv</c> to download all filtered rows.
/// </summary>
public class ReportsController : CrmController
{
    private readonly ReportService _reports;
    private readonly IUserContext _user;

    public ReportsController(ReportService reports, IUserContext user)
    {
        _reports = reports;
        _user = user;
    }

    [HttpGet]
    public IActionResult Index()
    {
        var links = new List<ReportLink>
        {
            new(nameof(Customers), "Customer Report", "Customer list, status, owner and creation date.", "All roles (own scope)"),
            new(nameof(Leads), "Lead Report", "Lead source, status, owner and conversion information.", "All roles (own scope)"),
            new(nameof(FollowUps), "Follow-Up Report", "Planned, completed, missed and overdue follow-ups.", "All roles (own scope)"),
            new(nameof(Opportunities), "Opportunity Report", "Stage, amount, probability and expected close date.", "All roles (own scope)"),
            new(nameof(Pipeline), "Pipeline Report", "Stage-wise and owner-wise pipeline, including weighted pipeline.", "All roles (own scope)"),
            new(nameof(Conversion), "Conversion Report", "Lead conversion and opportunity outcome metrics.", "All roles (own scope)")
        };
        if (!_user.IsSalesExecutive)
        {
            links.Add(new(nameof(UserActivity), "User Activity Report", "Actions and activity counts by user.", "Admin, Manager (team)"));
        }

        if (_user.IsAdmin)
        {
            links.Add(new(nameof(Audit), "Audit Report", "Security and business audit events.", "Admin only"));
        }

        return View(links);
    }

    [HttpGet]
    public async Task<IActionResult> Customers([FromQuery] ReportFilter filter, string? export)
    {
        var isExport = export == "csv";
        var report = await _reports.CustomerReportAsync(filter, isExport);
        return isExport ? CsvExport.ToFile(report.Rows.Items, "customer-report") : View("Customers", report);
    }

    [HttpGet]
    public async Task<IActionResult> Leads([FromQuery] ReportFilter filter, string? export)
    {
        var isExport = export == "csv";
        var report = await _reports.LeadReportAsync(filter, isExport);
        return isExport ? CsvExport.ToFile(report.Rows.Items, "lead-report") : View("Leads", report);
    }

    [HttpGet]
    public async Task<IActionResult> FollowUps([FromQuery] ReportFilter filter, string? export)
    {
        var isExport = export == "csv";
        var report = await _reports.FollowUpReportAsync(filter, isExport);
        return isExport ? CsvExport.ToFile(report.Rows.Items, "follow-up-report") : View("FollowUps", report);
    }

    [HttpGet]
    public async Task<IActionResult> Opportunities([FromQuery] ReportFilter filter, string? export)
    {
        var isExport = export == "csv";
        var report = await _reports.OpportunityReportAsync(filter, isExport);
        return isExport ? CsvExport.ToFile(report.Rows.Items, "opportunity-report") : View("Opportunities", report);
    }

    [HttpGet]
    public async Task<IActionResult> Pipeline([FromQuery] ReportFilter filter, string? export, string? table)
    {
        var isExport = export == "csv";
        var report = await _reports.PipelineReportAsync(filter, isExport);
        if (isExport)
        {
            return table == "stage"
                ? CsvExport.ToFile(report.ByStage, "pipeline-by-stage")
                : CsvExport.ToFile(report.ByOwner.Items, "pipeline-by-owner");
        }

        return View("Pipeline", report);
    }

    [HttpGet]
    public async Task<IActionResult> Conversion([FromQuery] ReportFilter filter, string? export, string? table)
    {
        var isExport = export == "csv";
        var report = await _reports.ConversionReportAsync(filter, isExport);
        if (isExport)
        {
            return table == "source"
                ? CsvExport.ToFile(report.BySource, "conversion-by-source")
                : CsvExport.ToFile(report.ByOwner.Items, "conversion-by-owner");
        }

        return View("Conversion", report);
    }

    [HttpGet]
    [Authorize(Roles = AppRoles.AdminOrManager)]
    public async Task<IActionResult> UserActivity([FromQuery] ReportFilter filter, string? export)
    {
        var isExport = export == "csv";
        var report = await _reports.UserActivityReportAsync(filter, isExport);
        return isExport ? CsvExport.ToFile(report.Rows.Items, "user-activity-report") : View("UserActivity", report);
    }

    [HttpGet]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> Audit([FromQuery] ReportFilter filter, string? export)
    {
        var isExport = export == "csv";
        var report = await _reports.AuditReportAsync(filter, isExport);
        return isExport ? CsvExport.ToFile(report.Rows.Items, "audit-report") : View("Audit", report);
    }
}
