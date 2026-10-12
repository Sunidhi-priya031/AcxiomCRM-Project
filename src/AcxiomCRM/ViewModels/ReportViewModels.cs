using System.ComponentModel.DataAnnotations;
using AcxiomCRM.Infrastructure;
using AcxiomCRM.Services;

namespace AcxiomCRM.ViewModels;

/// <summary>Common report filters bound from the query string.</summary>
public class ReportFilter
{
    [DataType(DataType.Date)]
    public DateTime? From { get; set; }

    [DataType(DataType.Date)]
    public DateTime? To { get; set; }

    public string? AssignedTo { get; set; }
    public string? Status { get; set; }
    public string? Stage { get; set; }
    public string? Source { get; set; }
    public string? Action { get; set; }
    public string? Entity { get; set; }
    public string? UserId { get; set; }
    public string? Q { get; set; }
    public string? Sort { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }
}

public class ReportPage<TRow>
{
    public string Key { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string DateFilterLabel { get; init; } = "Created";
    public ReportFilter Filter { get; init; } = new();
    public PagedResult<TRow> Rows { get; init; } = new();
    public List<SummaryItem> Summary { get; init; } = new();
    public List<UserOption> Owners { get; init; } = new();
}

public record SummaryItem(string Label, string Value, string? Hint = null);

public record ReportLink(string Action, string Title, string Description, string Access);

// ----- Row types. [Display(Name)] supplies the CSV/table header. -----

public class CustomerReportRow
{
    [Display(Name = "Code")] public string Code { get; set; } = "";
    [Display(Name = "Customer")] public string Name { get; set; } = "";
    [Display(Name = "Company")] public string? Company { get; set; }
    [Display(Name = "Email")] public string Email { get; set; } = "";
    [Display(Name = "Phone")] public string Phone { get; set; } = "";
    [Display(Name = "City")] public string? City { get; set; }
    [Display(Name = "Status")] public string Status { get; set; } = "";
    [Display(Name = "Owner")] public string Owner { get; set; } = "";
    [Display(Name = "Created")] public DateTime CreatedDate { get; set; }
    [CsvIgnore] public int Id { get; set; }
}

public class LeadReportRow
{
    [Display(Name = "Code")] public string Code { get; set; } = "";
    [Display(Name = "Lead")] public string Name { get; set; } = "";
    [Display(Name = "Company")] public string? Company { get; set; }
    [Display(Name = "Source")] public string Source { get; set; } = "";
    [Display(Name = "Status")] public string Status { get; set; } = "";
    [Display(Name = "Priority")] public string Priority { get; set; } = "";
    [Display(Name = "Owner")] public string Owner { get; set; } = "";
    [Display(Name = "Expected Value")] public decimal ExpectedValue { get; set; }
    [Display(Name = "Created")] public DateTime CreatedDate { get; set; }
    [Display(Name = "Converted")] public bool Converted { get; set; }
    [Display(Name = "Converted On")] public DateTime? ConvertedDate { get; set; }
    [CsvIgnore] public int Id { get; set; }
}

public class FollowUpReportRow
{
    [Display(Name = "Date")] public DateTime Date { get; set; }
    [Display(Name = "Subject")] public string Subject { get; set; } = "";
    [Display(Name = "Type")] public string Type { get; set; } = "";
    [Display(Name = "Related To")] public string RelatedType { get; set; } = "";
    [Display(Name = "Related Record")] public string RelatedName { get; set; } = "";
    [Display(Name = "Status")] public string Status { get; set; } = "";
    [Display(Name = "Overdue")] public bool Overdue { get; set; }
    [Display(Name = "Owner")] public string Owner { get; set; } = "";
    [Display(Name = "Completed On")] public DateTime? CompletedDate { get; set; }
    [CsvIgnore] public int Id { get; set; }
}

public class OpportunityReportRow
{
    [Display(Name = "Opportunity")] public string Name { get; set; } = "";
    [Display(Name = "Customer")] public string Customer { get; set; } = "";
    [Display(Name = "Stage")] public string Stage { get; set; } = "";
    [Display(Name = "Status")] public string Status { get; set; } = "";
    [Display(Name = "Amount")] public decimal Amount { get; set; }
    [Display(Name = "Probability %")] public int Probability { get; set; }
    [Display(Name = "Weighted Amount")] public decimal Weighted { get; set; }
    [Display(Name = "Expected Close")] public DateTime ExpectedCloseDate { get; set; }
    [Display(Name = "Owner")] public string Owner { get; set; } = "";
    [Display(Name = "Created")] public DateTime CreatedDate { get; set; }
    [CsvIgnore] public int Id { get; set; }
}

public class PipelineStageRow
{
    [Display(Name = "Stage")] public string Stage { get; set; } = "";
    [Display(Name = "Opportunities")] public int Count { get; set; }
    [Display(Name = "Amount")] public decimal Amount { get; set; }
    [Display(Name = "Weighted Amount")] public decimal Weighted { get; set; }
}

public class PipelineOwnerRow
{
    [Display(Name = "Owner")] public string Owner { get; set; } = "";
    [Display(Name = "Open Opportunities")] public int OpenCount { get; set; }
    [Display(Name = "Pipeline Amount")] public decimal Amount { get; set; }
    [Display(Name = "Weighted Amount")] public decimal Weighted { get; set; }
    [Display(Name = "Won")] public int WonCount { get; set; }
    [Display(Name = "Won Amount")] public decimal WonAmount { get; set; }
    [Display(Name = "Lost")] public int LostCount { get; set; }
}

public class PipelineReport
{
    public ReportFilter Filter { get; init; } = new();
    public List<PipelineStageRow> ByStage { get; init; } = new();
    public PagedResult<PipelineOwnerRow> ByOwner { get; init; } = new();
    public List<SummaryItem> Summary { get; init; } = new();
    public List<UserOption> Owners { get; init; } = new();
}

public class ConversionOwnerRow
{
    [Display(Name = "Owner")] public string Owner { get; set; } = "";
    [Display(Name = "Leads")] public int Leads { get; set; }
    [Display(Name = "Converted")] public int Converted { get; set; }
    [Display(Name = "Not Converted")] public int NotConverted { get; set; }
    [Display(Name = "Conversion Rate %")] public double ConversionRate { get; set; }
    [Display(Name = "Opportunities Won")] public int Won { get; set; }
    [Display(Name = "Opportunities Lost")] public int Lost { get; set; }
    [Display(Name = "Opportunities Open")] public int Open { get; set; }
    [Display(Name = "Win Rate %")] public double WinRate { get; set; }
    [Display(Name = "Won Amount")] public decimal WonAmount { get; set; }
}

public class ConversionSourceRow
{
    [Display(Name = "Source")] public string Source { get; set; } = "";
    [Display(Name = "Leads")] public int Leads { get; set; }
    [Display(Name = "Converted")] public int Converted { get; set; }
    [Display(Name = "Conversion Rate %")] public double ConversionRate { get; set; }
}

public class ConversionReport
{
    public ReportFilter Filter { get; init; } = new();
    public PagedResult<ConversionOwnerRow> ByOwner { get; init; } = new();
    public List<ConversionSourceRow> BySource { get; init; } = new();
    public List<SummaryItem> Summary { get; init; } = new();
    public List<UserOption> Owners { get; init; } = new();
}

public class UserActivityRow
{
    [Display(Name = "User")] public string User { get; set; } = "";
    [Display(Name = "Role")] public string Role { get; set; } = "";
    [Display(Name = "Logins")] public int Logins { get; set; }
    [Display(Name = "Failed Logins")] public int FailedLogins { get; set; }
    [Display(Name = "Creates")] public int Creates { get; set; }
    [Display(Name = "Updates")] public int Updates { get; set; }
    [Display(Name = "Deletes")] public int Deletes { get; set; }
    [Display(Name = "Other Actions")] public int Other { get; set; }
    [Display(Name = "Total Actions")] public int Total { get; set; }
    [Display(Name = "Activities Logged")] public int ActivitiesLogged { get; set; }
    [Display(Name = "Follow-Ups Completed")] public int FollowUpsCompleted { get; set; }
    [Display(Name = "Last Login")] public DateTime? LastLogin { get; set; }
    [CsvIgnore] public string UserId { get; set; } = "";
}

public class AuditReportRow
{
    [Display(Name = "Timestamp")] public DateTime Timestamp { get; set; }
    [Display(Name = "User")] public string User { get; set; } = "";
    [Display(Name = "Action")] public string Action { get; set; } = "";
    [Display(Name = "Entity")] public string Entity { get; set; } = "";
    [Display(Name = "Record Id")] public string? RecordId { get; set; }
    [Display(Name = "Result")] public string Result { get; set; } = "";
    [Display(Name = "IP Address")] public string? IpAddress { get; set; }
    [Display(Name = "Details")] public string? Details { get; set; }
    [CsvIgnore] public long Id { get; set; }
}
