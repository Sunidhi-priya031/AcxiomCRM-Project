using AcxiomCRM.Services;

namespace AcxiomCRM.ViewModels;

/// <summary>Describes which filter controls a report page shows.</summary>
public class ReportFilterView
{
    public ReportFilter Filter { get; init; } = new();
    public List<UserOption> Owners { get; init; } = new();
    public string DateLabel { get; init; } = "Created";
    public bool ShowOwner { get; init; } = true;
    public string OwnerParam { get; init; } = "assignedTo";
    public string[]? Statuses { get; init; }
    public string StatusLabel { get; init; } = "Status";
    public string[]? Stages { get; init; }
    public string[]? Sources { get; init; }
    public string[]? Actions { get; init; }
    public string[]? Entities { get; init; }
    public bool ShowSearch { get; init; }
    public string SearchPlaceholder { get; init; } = "Search";
}

public class ReportHeaderView
{
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public List<SummaryItem> Summary { get; init; } = new();
    public List<(string Label, string Url)> Exports { get; init; } = new();
}
