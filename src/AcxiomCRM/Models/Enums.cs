using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models;

public enum CustomerStatus
{
    Active,
    Inactive
}

public enum LeadStatus
{
    New,
    Contacted,
    Qualified,
    Unqualified,
    Converted,
    Lost
}

public enum LeadSource
{
    Website,
    Referral,
    [Display(Name = "Social Media")] SocialMedia,
    [Display(Name = "Email Campaign")] EmailCampaign,
    [Display(Name = "Cold Call")] ColdCall,
    [Display(Name = "Trade Show")] TradeShow,
    Partner,
    Other
}

public enum LeadPriority
{
    Low,
    Medium,
    High
}

public enum OpportunityStage
{
    Qualification,
    Proposal,
    Negotiation,
    Won,
    Lost
}

public enum OpportunityStatus
{
    Open,
    Won,
    Lost
}

public enum FollowUpType
{
    Call,
    Email,
    Meeting,
    Visit
}

public enum FollowUpStatus
{
    Planned,
    Completed,
    Missed,
    Cancelled
}

public enum ActivityType
{
    Call,
    Meeting,
    Email,
    Task
}

public enum ActivityStatus
{
    Planned,
    Completed,
    Cancelled
}
