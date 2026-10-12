namespace AcxiomCRM.Models;

/// <summary>The three primary roles defined by the specification (section 7).</summary>
public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string SalesExecutive = "SalesExecutive";

    public const string AdminOrManager = Admin + "," + Manager;

    public static readonly string[] All = { Admin, Manager, SalesExecutive };

    public static string DisplayName(string? role) => role switch
    {
        Admin => "Admin",
        Manager => "Manager",
        SalesExecutive => "Sales Executive",
        _ => role ?? "-"
    };

    public static bool IsValid(string? role) => role != null && All.Contains(role);
}
