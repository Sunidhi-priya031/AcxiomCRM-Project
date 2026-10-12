using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AcxiomCRM.Models;

/// <summary>A logged sales activity (Call, Meeting, Email or Task) against a customer or a lead.</summary>
public class Activity
{
    [Key]
    public int ActivityId { get; set; }

    public ActivityType ActivityType { get; set; }

    [Required, StringLength(150)]
    public string Subject { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    public DateTime ActivityDate { get; set; }

    public int? CustomerId { get; set; }

    [ForeignKey(nameof(CustomerId))]
    public Customer? Customer { get; set; }

    public int? LeadId { get; set; }

    [ForeignKey(nameof(LeadId))]
    public Lead? Lead { get; set; }

    [Required]
    public string AssignedTo { get; set; } = string.Empty;

    [ForeignKey(nameof(AssignedTo))]
    public ApplicationUser? AssignedToUser { get; set; }

    public ActivityStatus Status { get; set; } = ActivityStatus.Planned;

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    [Required]
    public string CreatedBy { get; set; } = string.Empty;

    [ForeignKey(nameof(CreatedBy))]
    public ApplicationUser? CreatedByUser { get; set; }

    public DateTime? ModifiedDate { get; set; }
}
