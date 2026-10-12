using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AcxiomCRM.Models;

public class Lead
{
    [Key]
    public int LeadId { get; set; }

    [Required, StringLength(20)]
    public string LeadCode { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string LeadName { get; set; } = string.Empty;

    [StringLength(256)]
    public string? Email { get; set; }

    [StringLength(15)]
    public string? Phone { get; set; }

    [StringLength(150)]
    public string? CompanyName { get; set; }

    public LeadSource Source { get; set; }

    public LeadStatus Status { get; set; } = LeadStatus.New;

    public LeadPriority Priority { get; set; } = LeadPriority.Medium;

    public decimal ExpectedValue { get; set; }

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

    /// <summary>Set when the lead is converted (lead-to-customer workflow).</summary>
    public DateTime? ConvertedDate { get; set; }

    public int? ConvertedCustomerId { get; set; }

    [ForeignKey(nameof(ConvertedCustomerId))]
    public Customer? ConvertedCustomer { get; set; }
}
