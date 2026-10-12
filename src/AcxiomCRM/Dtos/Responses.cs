using AcxiomCRM.Models;

namespace AcxiomCRM.Dtos;

// API response models. EF entities are never returned directly, and no password hashes,
// security stamps or tokens are ever included.

public record UserRefDto(string Id, string Name);

public record CustomerDto(
    int CustomerId,
    string CustomerCode,
    string CustomerName,
    string Email,
    string Phone,
    string? CompanyName,
    string? Address,
    string? City,
    string? State,
    CustomerStatus Status,
    string? Notes,
    UserRefDto? AssignedTo,
    UserRefDto? CreatedBy,
    DateTime CreatedDate,
    DateTime? ModifiedDate)
{
    public static CustomerDto From(Customer c) => new(
        c.CustomerId, c.CustomerCode, c.CustomerName, c.Email, c.Phone, c.CompanyName, c.Address, c.City, c.State,
        c.Status, c.Notes, UserRef(c.AssignedTo, c.AssignedToUser), UserRef(c.CreatedBy, c.CreatedByUser), c.CreatedDate, c.ModifiedDate);

    internal static UserRefDto? UserRef(string? id, ApplicationUser? user) =>
        id == null ? null : new UserRefDto(id, user?.FullName ?? string.Empty);
}

public record LeadDto(
    int LeadId,
    string LeadCode,
    string LeadName,
    string? Email,
    string? Phone,
    string? CompanyName,
    LeadSource Source,
    LeadStatus Status,
    LeadPriority Priority,
    decimal ExpectedValue,
    string? Notes,
    UserRefDto? AssignedTo,
    DateTime CreatedDate,
    DateTime? ConvertedDate,
    int? ConvertedCustomerId)
{
    public static LeadDto From(Lead l) => new(
        l.LeadId, l.LeadCode, l.LeadName, l.Email, l.Phone, l.CompanyName, l.Source, l.Status, l.Priority,
        l.ExpectedValue, l.Notes, CustomerDto.UserRef(l.AssignedTo, l.AssignedToUser), l.CreatedDate, l.ConvertedDate, l.ConvertedCustomerId);
}

public record OpportunityDto(
    int OpportunityId,
    string OpportunityName,
    int CustomerId,
    string? CustomerName,
    int? LeadId,
    decimal Amount,
    OpportunityStage Stage,
    int Probability,
    decimal WeightedAmount,
    DateTime ExpectedCloseDate,
    OpportunityStatus Status,
    LeadSource? Source,
    string? Notes,
    UserRefDto? AssignedTo,
    DateTime CreatedDate,
    DateTime? ClosedDate)
{
    public static OpportunityDto From(Opportunity o) => new(
        o.OpportunityId, o.OpportunityName, o.CustomerId, o.Customer?.CustomerName, o.LeadId, o.Amount, o.Stage,
        o.Probability, o.WeightedAmount, o.ExpectedCloseDate, o.Status, o.Source, o.Notes,
        CustomerDto.UserRef(o.AssignedTo, o.AssignedToUser), o.CreatedDate, o.ClosedDate);
}

public record FollowUpDto(
    int FollowUpId,
    string Subject,
    FollowUpType FollowUpType,
    DateTime FollowUpDate,
    FollowUpStatus Status,
    bool IsOverdue,
    string? Remarks,
    string RelatedType,
    int? RelatedId,
    int? CustomerId,
    int? LeadId,
    int? OpportunityId,
    UserRefDto? AssignedTo,
    DateTime? CompletedDate)
{
    public static FollowUpDto From(FollowUp f) => new(
        f.FollowUpId, f.Subject, f.FollowUpType, f.FollowUpDate, f.Status, f.IsOverdue, f.Remarks, f.RelatedType, f.RelatedId,
        f.CustomerId, f.LeadId, f.OpportunityId, CustomerDto.UserRef(f.AssignedTo, f.AssignedToUser), f.CompletedDate);
}

public record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount, int TotalPages);

public record ApiUserDto(string Id, string FullName, string Email, string Role);
