using System.Globalization;
using System.Text.Encodings.Web;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.WebUtilities;

namespace AcxiomCRM.Infrastructure;

public static class HtmlHelpers
{
    private static readonly CultureInfo Indian = CreateIndianCulture();

    private static CultureInfo CreateIndianCulture()
    {
        try
        {
            return CultureInfo.GetCultureInfo("en-IN");
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }

    /// <summary>Indian Rupee formatting with lakh/crore grouping, e.g. ₹12,34,567.</summary>
    public static string Money(decimal value, bool decimals = false) =>
        "₹" + value.ToString(decimals ? "#,##,##0.00" : "#,##,##0", Indian);

    public static string Date(DateTime? value) => value?.ToString("dd MMM yyyy") ?? "-";

    public static string DateTimeText(DateTime? value) => value?.ToString("dd MMM yyyy, hh:mm tt") ?? "-";

    /// <summary>URL for the current page with some query values replaced (null removes the value).</summary>
    public static string UrlWith(this IHtmlHelper html, IDictionary<string, string?> changes)
    {
        var request = html.ViewContext.HttpContext.Request;
        var query = request.Query.ToDictionary(q => q.Key, q => (string?)q.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in changes)
        {
            if (string.IsNullOrEmpty(value)) query.Remove(key);
            else query[key] = value;
        }

        var clean = query.Where(q => !string.IsNullOrEmpty(q.Value)).ToDictionary(q => q.Key, q => q.Value);
        return QueryHelpers.AddQueryString(request.PathBase + request.Path, clean);
    }

    /// <summary>Sortable column header. Clicking toggles ascending/descending and returns to page 1.</summary>
    public static IHtmlContent SortLink(this IHtmlHelper html, string label, string key, string? defaultDirectionDesc = null)
    {
        var current = html.ViewContext.HttpContext.Request.Query["sort"].ToString();
        var isAsc = string.Equals(current, key, StringComparison.OrdinalIgnoreCase);
        var isDesc = string.Equals(current, key + "_desc", StringComparison.OrdinalIgnoreCase);
        var next = isAsc ? key + "_desc" : key;
        var url = html.UrlWith(new Dictionary<string, string?> { ["sort"] = next, ["page"] = null });
        var icon = isAsc ? "bi-caret-up-fill" : isDesc ? "bi-caret-down-fill" : "bi-arrow-down-up text-body-tertiary";
        var aria = isAsc ? "ascending" : isDesc ? "descending" : "none";
        return new HtmlString(
            $"<a class=\"sort-link\" href=\"{HtmlEncoder.Default.Encode(url)}\" aria-sort=\"{aria}\">{HtmlEncoder.Default.Encode(label)} <i class=\"bi {icon}\" aria-hidden=\"true\"></i></a>");
    }

    public static IHtmlContent StatusBadge(object? status)
    {
        if (status == null)
        {
            return HtmlString.Empty;
        }

        var text = status is Enum e ? e.GetDisplayName() : status.ToString() ?? "";
        var css = status switch
        {
            CustomerStatus.Active => "badge-soft-success",
            CustomerStatus.Inactive => "badge-soft-secondary",
            LeadStatus.New => "badge-soft-primary",
            LeadStatus.Contacted => "badge-soft-info",
            LeadStatus.Qualified => "badge-soft-indigo",
            LeadStatus.Unqualified => "badge-soft-secondary",
            LeadStatus.Converted => "badge-soft-success",
            LeadStatus.Lost => "badge-soft-danger",
            OpportunityStage.Qualification => "badge-soft-info",
            OpportunityStage.Proposal => "badge-soft-primary",
            OpportunityStage.Negotiation => "badge-soft-warning",
            OpportunityStage.Won or OpportunityStatus.Won => "badge-soft-success",
            OpportunityStage.Lost or OpportunityStatus.Lost => "badge-soft-danger",
            OpportunityStatus.Open => "badge-soft-primary",
            FollowUpStatus.Planned => "badge-soft-primary",
            FollowUpStatus.Completed => "badge-soft-success",
            FollowUpStatus.Missed => "badge-soft-danger",
            FollowUpStatus.Cancelled => "badge-soft-secondary",
            ActivityStatus.Planned => "badge-soft-primary",
            ActivityStatus.Completed => "badge-soft-success",
            ActivityStatus.Cancelled => "badge-soft-secondary",
            LeadPriority.High => "badge-soft-danger",
            LeadPriority.Medium => "badge-soft-warning",
            LeadPriority.Low => "badge-soft-secondary",
            "Success" => "badge-soft-success",
            "Failure" => "badge-soft-danger",
            "Overdue" => "badge-soft-danger",
            _ => "badge-soft-secondary"
        };
        return new HtmlString($"<span class=\"badge {css}\">{HtmlEncoder.Default.Encode(text)}</span>");
    }

    public static string ActivityIcon(ActivityType type) => type switch
    {
        ActivityType.Call => "bi-telephone",
        ActivityType.Meeting => "bi-people",
        ActivityType.Email => "bi-envelope",
        _ => "bi-check2-square"
    };

    public static string FollowUpIcon(FollowUpType type) => type switch
    {
        FollowUpType.Call => "bi-telephone",
        FollowUpType.Email => "bi-envelope",
        FollowUpType.Meeting => "bi-people",
        _ => "bi-geo-alt"
    };

    public static string Initials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "?";
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 1 ? parts[0][..1].ToUpperInvariant() : (parts[0][..1] + parts[^1][..1]).ToUpperInvariant();
    }

    /// <summary>Pretty-prints audit JSON values for display.</summary>
    public static string PrettyJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return "-";
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            return System.Text.Json.JsonSerializer.Serialize(doc.RootElement, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
        }
        catch (System.Text.Json.JsonException)
        {
            return json;
        }
    }

    /// <summary>Short "field: old → new" summary for audit history timelines.</summary>
    public static string ChangeSummary(string? oldJson, string? newJson)
    {
        try
        {
            var oldValues = Parse(oldJson);
            var newValues = Parse(newJson);
            if (newValues.Count == 0) return string.Empty;
            var parts = newValues.Select(kv =>
            {
                var before = oldValues.TryGetValue(kv.Key, out var o) ? o : null;
                return before == null ? $"{kv.Key}: {kv.Value}" : $"{kv.Key}: {before} → {kv.Value}";
            }).Take(4);
            var text = string.Join("; ", parts);
            return newValues.Count > 4 ? text + "; …" : text;
        }
        catch (System.Text.Json.JsonException)
        {
            return string.Empty;
        }
    }

    private static Dictionary<string, string?> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object) return new();
        return doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.ValueKind switch
        {
            System.Text.Json.JsonValueKind.Null => "(empty)",
            System.Text.Json.JsonValueKind.String => FormatString(p.Value.GetString()),
            _ => p.Value.ToString()
        });
    }

    private static string? FormatString(string? value) =>
        DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) && value!.Length >= 19
            ? (d.TimeOfDay == TimeSpan.Zero ? d.ToString("dd MMM yyyy") : d.ToString("dd MMM yyyy HH:mm"))
            : value;
}
