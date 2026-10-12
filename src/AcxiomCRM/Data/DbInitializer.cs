using AcxiomCRM.Infrastructure;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AcxiomCRM.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;
        var db = provider.GetRequiredService<ApplicationDbContext>();
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("DbInitializer");

        await ApplySchemaAsync(db, logger);
        await ProtectAuditLogAsync(db);
        await EnsureRolesAsync(provider.GetRequiredService<RoleManager<IdentityRole>>());

        var seed = provider.GetRequiredService<IOptions<SeedSettings>>().Value;
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        if (seed.SeedDemoData)
        {
            if (string.IsNullOrWhiteSpace(seed.DemoPassword))
            {
                logger.LogWarning("Seed:SeedDemoData is enabled but Seed:DemoPassword is not set. " +
                                  "Set it with: dotnet user-secrets set \"Seed:DemoPassword\" \"<password>\" (see README). Demo data skipped.");
            }
            else
            {
                await DemoDataSeeder.SeedAsync(db, userManager, seed.DemoPassword, logger);
            }
        }

        await EnsureInitialAdminAsync(userManager, seed, logger);
    }

    private static async Task ApplySchemaAsync(ApplicationDbContext db, ILogger logger)
    {
        if (db.Database.GetMigrations().Any())
        {
            try
            {
                await db.Database.MigrateAsync();
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex,
                    "Applying migrations failed. If the database was created before migrations were added, " +
                    "delete App_Data/acxiomcrm.db and start the application again.");
                throw;
            }
        }
        else
        {
            logger.LogWarning("No EF Core migrations were found in the project; creating the schema with EnsureCreated. " +
                              "Run the setup script (or 'dotnet ef migrations add InitialCreate') to use migrations.");
            await db.Database.EnsureCreatedAsync();
        }
    }

    /// <summary>Database-level protection for the append-only audit log (SQLite triggers).</summary>
    private static async Task ProtectAuditLogAsync(ApplicationDbContext db)
    {
        if (db.Database.ProviderName != "Microsoft.EntityFrameworkCore.Sqlite")
        {
            return;
        }

        await db.Database.ExecuteSqlRawAsync(
            "CREATE TRIGGER IF NOT EXISTS TR_AuditLogs_NoUpdate BEFORE UPDATE ON AuditLogs " +
            "BEGIN SELECT RAISE(ABORT, 'Audit log records are append-only.'); END;");
        await db.Database.ExecuteSqlRawAsync(
            "CREATE TRIGGER IF NOT EXISTS TR_AuditLogs_NoDelete BEFORE DELETE ON AuditLogs " +
            "BEGIN SELECT RAISE(ABORT, 'Audit log records are append-only.'); END;");
    }

    private static async Task EnsureRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        foreach (var role in AppRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }
    }

    /// <summary>
    /// Bootstraps the first Admin from configuration (Seed:AdminEmail / Seed:AdminPassword, supplied through
    /// user-secrets or environment variables) when no Admin exists. Nothing is hard-coded.
    /// </summary>
    private static async Task EnsureInitialAdminAsync(UserManager<ApplicationUser> userManager, SeedSettings seed, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(seed.AdminEmail) || string.IsNullOrWhiteSpace(seed.AdminPassword))
        {
            return;
        }

        if ((await userManager.GetUsersInRoleAsync(AppRoles.Admin)).Count > 0)
        {
            return;
        }

        var admin = new ApplicationUser
        {
            UserName = seed.AdminEmail,
            Email = seed.AdminEmail,
            EmailConfirmed = true,
            FullName = "System Administrator",
            IsActive = true,
            CreatedDate = DateTime.Now
        };
        var result = await userManager.CreateAsync(admin, seed.AdminPassword);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(admin, AppRoles.Admin);
            logger.LogInformation("Initial Admin account {Email} created from configuration.", seed.AdminEmail);
        }
        else
        {
            logger.LogError("Initial Admin account could not be created: {Errors}",
                string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }
}
