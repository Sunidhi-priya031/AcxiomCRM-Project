using AcxiomCRM.Data;
using AcxiomCRM.Infrastructure;
using AcxiomCRM.Models;
using AcxiomCRM.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AcxiomCRM.Services;

/// <summary>
/// Builds the role-based dashboard. Every figure is computed from records in the caller's authorised scope.
/// Created-date based counts use the selected period; Won/Lost counts use the date the opportunity was closed.
/// </summary>
public class DashboardService
{
    private readonly ApplicationDbContext _db;
    private readonly IUserContext _user;
    private readonly UserDirectoryService _users;
    private readonly DashboardSettings _settings;

    public DashboardService(ApplicationDbContext db, IUserContext user, UserDirectoryService users, IOptions<DashboardSettings> settings)
    {
        _db = db;
        _user = user;
        _users = users;
        _settings = settings.Value;
    }

    public async Task<DashboardViewModel> BuildAsync(DateRange range)
    {
        var scope = await _user.GetScopeUserIdsAsync();
        var vm = new DashboardViewModel
        {
            Range = range,
            Role = _user.IsAdmin ? AppRoles.Admin : _user.IsManager ? AppRoles.Manager : AppRoles.SalesExecutive,
            ScopeLabel = _user.IsAdmin ? "All records" : _user.IsManager ? "Your team" : "Your assigned records",
            MonthlyChartMonths = Math.Clamp(_settings.MonthlyChartMonths, 1, 36),
            UpcomingDays = Math.Clamp(_settings.UpcomingFollowUpDays, 1, 60)
        };

        // Small projections are loaded and aggregated in memory: this keeps the numbers exact for decimal
        // amounts on every database provider (SQLite cannot aggregate decimals server-side).
        var customers = await _db.Customers.InScope(scope).AsNoTracking()
            .Select(c => new { c.CreatedDate }).ToListAsync();
        var leads = await _db.Leads.InScope(scope).AsNoTracking()
            .Select(l => new { l.Status, l.CreatedDate, l.AssignedTo }).ToListAsync();
        var opportunities = await _db.Opportunities.InScope(scope).AsNoTracking()
            .Select(o => new { o.Status, o.Stage, o.Amount, o.Probability, o.CreatedDate, o.ClosedDate, o.AssignedTo })
            .ToListAsync();

        var leadsInRange = leads.Where(l => range.Contains(l.CreatedDate)).ToList();
        var oppsInRange = opportunities.Where(o => range.Contains(o.CreatedDate)).ToList();
        var openInRange = oppsInRange.Where(o => o.Status == OpportunityStatus.Open).ToList();
        var closedInRange = opportunities.Where(o => o.ClosedDate.HasValue && range.Contains(o.ClosedDate.Value)).ToList();

        vm.TotalCustomers = customers.Count(c => range.Contains(c.CreatedDate));
        vm.TotalLeads = leadsInRange.Count;
        vm.OpenLeads = leadsInRange.Count(l => LeadWorkflow.IsOpen(l.Status));
        vm.TotalOpportunities = oppsInRange.Count;
        vm.OpenOpportunities = openInRange.Count;
        vm.WonOpportunities = closedInRange.Count(o => o.Status == OpportunityStatus.Won);
        vm.LostOpportunities = closedInRange.Count(o => o.Status == OpportunityStatus.Lost);
        vm.WonAmount = closedInRange.Where(o => o.Status == OpportunityStatus.Won).Sum(o => o.Amount);
        vm.TotalPipelineValue = openInRange.Sum(o => o.Amount);
        vm.WeightedPipelineValue = openInRange.Sum(o => Math.Round(o.Amount * o.Probability / 100m, 2));

        // Lead status chart
        vm.LeadStatusChart = Enum.GetValues<LeadStatus>()
            .Select(s => new ChartPoint(s.ToString(), leadsInRange.Count(l => l.Status == s)))
            .ToList();

        // Opportunity pipeline chart: open stages by created date, Won/Lost by closed date.
        vm.PipelineChart = Enum.GetValues<OpportunityStage>()
            .Select(stage =>
            {
                var items = Opportunity.IsClosedStage(stage)
                    ? closedInRange.Where(o => o.Stage == stage).ToList()
                    : openInRange.Where(o => o.Stage == stage).ToList();
                return new StageChartPoint(stage.ToString(), items.Count, items.Sum(o => o.Amount));
            })
            .ToList();

        // Monthly sales: last N months (configured reporting period), by close month.
        var firstMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-(vm.MonthlyChartMonths - 1));
        for (var i = 0; i < vm.MonthlyChartMonths; i++)
        {
            var start = firstMonth.AddMonths(i);
            var end = start.AddMonths(1);
            var closed = opportunities.Where(o => o.ClosedDate >= start && o.ClosedDate < end).ToList();
            vm.MonthlyChart.Add(new MonthlyChartPoint(
                start.ToString("MMM yyyy"),
                closed.Where(o => o.Status == OpportunityStatus.Won).Sum(o => o.Amount),
                closed.Count(o => o.Status == OpportunityStatus.Won),
                closed.Count(o => o.Status == OpportunityStatus.Lost)));
        }

