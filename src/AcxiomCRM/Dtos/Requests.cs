using System.ComponentModel.DataAnnotations;
using AcxiomCRM.Models;
using AcxiomCRM.Validation;
using Microsoft.AspNetCore.Mvc;
using static AcxiomCRM.Validation.ValidationRules;

namespace AcxiomCRM.Dtos;

// Request models are shared by the MVC forms (the form view models inherit from them) and the REST API,
// so client-side, server-side and API validation all come from the same attributes.

public class CustomerRequest
{
    [Display(Name = "Customer Name")]
    [Required(ErrorMessage = "Customer Name is required.")]
    [StringLength(NameLength, ErrorMessage = "Customer Name cannot exceed {1} characters.")]
    public string? CustomerName { get; set; }

    [Display(Name = "Email")]
    [Required(ErrorMessage = "Email is required.")]
    [StringLength(EmailLength, ErrorMessage = "Email cannot exceed {1} characters.")]
    [RegularExpression(EmailPattern, ErrorMessage = EmailMessage)]
    [Remote("CheckEmail", "Customers", AdditionalFields = "CustomerId", ErrorMessage = "A customer with this email already exists.")]
    public string? Email { get; set; }

    [Display(Name = "Phone")]
    [Required(ErrorMessage = "Phone is required.")]
    [RegularExpression(PhonePattern, ErrorMessage = PhoneMessage)]
    [Remote("CheckPhone", "Customers", AdditionalFields = "CustomerId", ErrorMessage = "A customer with this phone number already exists.")]
    public string? Phone { get; set; }

    [Display(Name = "Company Name")]
    [StringLength(CompanyLength, ErrorMessage = "Company Name cannot exceed {1} characters.")]
    public string? CompanyName { get; set; }

    [StringLength(250, ErrorMessage = "Address cannot exceed {1} characters.")]
    public string? Address { get; set; }

    [StringLength(100, ErrorMessage = "City cannot exceed {1} characters.")]
    public string? City { get; set; }

    [StringLength(100, ErrorMessage = "State cannot exceed {1} characters.")]
    public string? State { get; set; }

    [ValidEnum]
    public CustomerStatus Status { get; set; } = CustomerStatus.Active;

    [StringLength(NotesLength, ErrorMessage = "Notes cannot exceed {1} characters.")]
    public string? Notes { get; set; }

    /// <summary>User id of the assigned sales executive. Defaults to the current user.</summary>
    [Display(Name = "Assigned To")]
    public string? AssignedTo { get; set; }
}

public class LeadRequest
{
    [Display(Name = "Lead Name")]
    [Required(ErrorMessage = "Lead Name is required.")]
    [StringLength(NameLength, ErrorMessage = "Lead Name cannot exceed {1} characters.")]
    public string? LeadName { get; set; }

    [StringLength(EmailLength, ErrorMessage = "Email cannot exceed {1} characters.")]
    [RegularExpression(EmailPattern, ErrorMessage = EmailMessage)]
    public string? Email { get; set; }

    [RegularExpression(PhonePattern, ErrorMessage = PhoneMessage)]
    public string? Phone { get; set; }

    [Display(Name = "Company Name")]
    [StringLength(CompanyLength, ErrorMessage = "Company Name cannot exceed {1} characters.")]
    public string? CompanyName { get; set; }

    [Required(ErrorMessage = "Source is required.")]
    [ValidEnum]
    public LeadSource? Source { get; set; }

    [Display(Name = "Lead Status")]
    [Required(ErrorMessage = "Lead Status is required.")]
    [ValidEnum]
    public LeadStatus? Status { get; set; }

    [Required(ErrorMessage = "Priority is required.")]
    [ValidEnum]
    public LeadPriority? Priority { get; set; }

