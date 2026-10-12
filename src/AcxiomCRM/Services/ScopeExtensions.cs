using AcxiomCRM.Models;

namespace AcxiomCRM.Services;

/// <summary>
/// Record-level authorization filters. A record is in scope when it is assigned to, or was created by,
/// a user in the caller's scope. A null scope means unrestricted (Admin).
/// These filters are applied on the server for every list, lookup, report and API query.
/// </summary>
public static class ScopeExtensions
{
    public static IQueryable<Customer> InScope(this IQueryable<Customer> query, List<string>? scope) =>
        scope == null ? query : query.Where(x => scope.Contains(x.AssignedTo) || scope.Contains(x.CreatedBy));

    public static IQueryable<Lead> InScope(this IQueryable<Lead> query, List<string>? scope) =>
        scope == null ? query : query.Where(x => scope.Contains(x.AssignedTo) || scope.Contains(x.CreatedBy));

    public static IQueryable<Opportunity> InScope(this IQueryable<Opportunity> query, List<string>? scope) =>
        scope == null ? query : query.Where(x => scope.Contains(x.AssignedTo) || scope.Contains(x.CreatedBy));

    public static IQueryable<FollowUp> InScope(this IQueryable<FollowUp> query, List<string>? scope) =>
        scope == null ? query : query.Where(x => scope.Contains(x.AssignedTo) || scope.Contains(x.CreatedBy));

    public static IQueryable<Activity> InScope(this IQueryable<Activity> query, List<string>? scope) =>
        scope == null ? query : query.Where(x => scope.Contains(x.AssignedTo) || scope.Contains(x.CreatedBy));

    public static bool IsInScope(List<string>? scope, string assignedTo, string createdBy) =>
        scope == null || scope.Contains(assignedTo) || scope.Contains(createdBy);
}
