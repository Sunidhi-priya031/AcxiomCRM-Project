using AcxiomCRM.Infrastructure;

namespace AcxiomCRM.ViewModels;

public class PagerModel
{
    public int Page { get; init; }
    public int TotalPages { get; init; }
    public int TotalCount { get; init; }
    public int FirstItem { get; init; }
    public int LastItem { get; init; }
    public string ItemName { get; init; } = "records";
    public string PageParam { get; init; } = "page";

    public static PagerModel From<T>(PagedResult<T> page, string itemName = "records", string pageParam = "page") => new()
    {
        Page = page.Page,
        TotalPages = page.TotalPages,
        TotalCount = page.TotalCount,
        FirstItem = page.FirstItem,
        LastItem = page.LastItem,
        ItemName = itemName,
        PageParam = pageParam
    };
}
