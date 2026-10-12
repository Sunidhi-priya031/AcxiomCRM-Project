using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AcxiomCRM.Models;

/// <summary>
/// A scheduled follow-up. Each follow-up relates to exactly one customer, lead or opportunity.
/// </summary>
public class FollowUp
{
    [Key]
    public int FollowUpId { get; set; }

    [Required, StringLength(150)]
    public string Subject { get; set; } = string.Empty;

    public FollowUpType FollowUpType { get; set; }

    public DateTime FollowUpDate { get; set; }

    [StringLength(1000)]
    public string? Remarks { get; set; }

    public FollowUpStatus Status { get; set; } = FollowUpStatus.Planned;

    public int? CustomerId { get; set; }

    [ForeignKey(nameof(CustomerId))]
    public Customer? Customer { get; set; }

    public int? LeadId { get; set; }

    [ForeignKey(nameof(LeadId))]
    public Lead? Lead { get; set; }

    public int? OpportunityId { get; set; }

    [ForeignKey(nameof(OpportunityId))]
    public Opportunity? Opportunity { get; set; }

    [Required]
    public string AssignedTo { get; set; } = string.Empty;

    [ForeignKey(nameof(AssignedTo))]
    public ApplicationUser? AssignedToUser { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    [Required]
    public string CreatedBy { get; set; } = string.Empty;

    [ForeignKey(nameof(CreatedBy))]
    public ApplicationUser? CreatedByUser { get; set; }

    public DateTime? CompletedDate { get; set; }

    public DateTime? ModifiedDate { get; set; }

    [NotMapped]
    public bool IsOverdue => Status == FollowUpStatus.Planned && FollowUpDate.Date < DateTime.Today;

    [NotMapped]
    public string RelatedType => CustomerId.HasValue ? "Customer" : LeadId.HasValue ? "Lead" : OpportunityId.HasValue ? "Opportunity" : "-";

    [NotMapped]
    public int? RelatedId => CustomerId ?? LeadId ?? OpportunityId;
}
