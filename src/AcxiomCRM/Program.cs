using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using System.Threading.RateLimiting;
using AcxiomCRM.Data;
using AcxiomCRM.Infrastructure;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;
var isDevelopment = builder.Environment.IsDevelopment();

// ---------------------------------------------------------------- Configuration
builder.Services.Configure<SecuritySettings>(configuration.GetSection("Security"));
builder.Services.Configure<SeedSettings>(configuration.GetSection("Seed"));
builder.Services.Configure<DashboardSettings>(configuration.GetSection("Dashboard"));
var security = configuration.GetSection("Security").Get<SecuritySettings>() ?? new SecuritySettings();

// ---------------------------------------------------------------- Database (EF Core + SQLite)
var dataDirectory = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
Directory.CreateDirectory(dataDirectory);
var connectionString = (configuration.GetConnectionString("DefaultConnection")
                        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured."))
    .Replace("|DataDirectory|", dataDirectory + Path.DirectorySeparatorChar);
builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));

// ---------------------------------------------------------------- Identity: hashing, password policy, lockout
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedAccount = false;

        options.Password.RequiredLength = security.Password.RequiredLength;
        options.Password.RequireDigit = security.Password.RequireDigit;
        options.Password.RequireUppercase = security.Password.RequireUppercase;
        options.Password.RequireLowercase = security.Password.RequireLowercase;
        options.Password.RequireNonAlphanumeric = security.Password.RequireNonAlphanumeric;
        options.Password.RequiredUniqueChars = security.Password.RequiredUniqueChars;

        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = security.Lockout.MaxFailedAttempts;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(security.Lockout.LockoutMinutes);
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager<ApplicationSignInManager>()
    .AddClaimsPrincipalFactory<AppClaimsPrincipalFactory>()
    .AddDefaultTokenProviders();

// Bearer tokens for API clients (issued by POST /api/auth/login).
builder.Services.AddAuthentication()
    .AddBearerToken(IdentityConstants.BearerScheme, options =>
    {
        options.BearerTokenExpiration = TimeSpan.FromMinutes(security.ApiTokenMinutes);
    });

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "AcxiomCRM.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = isDevelopment ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(security.SessionTimeoutMinutes);
    options.SlidingExpiration = true;
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";

    // API callers get status codes, not redirects to the login page.
    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
});

// Re-validate the security stamp regularly (in addition to SessionValidationFilter on every request).
builder.Services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.FromMinutes(1));

builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "AcxiomCRM.Antiforgery";
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = isDevelopment ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});

// ---------------------------------------------------------------- Authorization
builder.Services.AddAuthorization(options =>
{
    // Every endpoint requires an authenticated user unless it explicitly allows anonymous access.
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();

    // API endpoints accept either the browser session cookie or an API bearer token.
    options.AddPolicy(ApiPolicies.ApiUser, policy => policy
        .AddAuthenticationSchemes(IdentityConstants.ApplicationScheme, IdentityConstants.BearerScheme)
        .RequireAuthenticatedUser());
});

// ---------------------------------------------------------------- Rate limiting for authentication endpoints
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(ApiPolicies.LoginRateLimit, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = Math.Max(1, security.LoginRateLimitPerMinute),
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

// ---------------------------------------------------------------- MVC + API
builder.Services
    .AddControllersWithViews(options =>
    {
        options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
        options.Filters.Add<SessionValidationFilter>();
        // Required fields are declared explicitly with [Required] and field-specific messages.
        options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
        // Field-specific messages for values that cannot be bound (e.g. tampered enum values, text in number fields).
        options.ModelBindingMessageProvider.SetAttemptedValueIsInvalidAccessor((value, field) => $"The value '{value}' is not valid for {field}.");
        options.ModelBindingMessageProvider.SetValueMustBeANumberAccessor(field => $"{field} must be a number.");
        options.ModelBindingMessageProvider.SetValueIsInvalidAccessor(value => $"The value '{value}' is not valid.");
    })
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
// Emit non-ASCII text (₹, Indian names) as-is instead of numeric entities; HTML-special characters are still encoded.
builder.Services.Configure<Microsoft.Extensions.WebEncoders.WebEncoderOptions>(options =>
    options.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));
builder.Services.AddHttpContextAccessor();

// ---------------------------------------------------------------- Application services
builder.Services.AddScoped<IUserContext, UserContext>();
builder.Services.AddScoped<SessionValidationFilter>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<UserDirectoryService>();
builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<LeadService>();
builder.Services.AddScoped<OpportunityService>();
builder.Services.AddScoped<FollowUpService>();
builder.Services.AddScoped<ActivityService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<UserAdminService>();

if (!isDevelopment)
{
    builder.Services.AddHsts(options =>
    {
        options.MaxAge = TimeSpan.FromDays(180);
        options.IncludeSubDomains = true;
    });
}

var app = builder.Build();

await DbInitializer.InitializeAsync(app.Services);

// ---------------------------------------------------------------- HTTP pipeline
// Generic error page (MVC) or problem response (API) in every environment: stack traces and database
// details are written to the server log only, never to users.
app.UseExceptionHandler("/Home/Error");
if (!isDevelopment)
{
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Home/StatusCode/{0}");

app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next();
});

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

app.Run();

public partial class Program
{
}
