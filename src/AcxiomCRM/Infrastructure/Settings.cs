namespace AcxiomCRM.Infrastructure;

/// <summary>Bound from the "Security" configuration section.</summary>
public class SecuritySettings
{
    public PasswordSettings Password { get; set; } = new();
    public LockoutSettings Lockout { get; set; } = new();

    /// <summary>Idle timeout of the authentication cookie (sliding).</summary>
    public int SessionTimeoutMinutes { get; set; } = 30;

    /// <summary>Login attempts allowed per client IP per minute (MVC login and API login).</summary>
    public int LoginRateLimitPerMinute { get; set; } = 20;

    /// <summary>Lifetime of API bearer tokens issued by POST /api/auth/login.</summary>
    public int ApiTokenMinutes { get; set; } = 60;
}

public class PasswordSettings
{
    public int RequiredLength { get; set; } = 8;
    public bool RequireDigit { get; set; } = true;
    public bool RequireUppercase { get; set; } = true;
    public bool RequireLowercase { get; set; } = true;
    public bool RequireNonAlphanumeric { get; set; } = true;
    public int RequiredUniqueChars { get; set; } = 4;
}

public class LockoutSettings
{
    public int MaxFailedAttempts { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
}

/// <summary>Bound from the "Seed" configuration section. Demo data is for local development only.</summary>
public class SeedSettings
{
    public bool SeedDemoData { get; set; }

    /// <summary>Password for seeded demo accounts. Supply via user-secrets; never commit it.</summary>
    public string? DemoPassword { get; set; }

    /// <summary>Optional first Admin account for a fresh (non-demo) database. Supply via user-secrets or environment variables.</summary>
    public string? AdminEmail { get; set; }

    public string? AdminPassword { get; set; }
}

public class DashboardSettings
{
    /// <summary>Number of months shown in the Monthly Sales chart (the configured reporting period).</summary>
    public int MonthlyChartMonths { get; set; } = 12;

    /// <summary>Window (days) used for "upcoming" follow-ups.</summary>
    public int UpcomingFollowUpDays { get; set; } = 7;
}
