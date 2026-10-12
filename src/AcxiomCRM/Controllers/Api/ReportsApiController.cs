using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers.Api;

[Route("api/reports")]
public class ReportsApiController : ApiControllerBase
{
    private readonly ReportService _reports;
    private readonly IUserContext _user;

    public ReportsApiController(ReportService reports, IUserContext user)
    {
        _reports = reports;
        _user = user;
    }

    /// <summary>
    /// Pipeline report data. Admin: all records; Manager: team; Sales Executive: own pipeline only (configured scope).
    /// Query: from, to (created date), assignedTo.
    /// </summary>
    [HttpGet("pipeline")]
    public async Task<IActionResult> Pipeline([FromQuery] ReportFilter filter)
    {
        var report = await _reports.PipelineReportAsync(filter, export: true);
        return Ok(new
        {
            scope = _user.IsAdmin ? "All" : _user.IsManager ? "Team" : "Own",
            byStage = report.ByStage,
            byOwner = report.ByOwner.Items,
            summary = report.Summary.Select(s => new { s.Label, s.Value })
        });
    }
}