        // Follow-ups (not period-filtered: they are about what needs doing now)
        var today = DateTime.Today;
        var followUps = _db.FollowUps.InScope(scope).AsNoTracking().Where(f => f.Status == FollowUpStatus.Planned);
        vm.PendingFollowUps = await followUps.CountAsync();
        vm.OverdueFollowUps = await followUps.CountAsync(f => f.FollowUpDate < today);
        var upcomingLimit = today.AddDays(vm.UpcomingDays + 1);
        vm.UpcomingFollowUps = await followUps
            .Include(f => f.Customer).Include(f => f.Lead).Include(f => f.Opportunity).Include(f => f.AssignedToUser)
            .Where(f => f.FollowUpDate >= today && f.FollowUpDate < upcomingLimit)
            .OrderBy(f => f.FollowUpDate).Take(8).ToListAsync();
        vm.OverdueFollowUpItems = await followUps
            .Include(f => f.Customer).Include(f => f.Lead).Include(f => f.Opportunity).Include(f => f.AssignedToUser)
            .Where(f => f.FollowUpDate < today)
            .OrderBy(f => f.FollowUpDate).Take(5).ToListAsync();

        if (_user.IsAdmin || _user.IsManager)
        {
            var owners = await _users.GetScopeUsersAsync();
            vm.TeamPerformance = owners
                .Select(u =>
                {
                    var open = openInRange.Where(o => o.AssignedTo == u.Id).ToList();
                    var won = closedInRange.Where(o => o.AssignedTo == u.Id && o.Status == OpportunityStatus.Won).ToList();
                    return new OwnerPerformanceRow(
                        u.FullName,
                        leadsInRange.Count(l => l.AssignedTo == u.Id && LeadWorkflow.IsOpen(l.Status)),
                        open.Count,
                        open.Sum(o => o.Amount),
                        open.Sum(o => Math.Round(o.Amount * o.Probability / 100m, 2)),
                        won.Count,
                        won.Sum(o => o.Amount));
                })
                .Where(r => r.OpenLeads + r.OpenOpportunities + r.Won > 0)
                .OrderByDescending(r => r.PipelineValue)
                .ToList();
        }

        if (_user.IsAdmin)
        {
            vm.Admin = new AdminStats
            {
                ActiveUsers = await _db.Users.CountAsync(u => u.IsActive),
                InactiveUsers = await _db.Users.CountAsync(u => !u.IsActive),
                LockedOutUsers = (await _db.Users.Where(u => u.LockoutEnd != null).Select(u => u.LockoutEnd).ToListAsync())
                    .Count(end => end > DateTimeOffset.UtcNow),
                LoginsToday = await _db.AuditLogs.CountAsync(a => a.Action == AuditActions.Login && a.CreatedDate >= today),
                FailedLoginsToday = await _db.AuditLogs.CountAsync(a =>
                    (a.Action == AuditActions.LoginFailed || a.Action == AuditActions.Lockout) && a.CreatedDate >= today),
                RecentSecurityEvents = await _db.AuditLogs.AsNoTracking()
                    .Where(a => a.EntityName == AuditEntities.Account || a.EntityName == AuditEntities.User)
                    .OrderByDescending(a => a.AuditLogId).Take(6).ToListAsync()
            };
        }
        else if (_user.IsSalesExecutive)
        {
            vm.RecentActivities = await _db.Activities.InScope(scope).AsNoTracking()
                .Include(a => a.Customer).Include(a => a.Lead)
                .OrderByDescending(a => a.ActivityDate).Take(6).ToListAsync();
        }

        return vm;
    }
}