    [Display(Name = "Expected Value")]
    [Required(ErrorMessage = "Expected Value is required.")]
    [Range(typeof(decimal), "0", MaxMoney, ErrorMessage = "Expected Value must be between 0 and 999,999,999,999.",
        ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    public decimal? ExpectedValue { get; set; }

    [StringLength(NotesLength, ErrorMessage = "Notes cannot exceed {1} characters.")]
    public string? Notes { get; set; }

    [Display(Name = "Assigned To")]
    public string? AssignedTo { get; set; }
}

public class OpportunityRequest
{
    [Display(Name = "Opportunity Name")]
    [Required(ErrorMessage = "Opportunity Name is required.")]
    [StringLength(150, ErrorMessage = "Opportunity Name cannot exceed {1} characters.")]
    public string? OpportunityName { get; set; }

    [Display(Name = "Customer")]
    [Required(ErrorMessage = "Customer is required.")]
    public int? CustomerId { get; set; }

    [Display(Name = "Lead")]
    public int? LeadId { get; set; }

    [Required(ErrorMessage = "Amount is required.")]
    [Range(typeof(decimal), "0.01", MaxMoney, ErrorMessage = AmountMessage,
        ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    public decimal? Amount { get; set; }

    [Required(ErrorMessage = "Stage is required.")]
    [ValidEnum]
    public OpportunityStage? Stage { get; set; }

    [Display(Name = "Probability (%)")]
    [Required(ErrorMessage = "Probability is required.")]
    [Range(0, 100, ErrorMessage = ProbabilityMessage)]
    public int? Probability { get; set; }

    [Display(Name = "Expected Close Date")]
    [Required(ErrorMessage = "Expected Close Date is required.")]
    [DataType(DataType.Date)]
    [NotInPast(UnlessProperty = nameof(Stage), UnlessValues = "Won,Lost", ErrorMessage = CloseDateMessage)]
    public DateTime? ExpectedCloseDate { get; set; }

    [ValidEnum]
    public LeadSource? Source { get; set; }

    [StringLength(NotesLength, ErrorMessage = "Notes cannot exceed {1} characters.")]
    public string? Notes { get; set; }

    [Display(Name = "Assigned To")]
    public string? AssignedTo { get; set; }
}

public class FollowUpRequest
{
    [Required(ErrorMessage = "Subject is required.")]
    [StringLength(150, ErrorMessage = "Subject cannot exceed {1} characters.")]
    public string? Subject { get; set; }

    [Display(Name = "Follow-Up Type")]
    [Required(ErrorMessage = "Follow-Up Type is required.")]
    [ValidEnum]
    public FollowUpType? FollowUpType { get; set; }

    [Display(Name = "Follow-Up Date")]
    [Required(ErrorMessage = "Follow-Up Date is required.")]
    [NotInPast(ErrorMessage = FollowUpDateMessage)]
    public DateTime? FollowUpDate { get; set; }

    [StringLength(NotesLength, ErrorMessage = "Remarks cannot exceed {1} characters.")]
    public string? Remarks { get; set; }

    [Display(Name = "Customer")]
    public int? CustomerId { get; set; }

    [Display(Name = "Lead")]
    public int? LeadId { get; set; }

    [Display(Name = "Opportunity")]
    public int? OpportunityId { get; set; }

    [Display(Name = "Assigned To")]
    public string? AssignedTo { get; set; }
}

public class FollowUpRescheduleRequest
{
    [Display(Name = "New Follow-Up Date")]
    [Required(ErrorMessage = "New Follow-Up Date is required.")]
    [NotInPast(ErrorMessage = FollowUpDateMessage)]
    public DateTime? NewDate { get; set; }

    [StringLength(500, ErrorMessage = "Reason cannot exceed {1} characters.")]
    public string? Reason { get; set; }
}

public class FollowUpCompleteRequest
{
    [Display(Name = "Outcome / Remarks")]
    [StringLength(NotesLength, ErrorMessage = "Remarks cannot exceed {1} characters.")]
    public string? Outcome { get; set; }
}

public class ActivityRequest
{
    [Display(Name = "Activity Type")]
    [Required(ErrorMessage = "Activity Type is required.")]
    [ValidEnum]
    public ActivityType? ActivityType { get; set; }

    [Required(ErrorMessage = "Subject is required.")]
    [StringLength(150, ErrorMessage = "Subject cannot exceed {1} characters.")]
    public string? Subject { get; set; }

    [StringLength(2000, ErrorMessage = "Description cannot exceed {1} characters.")]
    public string? Description { get; set; }

    [Display(Name = "Activity Date")]
    [Required(ErrorMessage = "Activity Date is required.")]
    public DateTime? ActivityDate { get; set; }

    [Display(Name = "Customer")]
    public int? CustomerId { get; set; }

    [Display(Name = "Lead")]
    public int? LeadId { get; set; }

    [Required(ErrorMessage = "Status is required.")]
    [ValidEnum]
    public ActivityStatus? Status { get; set; }

    [Display(Name = "Assigned To")]
    public string? AssignedTo { get; set; }
}

public class LeadConversionRequest
{
    /// <summary>True: create a new customer from the lead. False: link to an existing customer.</summary>
    public bool CreateNewCustomer { get; set; } = true;

    [Display(Name = "Existing Customer")]
    [RequiredIf(nameof(CreateNewCustomer), false, ErrorMessage = "Select an existing customer.")]
    public int? ExistingCustomerId { get; set; }

    [Display(Name = "Customer Name")]
    [RequiredIf(nameof(CreateNewCustomer), true, ErrorMessage = "Customer Name is required.")]
    [StringLength(NameLength, ErrorMessage = "Customer Name cannot exceed {1} characters.")]
    public string? CustomerName { get; set; }

    [RequiredIf(nameof(CreateNewCustomer), true, ErrorMessage = "Email is required.")]
    [StringLength(EmailLength, ErrorMessage = "Email cannot exceed {1} characters.")]
    [RegularExpression(EmailPattern, ErrorMessage = EmailMessage)]
    public string? Email { get; set; }

    [RequiredIf(nameof(CreateNewCustomer), true, ErrorMessage = "Phone is required.")]
    [RegularExpression(PhonePattern, ErrorMessage = PhoneMessage)]
    public string? Phone { get; set; }

    [Display(Name = "Company Name")]
    [StringLength(CompanyLength, ErrorMessage = "Company Name cannot exceed {1} characters.")]
    public string? CompanyName { get; set; }

    [StringLength(250, ErrorMessage = "Address cannot exceed {1} characters.")]
    public string? Address { get; set; }

    [StringLength(100, ErrorMessage = "City cannot exceed {1} characters.")]
    public string? City { get; set; }

    [StringLength(100, ErrorMessage = "State cannot exceed {1} characters.")]
    public string? State { get; set; }

    public bool CreateOpportunity { get; set; } = true;

    [Display(Name = "Opportunity Name")]
    [RequiredIf(nameof(CreateOpportunity), true, ErrorMessage = "Opportunity Name is required.")]
    [StringLength(150, ErrorMessage = "Opportunity Name cannot exceed {1} characters.")]
    public string? OpportunityName { get; set; }

    [RequiredIf(nameof(CreateOpportunity), true, ErrorMessage = "Amount is required.")]
    [Range(typeof(decimal), "0.01", MaxMoney, ErrorMessage = AmountMessage,
        ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    public decimal? Amount { get; set; }

    [Display(Name = "Probability (%)")]
    [RequiredIf(nameof(CreateOpportunity), true, ErrorMessage = "Probability is required.")]
    [Range(0, 100, ErrorMessage = ProbabilityMessage)]
    public int? Probability { get; set; }

    [Display(Name = "Expected Close Date")]
    [RequiredIf(nameof(CreateOpportunity), true, ErrorMessage = "Expected Close Date is required.")]
    [DataType(DataType.Date)]
    [NotInPast(ErrorMessage = CloseDateMessage)]
    public DateTime? ExpectedCloseDate { get; set; }

    [ValidEnum]
    public OpportunityStage? Stage { get; set; } = OpportunityStage.Qualification;
}

public class ApiLoginRequest
{
    [Required(ErrorMessage = "Email is required.")]
    [StringLength(EmailLength)]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(128)]
    public string? Password { get; set; }
}
