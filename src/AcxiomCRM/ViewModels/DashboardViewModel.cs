using AcxiomCRM.Infrastructure;
using AcxiomCRM.Models;

namespace AcxiomCRM.ViewModels;

public class DashboardViewModel
{
    public DateRange Range { get; set; } = new();
    public string ScopeLabel { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;

    // KPI cards (section 17.11)
    public int TotalCustomers { get; set; }
    public int TotalLeads { get; set; }
    public int OpenLeads { get; set; }
    public int TotalOpportunities { get; set; }
    public int OpenOpportunities { get; set; }
    public int WonOpportunities { get; set; }
    public int LostOpportunities { get; set; }
    public decimal TotalPipelineValue { get; set; }
    public decimal WeightedPipelineValue { get; set; }
    public decimal WonAmount { get; set; }
    public int PendingFollowUps { get; set; }
    public int OverdueFollowUps { get; set; }

    // Charts (section 17.12)
    public List<ChartPoint> LeadStatusChart { get; set; } = new();
    public List<StageChartPoint> PipelineChart { get; set; } = new();
    public List<MonthlyChartPoint> MonthlyChart { get; set; } = new();
    public int MonthlyChartMonths { get; set; }

    public List<FollowUp> UpcomingFollowUps { get; set; } = new();
    public List<FollowUp> OverdueFollowUpItems { get; set; } = new();
    public int UpcomingDays { get; set; }

    // Manager / Admin: performance by owner
    public List<OwnerPerformanceRow> TeamPerformance { get; set; } = new();

    // Admin: user / security statistics
    public AdminStats? Admin { get; set; }

    // Sales Executive: recent own activities
    public List<Activity> RecentActivities { get; set; } = new();
}

public record ChartPoint(string Label, int Count);

public record StageChartPoint(string Stage, int Count, decimal Amount);

public record MonthlyChartPoint(string Month, decimal WonAmount, int WonCount, int LostCount);

public record OwnerPerformanceRow(string OwnerName, int OpenLeads, int OpenOpportunities, decimal PipelineValue, decimal WeightedValue, int Won, decimal WonAmount);

public class AdminStats
{
    public int ActiveUsers { get; set; }
    public int InactiveUsers { get; set; }
    public int LockedOutUsers { get; set; }
    public int LoginsToday { get; set; }
    public int FailedLoginsToday { get; set; }
    public List<AuditLog> RecentSecurityEvents { get; set; } = new();
}
