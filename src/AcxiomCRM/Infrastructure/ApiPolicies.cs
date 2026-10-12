namespace AcxiomCRM.Infrastructure;

public static class ApiPolicies
{
    /// <summary>Authenticated via the session cookie or an API bearer token.</summary>
    public const string ApiUser = "ApiUser";

    /// <summary>Fixed-window per-IP limit for login endpoints.</summary>
    public const string LoginRateLimit = "login";
}
