using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AcxiomCRM.Models;

public class Customer
{
    [Key]
    public int CustomerId { get; set; }

    [Required, StringLength(20)]
    public string CustomerCode { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string CustomerName { get; set; } = string.Empty;

    /// <summary>Stored trimmed and lower-cased; unique.</summary>
    [Required, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    /// <summary>10-digit mobile number; unique.</summary>
    [Required, StringLength(15)]
    public string Phone { get; set; } = string.Empty;

    [StringLength(150)]
    public string? CompanyName { get; set; }

    [StringLength(250)]
    public string? Address { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    [StringLength(100)]
    public string? State { get; set; }

    public CustomerStatus Status { get; set; } = CustomerStatus.Active;

    [StringLength(1000)]
    public string? Notes { get; set; }

    /// <summary>Assigned sales executive (owner).</summary>
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
}
