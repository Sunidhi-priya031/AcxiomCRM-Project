using AcxiomCRM.Dtos;
using AcxiomCRM.Infrastructure;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AcxiomCRM.ViewModels;

// ---------------------------------------------------------------- Customers

public class CustomerIndexViewModel
{
    public CustomerSearch Search { get; set; } = new();
    public PagedResult<Customer> Results { get; set; } = new();
    public List<UserOption> Owners { get; set; } = new();
    public bool CanDelete { get; set; }
}

public class CustomerFormViewModel : CustomerRequest
{
    public int? CustomerId { get; set; }
    public string? CustomerCode { get; set; }
    public IEnumerable<SelectListItem> AssignableUsers { get; set; } = Array.Empty<SelectListItem>();
    public bool CanChooseAssignee { get; set; }
}

public class CustomerDetailsViewModel
{
    public Customer Customer { get; set; } = null!;
    public List<Opportunity> Opportunities { get; set; } = new();
    public List<FollowUp> FollowUps { get; set; } = new();
    public List<Activity> Activities { get; set; } = new();
    public List<Lead> SourceLeads { get; set; } = new();
    public List<AuditLog> History { get; set; } = new();
    public bool CanDelete { get; set; }
}

public class CustomerDeleteViewModel
{
    public Customer Customer { get; set; } = null!;
    public int RelatedCount { get; set; }
}

// ---------------------------------------------------------------- Leads

public class LeadIndexViewModel
{
    public LeadSearch Search { get; set; } = new();
    public PagedResult<Lead> Results { get; set; } = new();
    public List<UserOption> Owners { get; set; } = new();
    public bool CanDelete { get; set; }
}

public class LeadFormViewModel : LeadRequest
{
    public int? LeadId { get; set; }
    public string? LeadCode { get; set; }
    public IEnumerable<SelectListItem> StatusOptions { get; set; } = Array.Empty<SelectListItem>();
    public IEnumerable<SelectListItem> AssignableUsers { get; set; } = Array.Empty<SelectListItem>();
    public bool CanChooseAssignee { get; set; }
}

public class LeadDetailsViewModel
{
    public Lead Lead { get; set; } = null!;
    public List<Opportunity> Opportunities { get; set; } = new();
    public List<FollowUp> FollowUps { get; set; } = new();
    public List<Activity> Activities { get; set; } = new();
    public List<AuditLog> History { get; set; } = new();
    public bool CanDelete { get; set; }
    public IEnumerable<LeadStatus> NextStatuses { get; set; } = Array.Empty<LeadStatus>();
}

public class LeadConvertViewModel : LeadConversionRequest
{
    public Lead Lead { get; set; } = null!;
    public IEnumerable<SelectListItem> Customers { get; set; } = Array.Empty<SelectListItem>();
    public IEnumerable<SelectListItem> StageOptions { get; set; } = Array.Empty<SelectListItem>();
}

public class LeadDeleteViewModel
{
    public Lead Lead { get; set; } = null!;
    public int FollowUps { get; set; }
    public int Activities { get; set; }
    public bool Blocked { get; set; }
}

// ---------------------------------------------------------------- Opportunities

public class OpportunityIndexViewModel
{
    public OpportunitySearch Search { get; set; } = new();
    public PagedResult<Opportunity> Results { get; set; } = new();
    public List<UserOption> Owners { get; set; } = new();
    public bool CanDelete { get; set; }
}

public class OpportunityFormViewModel : OpportunityRequest
{
    public int? OpportunityId { get; set; }
    public bool IsClosed { get; set; }
    public IEnumerable<SelectListItem> CustomerOptions { get; set; } = Array.Empty<SelectListItem>();
    public IEnumerable<SelectListItem> LeadOptions { get; set; } = Array.Empty<SelectListItem>();
    public IEnumerable<SelectListItem> StageOptions { get; set; } = Array.Empty<SelectListItem>();
    public IEnumerable<SelectListItem> AssignableUsers { get; set; } = Array.Empty<SelectListItem>();
    public bool CanChooseAssignee { get; set; }
}

public class OpportunityDetailsViewModel
{
    public Opportunity Opportunity { get; set; } = null!;
    public List<FollowUp> FollowUps { get; set; } = new();
    public List<AuditLog> History { get; set; } = new();
    public bool CanDelete { get; set; }
}

public class PipelineViewModel
{
    public string? AssignedTo { get; set; }
    public List<UserOption> Owners { get; set; } = new();
    public List<PipelineColumn> Columns { get; set; } = new();
    public decimal TotalAmount { get; set; }
    public decimal TotalWeighted { get; set; }
}

public record PipelineColumn(OpportunityStage Stage, List<Opportunity> Items)
{
    public decimal Amount => Items.Sum(i => i.Amount);
    public decimal Weighted => Items.Sum(i => i.WeightedAmount);
}

// ---------------------------------------------------------------- Follow-ups

public class FollowUpIndexViewModel
{
    public FollowUpSearch Search { get; set; } = new();
    public PagedResult<FollowUp> Results { get; set; } = new();
    public List<UserOption> Owners { get; set; } = new();
    public int OverdueCount { get; set; }
    public int TodayCount { get; set; }
    public int UpcomingCount { get; set; }
    public int PendingCount { get; set; }
}

public class FollowUpFormViewModel : FollowUpRequest
{
    public int? FollowUpId { get; set; }

    /// <summary>"Customer", "Lead" or "Opportunity" - which related record the form links to.</summary>
    public string RelatedType { get; set; } = "Customer";
    public IEnumerable<SelectListItem> CustomerOptions { get; set; } = Array.Empty<SelectListItem>();
    public IEnumerable<SelectListItem> LeadOptions { get; set; } = Array.Empty<SelectListItem>();
    public IEnumerable<SelectListItem> OpportunityOptions { get; set; } = Array.Empty<SelectListItem>();
    public IEnumerable<SelectListItem> AssignableUsers { get; set; } = Array.Empty<SelectListItem>();
    public bool CanChooseAssignee { get; set; }
}

public class FollowUpDetailsViewModel
{
    public FollowUp FollowUp { get; set; } = null!;
    public List<AuditLog> History { get; set; } = new();
    public FollowUpRescheduleRequest Reschedule { get; set; } = new();
    public FollowUpCompleteRequest Complete { get; set; } = new();
}

// ---------------------------------------------------------------- Activities

public class ActivityIndexViewModel
{
    public ActivitySearch Search { get; set; } = new();
    public PagedResult<Activity> Results { get; set; } = new();
    public List<UserOption> Owners { get; set; } = new();
    public bool CanDelete { get; set; }
}

public class ActivityFormViewModel : ActivityRequest
{
    public int? ActivityId { get; set; }
    public string RelatedType { get; set; } = "Customer";
    public IEnumerable<SelectListItem> CustomerOptions { get; set; } = Array.Empty<SelectListItem>();
    public IEnumerable<SelectListItem> LeadOptions { get; set; } = Array.Empty<SelectListItem>();
    public IEnumerable<SelectListItem> AssignableUsers { get; set; } = Array.Empty<SelectListItem>();
    public bool CanChooseAssignee { get; set; }
}

// ---------------------------------------------------------------- Audit

public class AuditIndexViewModel
{
    public ReportFilter Filter { get; set; } = new();
    public PagedResult<AuditLog> Results { get; set; } = new();
    public List<UserOption> Users { get; set; } = new();
}
