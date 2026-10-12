using AcxiomCRM.Models;

namespace AcxiomCRM.Services;

/// <summary>
/// Configured lead status workflow (section 4.5). Converted can only be reached through the
/// conversion action; Converted and Lost are final.
/// </summary>
public static class LeadWorkflow
{
    private static readonly Dictionary<LeadStatus, LeadStatus[]> Transitions = new()
    {
        [LeadStatus.New] = new[] { LeadStatus.Contacted, LeadStatus.Unqualified, LeadStatus.Lost },
        [LeadStatus.Contacted] = new[] { LeadStatus.Qualified, LeadStatus.Unqualified, LeadStatus.Lost },
        [LeadStatus.Qualified] = new[] { LeadStatus.Contacted, LeadStatus.Unqualified, LeadStatus.Lost },
        [LeadStatus.Unqualified] = new[] { LeadStatus.Contacted, LeadStatus.Lost },
        [LeadStatus.Converted] = Array.Empty<LeadStatus>(),
        [LeadStatus.Lost] = Array.Empty<LeadStatus>()
    };

    /// <summary>Statuses a brand-new lead may start with.</summary>
    public static readonly LeadStatus[] InitialStatuses =
        { LeadStatus.New, LeadStatus.Contacted, LeadStatus.Qualified, LeadStatus.Unqualified };

    public static bool IsFinal(LeadStatus status) => status is LeadStatus.Converted or LeadStatus.Lost;

    public static bool CanTransition(LeadStatus from, LeadStatus to) =>
        from == to || Transitions[from].Contains(to);

    /// <summary>Statuses selectable on the edit form for a lead currently in <paramref name="current"/>.</summary>
    public static IEnumerable<LeadStatus> AllowedTargets(LeadStatus current) =>
        new[] { current }.Concat(Transitions[current]);

    public static bool IsOpen(LeadStatus status) =>
        status is LeadStatus.New or LeadStatus.Contacted or LeadStatus.Qualified;
}

/// <summary>
/// Opportunity pipeline: Qualification -> Proposal -> Negotiation -> Won/Lost. Open stages can move
/// forwards or backwards; Won and Lost are final outcomes.
/// </summary>
public static class OpportunityWorkflow
{
    public static readonly OpportunityStage[] OpenStages =
        { OpportunityStage.Qualification, OpportunityStage.Proposal, OpportunityStage.Negotiation };

    public static bool CanTransition(OpportunityStage from, OpportunityStage to) =>
        from == to || !Opportunity.IsClosedStage(from);

    /// <summary>Default probability suggested for each stage (user can override within 0-100).</summary>
    public static int DefaultProbability(OpportunityStage stage) => stage switch
    {
        OpportunityStage.Qualification => 20,
        OpportunityStage.Proposal => 50,
        OpportunityStage.Negotiation => 75,
        OpportunityStage.Won => 100,
        _ => 0
    };
}
