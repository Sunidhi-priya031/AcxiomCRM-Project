using AcxiomCRM.Data;
using AcxiomCRM.Infrastructure;
using AcxiomCRM.Models;
using AcxiomCRM.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services;

/// <summary>
/// Report data (section 4.11 / 11). All reports are restricted to the caller's authorised scope.
/// When <c>export</c> is true every matching row is returned (for CSV) instead of a single page.
/// </summary>
public class ReportService
{
    private readonly ApplicationDbContext _db;
    private readonly IUserContext _user;
    private readonly UserDirectoryService _users;

    public ReportService(ApplicationDbContext db, IUserContext user, UserDirectoryService users)
    {
        _db = db;
        _user = user;
        _users = users;
    }

    private static DateTime? FromDate(ReportFilter f) => f.From?.Date;
    private static DateTime? ToExclusive(ReportFilter f) => f.To?.Date.AddDays(1);

    private static int? PageSize(ReportFilter f, bool export) => export ? int.MaxValue : f.PageSize;

    private static PagedResult<T> All<T>(List<T> rows) =>
        new() { Items = rows, Page = 1, PageSize = Math.Max(rows.Count, 1), TotalCount = rows.Count };

    private static string Money(decimal value) => "₹" + value.ToString("N0");

    private static string Pct(int part, int total) => total == 0 ? "0%" : $"{Math.Round(part * 100.0 / total, 1)}%";

    // ---------------------------------------------------------------- Customer report

