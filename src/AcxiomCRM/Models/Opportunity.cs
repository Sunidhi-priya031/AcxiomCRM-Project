using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AcxiomCRM.Models;

public class Opportunity
{
    [Key]
    public int OpportunityId { get; set; }

    [Required, StringLength(150)]
    public string OpportunityName { get; set; } = string.Empty;

    public int CustomerId { get; set; }

    [ForeignKey(nameof(CustomerId))]
    public Customer? Customer { get; set; }

    /// <summary>Originating lead, when the opportunity came from a lead conversion.</summary>
    public int? LeadId { get; set; }

    [ForeignKey(nameof(LeadId))]
    public Lead? Lead { get; set; }

    public decimal Amount { get; set; }

    public OpportunityStage Stage { get; set; } = OpportunityStage.Qualification;

    /// <summary>0-100 inclusive.</summary>
    public int Probability { get; set; }

    public DateTime ExpectedCloseDate { get; set; }

    /// <summary>Derived from Stage: Open for pipeline stages, Won/Lost for closed stages.</summary>
    public OpportunityStatus Status { get; set; } = OpportunityStatus.Open;

    public LeadSource? Source { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    [Required]
    public string AssignedTo { get; set; } = string.Empty;

    [ForeignKey(nameof(AssignedTo))]
    public ApplicationUser? AssignedToUser { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    [Required]
    public string CreatedBy { get; set; } = string.Empty;

    [ForeignKey(nameof(CreatedBy))]
    public ApplicationUser? CreatedByUser { get; set; }

    public DateTime? ModifiedDate { get; set; }

    /// <summary>When the opportunity was marked Won or Lost (drives monthly sales figures).</summary>
    public DateTime? ClosedDate { get; set; }

    /// <summary>Weighted pipeline value = Amount x Probability / 100.</summary>
    [NotMapped]
    public decimal WeightedAmount => Math.Round(Amount * Probability / 100m, 2);

    [NotMapped]
    public bool IsOpen => Status == OpportunityStatus.Open;

    public static bool IsClosedStage(OpportunityStage stage) =>
        stage == OpportunityStage.Won || stage == OpportunityStage.Lost;

    public static OpportunityStatus StatusForStage(OpportunityStage stage) => stage switch
    {
        OpportunityStage.Won => OpportunityStatus.Won,
        OpportunityStage.Lost => OpportunityStatus.Lost,
        _ => OpportunityStatus.Open
    };
}