    public async Task<ReportPage<CustomerReportRow>> CustomerReportAsync(ReportFilter f, bool export = false)
    {
        var scope = await _user.GetScopeUserIdsAsync();
        var query = _db.Customers.InScope(scope).AsNoTracking();
        var from = FromDate(f);
        var to = ToExclusive(f);
        if (from.HasValue) query = query.Where(c => c.CreatedDate >= from);
        if (to.HasValue) query = query.Where(c => c.CreatedDate < to);
        if (!string.IsNullOrEmpty(f.AssignedTo)) query = query.Where(c => c.AssignedTo == f.AssignedTo);
        if (Enum.TryParse<CustomerStatus>(f.Status, out var status)) query = query.Where(c => c.Status == status);
        if (!string.IsNullOrWhiteSpace(f.Q))
        {
            var term = f.Q.Trim().ToLower();
            query = query.Where(c => c.CustomerName.ToLower().Contains(term) || (c.CompanyName != null && c.CompanyName.ToLower().Contains(term)));
        }

        var statusCounts = await query.GroupBy(c => c.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
        query = query.Include(c => c.AssignedToUser);

        query = f.Sort switch
        {
            "name" => query.OrderBy(c => c.CustomerName),
            "name_desc" => query.OrderByDescending(c => c.CustomerName),
            "status" => query.OrderBy(c => c.Status),
            "owner" => query.OrderBy(c => c.AssignedToUser!.FullName),
            "created" => query.OrderBy(c => c.CreatedDate),
            _ => query.OrderByDescending(c => c.CreatedDate)
        };

        var page = await query.ToPagedResultAsync(f.Page, PageSize(f, export));
        var rows = page.Items.Select(c => new CustomerReportRow
        {
            Id = c.CustomerId, Code = c.CustomerCode, Name = c.CustomerName, Company = c.CompanyName, Email = c.Email,
            Phone = c.Phone, City = c.City, Status = c.Status.ToString(), Owner = c.AssignedToUser?.FullName ?? "-", CreatedDate = c.CreatedDate
        }).ToList();

        var total = statusCounts.Sum(s => s.Count);
        return new ReportPage<CustomerReportRow>
        {
            Key = "Customers",
            Title = "Customer Report",
            Description = "Customer list with status, owner and creation date.",
            Filter = f,
            Rows = new PagedResult<CustomerReportRow> { Items = rows, Page = page.Page, PageSize = page.PageSize, TotalCount = page.TotalCount },
            Summary = new()
            {
                new("Customers", total.ToString()),
                new("Active", statusCounts.Where(s => s.Key == CustomerStatus.Active).Sum(s => s.Count).ToString()),
                new("Inactive", statusCounts.Where(s => s.Key == CustomerStatus.Inactive).Sum(s => s.Count).ToString())
            },
            Owners = await _users.GetScopeUsersAsync()
        };
    }

    // ---------------------------------------------------------------- Lead report

    public async Task<ReportPage<LeadReportRow>> LeadReportAsync(ReportFilter f, bool export = false)
    {
        var scope = await _user.GetScopeUserIdsAsync();
        var query = _db.Leads.InScope(scope).AsNoTracking();
        var from = FromDate(f);
        var to = ToExclusive(f);
        if (from.HasValue) query = query.Where(l => l.CreatedDate >= from);
        if (to.HasValue) query = query.Where(l => l.CreatedDate < to);
        if (!string.IsNullOrEmpty(f.AssignedTo)) query = query.Where(l => l.AssignedTo == f.AssignedTo);
        if (Enum.TryParse<LeadStatus>(f.Status, out var status)) query = query.Where(l => l.Status == status);
        if (Enum.TryParse<LeadSource>(f.Source, out var source)) query = query.Where(l => l.Source == source);

        var counts = await query.GroupBy(l => l.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
        query = query.Include(l => l.AssignedToUser);

        query = f.Sort switch
        {
            "name" => query.OrderBy(l => l.LeadName),
            "source" => query.OrderBy(l => l.Source),
            "status" => query.OrderBy(l => l.Status),
            "value" => query.OrderBy(l => l.ExpectedValue),
            "value_desc" => query.OrderByDescending(l => l.ExpectedValue),
            "created" => query.OrderBy(l => l.CreatedDate),
            _ => query.OrderByDescending(l => l.CreatedDate)
        };

        var page = await query.ToPagedResultAsync(f.Page, PageSize(f, export));
        var rows = page.Items.Select(l => new LeadReportRow
        {
            Id = l.LeadId, Code = l.LeadCode, Name = l.LeadName, Company = l.CompanyName, Source = l.Source.GetDisplayName(),
            Status = l.Status.ToString(), Priority = l.Priority.ToString(), Owner = l.AssignedToUser?.FullName ?? "-",
            ExpectedValue = l.ExpectedValue, CreatedDate = l.CreatedDate, Converted = l.Status == LeadStatus.Converted,
            ConvertedDate = l.ConvertedDate
        }).ToList();

        var total = counts.Sum(c => c.Count);
        var converted = counts.Where(c => c.Key == LeadStatus.Converted).Sum(c => c.Count);
        return new ReportPage<LeadReportRow>
        {
            Key = "Leads",
            Title = "Lead Report",
            Description = "Leads by source, status and owner, with conversion information.",
            Filter = f,
            Rows = new PagedResult<LeadReportRow> { Items = rows, Page = page.Page, PageSize = page.PageSize, TotalCount = page.TotalCount },
            Summary = new()
            {
                new("Leads", total.ToString()),
                new("Open", counts.Where(c => LeadWorkflow.IsOpen(c.Key)).Sum(c => c.Count).ToString()),
                new("Converted", converted.ToString(), Pct(converted, total) + " conversion"),
                new("Lost / Unqualified", counts.Where(c => c.Key is LeadStatus.Lost or LeadStatus.Unqualified).Sum(c => c.Count).ToString())
            },
            Owners = await _users.GetScopeUsersAsync()
        };
    }

    // ---------------------------------------------------------------- Follow-up report

    public async Task<ReportPage<FollowUpReportRow>> FollowUpReportAsync(ReportFilter f, bool export = false)
    {
        var scope = await _user.GetScopeUserIdsAsync();
        var today = DateTime.Today;
        var query = _db.FollowUps.InScope(scope).AsNoTracking();
        var from = FromDate(f);
        var to = ToExclusive(f);
        if (from.HasValue) query = query.Where(x => x.FollowUpDate >= from);
        if (to.HasValue) query = query.Where(x => x.FollowUpDate < to);
        if (!string.IsNullOrEmpty(f.AssignedTo)) query = query.Where(x => x.AssignedTo == f.AssignedTo);

        var statusRows = await query.Select(x => new { x.Status, x.FollowUpDate }).ToListAsync();
        query = query.Include(x => x.Customer).Include(x => x.Lead).Include(x => x.Opportunity).Include(x => x.AssignedToUser);

        if (f.Status == "Overdue")
        {
            query = query.Where(x => x.Status == FollowUpStatus.Planned && x.FollowUpDate < today);
        }
        else if (Enum.TryParse<FollowUpStatus>(f.Status, out var status))
        {
            query = query.Where(x => x.Status == status);
        }

        query = f.Sort switch
        {
            "date_desc" => query.OrderByDescending(x => x.FollowUpDate),
            "status" => query.OrderBy(x => x.Status),
            "owner" => query.OrderBy(x => x.AssignedToUser!.FullName),
            _ => query.OrderBy(x => x.FollowUpDate)
        };

        var page = await query.ToPagedResultAsync(f.Page, PageSize(f, export));
        var rows = page.Items.Select(x => new FollowUpReportRow
        {
            Id = x.FollowUpId, Date = x.FollowUpDate, Subject = x.Subject, Type = x.FollowUpType.ToString(),
            RelatedType = x.RelatedType,
            RelatedName = x.Customer?.CustomerName ?? x.Lead?.LeadName ?? x.Opportunity?.OpportunityName ?? "-",
            Status = x.Status.ToString(), Overdue = x.IsOverdue, Owner = x.AssignedToUser?.FullName ?? "-", CompletedDate = x.CompletedDate
        }).ToList();

        return new ReportPage<FollowUpReportRow>
        {
            Key = "FollowUps",
            Title = "Follow-Up Report",
            Description = "Planned, completed, missed and overdue follow-ups.",
            DateFilterLabel = "Follow-up date",
            Filter = f,
            Rows = new PagedResult<FollowUpReportRow> { Items = rows, Page = page.Page, PageSize = page.PageSize, TotalCount = page.TotalCount },
            Summary = new()
            {
                new("Planned", statusRows.Count(s => s.Status == FollowUpStatus.Planned).ToString()),
                new("Completed", statusRows.Count(s => s.Status == FollowUpStatus.Completed).ToString()),
                new("Missed", statusRows.Count(s => s.Status == FollowUpStatus.Missed).ToString()),
                new("Cancelled", statusRows.Count(s => s.Status == FollowUpStatus.Cancelled).ToString()),
                new("Overdue", statusRows.Count(s => s.Status == FollowUpStatus.Planned && s.FollowUpDate < today).ToString())
            },
            Owners = await _users.GetScopeUsersAsync()
        };
    }

    // ---------------------------------------------------------------- Opportunity report

    public async Task<ReportPage<OpportunityReportRow>> OpportunityReportAsync(ReportFilter f, bool export = false)
    {
        var scope = await _user.GetScopeUserIdsAsync();
        var query = _db.Opportunities.InScope(scope).AsNoTracking();
        var from = FromDate(f);
        var to = ToExclusive(f);
        if (from.HasValue) query = query.Where(o => o.ExpectedCloseDate >= from);
        if (to.HasValue) query = query.Where(o => o.ExpectedCloseDate < to);
        if (!string.IsNullOrEmpty(f.AssignedTo)) query = query.Where(o => o.AssignedTo == f.AssignedTo);
        if (Enum.TryParse<OpportunityStage>(f.Stage, out var stage)) query = query.Where(o => o.Stage == stage);
        if (Enum.TryParse<OpportunityStatus>(f.Status, out var status)) query = query.Where(o => o.Status == status);

        var totals = await query.Select(o => new { o.Status, o.Amount, o.Probability }).ToListAsync();
        query = query.Include(o => o.Customer).Include(o => o.AssignedToUser);

        query = f.Sort switch
        {
            "name" => query.OrderBy(o => o.OpportunityName),
            "stage" => query.OrderBy(o => o.Stage),
            "amount" => query.OrderBy(o => o.Amount),
            "amount_desc" => query.OrderByDescending(o => o.Amount),
            "probability" => query.OrderByDescending(o => o.Probability),
            "close_desc" => query.OrderByDescending(o => o.ExpectedCloseDate),
            _ => query.OrderBy(o => o.ExpectedCloseDate)
        };

        var page = await query.ToPagedResultAsync(f.Page, PageSize(f, export));
        var rows = page.Items.Select(o => new OpportunityReportRow
        {
            Id = o.OpportunityId, Name = o.OpportunityName, Customer = o.Customer?.CustomerName ?? "-", Stage = o.Stage.ToString(),
            Status = o.Status.ToString(), Amount = o.Amount, Probability = o.Probability, Weighted = o.WeightedAmount,
            ExpectedCloseDate = o.ExpectedCloseDate, Owner = o.AssignedToUser?.FullName ?? "-", CreatedDate = o.CreatedDate
        }).ToList();

        var open = totals.Where(t => t.Status == OpportunityStatus.Open).ToList();
        return new ReportPage<OpportunityReportRow>
        {
            Key = "Opportunities",
            Title = "Opportunity Report",
            Description = "Opportunities with stage, amount, probability and expected close date.",
            DateFilterLabel = "Expected close",
            Filter = f,
            Rows = new PagedResult<OpportunityReportRow> { Items = rows, Page = page.Page, PageSize = page.PageSize, TotalCount = page.TotalCount },
            Summary = new()
            {
                new("Opportunities", totals.Count.ToString()),
                new("Total amount", Money(totals.Sum(t => t.Amount))),
                new("Open pipeline", Money(open.Sum(t => t.Amount))),
                new("Weighted pipeline", Money(open.Sum(t => Math.Round(t.Amount * t.Probability / 100m, 2))))
            },
            Owners = await _users.GetScopeUsersAsync()
        };
    }

    // ---------------------------------------------------------------- Pipeline report

    public async Task<PipelineReport> PipelineReportAsync(ReportFilter f, bool export = false)
    {
        var scope = await _user.GetScopeUserIdsAsync();
        var query = _db.Opportunities.InScope(scope).AsNoTracking();
        var from = FromDate(f);
        var to = ToExclusive(f);
        if (from.HasValue) query = query.Where(o => o.CreatedDate >= from);
        if (to.HasValue) query = query.Where(o => o.CreatedDate < to);
        if (!string.IsNullOrEmpty(f.AssignedTo)) query = query.Where(o => o.AssignedTo == f.AssignedTo);

        var data = await query.Select(o => new { o.Stage, o.Status, o.Amount, o.Probability, o.AssignedTo }).ToListAsync();
        var owners = await _users.GetScopeUsersAsync();

        var byStage = Enum.GetValues<OpportunityStage>().Select(stage =>
        {
            var items = data.Where(d => d.Stage == stage).ToList();
            return new PipelineStageRow
            {
                Stage = stage.ToString(),
                Count = items.Count,
                Amount = items.Sum(i => i.Amount),
                Weighted = items.Sum(i => Math.Round(i.Amount * i.Probability / 100m, 2))
            };
        }).ToList();

        var ownerRows = owners.Select(u =>
        {
            var mine = data.Where(d => d.AssignedTo == u.Id).ToList();
            var open = mine.Where(d => d.Status == OpportunityStatus.Open).ToList();
            var won = mine.Where(d => d.Status == OpportunityStatus.Won).ToList();
            return new PipelineOwnerRow
            {
                Owner = u.FullName,
                OpenCount = open.Count,
                Amount = open.Sum(d => d.Amount),
                Weighted = open.Sum(d => Math.Round(d.Amount * d.Probability / 100m, 2)),
                WonCount = won.Count,
                WonAmount = won.Sum(d => d.Amount),
                LostCount = mine.Count(d => d.Status == OpportunityStatus.Lost)
            };
        }).Where(r => r.OpenCount + r.WonCount + r.LostCount > 0);

        ownerRows = f.Sort switch
        {
            "owner" => ownerRows.OrderBy(r => r.Owner),
            "weighted" => ownerRows.OrderByDescending(r => r.Weighted),
            "won" => ownerRows.OrderByDescending(r => r.WonAmount),
            _ => ownerRows.OrderByDescending(r => r.Amount)
        };
        var ownerList = ownerRows.ToList();

        var openAll = data.Where(d => d.Status == OpportunityStatus.Open).ToList();
        return new PipelineReport
        {
            Filter = f,
            ByStage = byStage,
            ByOwner = export ? All(ownerList) : ownerList.ToPagedResult(f.Page, f.PageSize),
            Owners = owners,
            Summary = new()
            {
                new("Open opportunities", openAll.Count.ToString()),
                new("Pipeline amount", Money(openAll.Sum(d => d.Amount))),
                new("Weighted pipeline", Money(openAll.Sum(d => Math.Round(d.Amount * d.Probability / 100m, 2))), "Amount × Probability / 100"),
                new("Won amount", Money(data.Where(d => d.Status == OpportunityStatus.Won).Sum(d => d.Amount)))
            }
        };
    }

    // ---------------------------------------------------------------- Conversion report

    public async Task<ConversionReport> ConversionReportAsync(ReportFilter f, bool export = false)
    {
        var scope = await _user.GetScopeUserIdsAsync();
        var from = FromDate(f);
        var to = ToExclusive(f);
        var leadQuery = _db.Leads.InScope(scope).AsNoTracking();
        var oppQuery = _db.Opportunities.InScope(scope).AsNoTracking();
        if (from.HasValue)
        {
            leadQuery = leadQuery.Where(l => l.CreatedDate >= from);
            oppQuery = oppQuery.Where(o => o.CreatedDate >= from);
        }

        if (to.HasValue)
        {
            leadQuery = leadQuery.Where(l => l.CreatedDate < to);
            oppQuery = oppQuery.Where(o => o.CreatedDate < to);
        }

        if (!string.IsNullOrEmpty(f.AssignedTo))
        {
            leadQuery = leadQuery.Where(l => l.AssignedTo == f.AssignedTo);
            oppQuery = oppQuery.Where(o => o.AssignedTo == f.AssignedTo);
        }

        var leads = await leadQuery.Select(l => new { l.Status, l.Source, l.AssignedTo }).ToListAsync();
        var opps = await oppQuery.Select(o => new { o.Status, o.Amount, o.AssignedTo }).ToListAsync();
        var owners = await _users.GetScopeUsersAsync();

        static double Rate(int part, int total) => total == 0 ? 0 : Math.Round(part * 100.0 / total, 1);

        var ownerRows = owners.Select(u =>
        {
            var myLeads = leads.Where(l => l.AssignedTo == u.Id).ToList();
            var myOpps = opps.Where(o => o.AssignedTo == u.Id).ToList();
            var converted = myLeads.Count(l => l.Status == LeadStatus.Converted);
            var won = myOpps.Count(o => o.Status == OpportunityStatus.Won);
            var lost = myOpps.Count(o => o.Status == OpportunityStatus.Lost);
            return new ConversionOwnerRow
            {
                Owner = u.FullName,
                Leads = myLeads.Count,
                Converted = converted,
                NotConverted = myLeads.Count - converted,
                ConversionRate = Rate(converted, myLeads.Count),
                Won = won,
                Lost = lost,
                Open = myOpps.Count(o => o.Status == OpportunityStatus.Open),
                WinRate = Rate(won, won + lost),
                WonAmount = myOpps.Where(o => o.Status == OpportunityStatus.Won).Sum(o => o.Amount)
            };
        }).Where(r => r.Leads + r.Won + r.Lost + r.Open > 0);

        ownerRows = f.Sort switch
        {
            "owner" => ownerRows.OrderBy(r => r.Owner),
            "winrate" => ownerRows.OrderByDescending(r => r.WinRate),
            "won" => ownerRows.OrderByDescending(r => r.WonAmount),
            _ => ownerRows.OrderByDescending(r => r.ConversionRate)
        };
        var ownerList = ownerRows.ToList();

        var bySource = Enum.GetValues<LeadSource>().Select(s =>
        {
            var items = leads.Where(l => l.Source == s).ToList();
            var converted = items.Count(l => l.Status == LeadStatus.Converted);
            return new ConversionSourceRow { Source = s.GetDisplayName(), Leads = items.Count, Converted = converted, ConversionRate = Rate(converted, items.Count) };
        }).Where(r => r.Leads > 0).OrderByDescending(r => r.Leads).ToList();

        var totalConverted = leads.Count(l => l.Status == LeadStatus.Converted);
        var totalWon = opps.Count(o => o.Status == OpportunityStatus.Won);
        var totalLost = opps.Count(o => o.Status == OpportunityStatus.Lost);
        return new ConversionReport
        {
            Filter = f,
            ByOwner = export ? All(ownerList) : ownerList.ToPagedResult(f.Page, f.PageSize),
            BySource = bySource,
            Owners = owners,
            Summary = new()
            {
                new("Leads", leads.Count.ToString()),
                new("Converted", totalConverted.ToString(), Pct(totalConverted, leads.Count) + " conversion rate"),
                new("Not converted", (leads.Count - totalConverted).ToString()),
                new("Opportunities won / lost", $"{totalWon} / {totalLost}", Pct(totalWon, totalWon + totalLost) + " win rate"),
                new("Won amount", Money(opps.Where(o => o.Status == OpportunityStatus.Won).Sum(o => o.Amount)))
            }
        };
    }

    // ---------------------------------------------------------------- User activity report (Admin / Manager)

    public async Task<ReportPage<UserActivityRow>> UserActivityReportAsync(ReportFilter f, bool export = false)
    {
        var owners = await _users.GetScopeUsersAsync();
        var ids = owners.Select(o => o.Id).ToList();
        var from = FromDate(f);
        var to = ToExclusive(f);

        var auditQuery = _db.AuditLogs.AsNoTracking().Where(a => a.UserId != null && ids.Contains(a.UserId));
        var activityQuery = _db.Activities.AsNoTracking().Where(a => ids.Contains(a.CreatedBy));
        var followUpQuery = _db.FollowUps.AsNoTracking().Where(x => x.Status == FollowUpStatus.Completed && ids.Contains(x.AssignedTo));
        if (from.HasValue)
        {
            auditQuery = auditQuery.Where(a => a.CreatedDate >= from);
            activityQuery = activityQuery.Where(a => a.CreatedDate >= from);
            followUpQuery = followUpQuery.Where(x => x.CompletedDate >= from);
        }

        if (to.HasValue)
        {
            auditQuery = auditQuery.Where(a => a.CreatedDate < to);
            activityQuery = activityQuery.Where(a => a.CreatedDate < to);
            followUpQuery = followUpQuery.Where(x => x.CompletedDate < to);
        }

        var actions = await auditQuery.GroupBy(a => new { a.UserId, a.Action })
            .Select(g => new { g.Key.UserId, g.Key.Action, Count = g.Count() }).ToListAsync();
        var activityCounts = await activityQuery.GroupBy(a => a.CreatedBy).Select(g => new { UserId = g.Key, Count = g.Count() }).ToListAsync();
        var followUpCounts = await followUpQuery.GroupBy(x => x.AssignedTo).Select(g => new { UserId = g.Key, Count = g.Count() }).ToListAsync();
        var lastLogins = await _db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).Select(u => new { u.Id, u.LastLoginDate }).ToListAsync();

        var source = owners.Where(u => string.IsNullOrEmpty(f.UserId) || u.Id == f.UserId).Select(u =>
        {
            var mine = actions.Where(a => a.UserId == u.Id).ToList();
            int Count(params string[] names) => mine.Where(a => names.Contains(a.Action)).Sum(a => a.Count);
            var total = mine.Sum(a => a.Count);
            var logins = Count(AuditActions.Login);
            var failed = Count(AuditActions.LoginFailed, AuditActions.Lockout);
            var creates = Count(AuditActions.Create);
            var updates = Count(AuditActions.Update, AuditActions.StatusChange);
            var deletes = Count(AuditActions.Delete, AuditActions.Deactivate);
            return new UserActivityRow
            {
                UserId = u.Id,
                User = u.FullName,
                Role = AppRoles.DisplayName(u.Role),
                Logins = logins,
                FailedLogins = failed,
                Creates = creates,
                Updates = updates,
                Deletes = deletes,
                Other = total - logins - failed - creates - updates - deletes,
                Total = total,
                ActivitiesLogged = activityCounts.Where(a => a.UserId == u.Id).Sum(a => a.Count),
                FollowUpsCompleted = followUpCounts.Where(a => a.UserId == u.Id).Sum(a => a.Count),
                LastLogin = lastLogins.FirstOrDefault(l => l.Id == u.Id)?.LastLoginDate
            };
        });

        source = f.Sort switch
        {
            "user" => source.OrderBy(r => r.User),
            "logins" => source.OrderByDescending(r => r.Logins),
            "activities" => source.OrderByDescending(r => r.ActivitiesLogged),
            _ => source.OrderByDescending(r => r.Total)
        };
        var list = source.ToList();

        return new ReportPage<UserActivityRow>
        {
            Key = "UserActivity",
            Title = "User Activity Report",
            Description = "Actions and activity counts per user, from the audit log and activity records.",
            DateFilterLabel = "Event date",
            Filter = f,
            Rows = export ? All(list) : list.ToPagedResult(f.Page, f.PageSize),
            Summary = new()
            {
                new("Users", list.Count.ToString()),
                new("Total actions", list.Sum(r => r.Total).ToString()),
                new("Logins", list.Sum(r => r.Logins).ToString()),
                new("Failed logins", list.Sum(r => r.FailedLogins).ToString()),
                new("Activities logged", list.Sum(r => r.ActivitiesLogged).ToString())
            },
            Owners = owners
        };
    }

    // ---------------------------------------------------------------- Audit report (Admin only)

    public IQueryable<AuditLog> FilterAudit(ReportFilter f)
    {
        var query = _db.AuditLogs.AsNoTracking();
        var from = FromDate(f);
        var to = ToExclusive(f);
        if (from.HasValue) query = query.Where(a => a.CreatedDate >= from);
        if (to.HasValue) query = query.Where(a => a.CreatedDate < to);
        if (!string.IsNullOrEmpty(f.UserId)) query = query.Where(a => a.UserId == f.UserId);
        if (!string.IsNullOrEmpty(f.Action)) query = query.Where(a => a.Action == f.Action);
        if (!string.IsNullOrEmpty(f.Entity)) query = query.Where(a => a.EntityName == f.Entity);
        if (!string.IsNullOrEmpty(f.Status)) query = query.Where(a => a.Result == f.Status);
        if (!string.IsNullOrWhiteSpace(f.Q))
        {
            var term = f.Q.Trim().ToLower();
            query = query.Where(a =>
                (a.RecordId != null && a.RecordId.ToLower() == term) ||
                (a.UserName != null && a.UserName.ToLower().Contains(term)) ||
                (a.Details != null && a.Details.ToLower().Contains(term)));
        }

        return f.Sort == "oldest" ? query.OrderBy(a => a.AuditLogId) : query.OrderByDescending(a => a.AuditLogId);
    }

    public async Task<ReportPage<AuditReportRow>> AuditReportAsync(ReportFilter f, bool export = false)
    {
        var query = FilterAudit(f);
        var byAction = await query.GroupBy(a => a.Action).Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
        var failures = await query.CountAsync(a => a.Result == AuditResults.Failure);

        var page = await query.ToPagedResultAsync(f.Page, PageSize(f, export));
        var rows = page.Items.Select(a => new AuditReportRow
        {
            Id = a.AuditLogId, Timestamp = a.CreatedDate, User = a.UserName ?? "-", Action = a.Action, Entity = a.EntityName,
            RecordId = a.RecordId, Result = a.Result, IpAddress = a.IpAddress, Details = a.Details
        }).ToList();

        var summary = new List<SummaryItem> { new("Events", byAction.Sum(a => a.Count).ToString()), new("Failures", failures.ToString()) };
        summary.AddRange(byAction.OrderByDescending(a => a.Count).Take(4).Select(a => new SummaryItem(a.Key, a.Count.ToString())));

        return new ReportPage<AuditReportRow>
        {
            Key = "Audit",
            Title = "Audit Report",
            Description = "Security and business audit events.",
            DateFilterLabel = "Event date",
            Filter = f,
            Rows = new PagedResult<AuditReportRow> { Items = rows, Page = page.Page, PageSize = page.PageSize, TotalCount = page.TotalCount },
            Summary = summary,
            Owners = await _users.GetAllUsersAsync()
        };
    }
}
