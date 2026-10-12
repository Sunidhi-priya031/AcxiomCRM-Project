using AcxiomCRM.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Customer> Customers { get; set; } = null!;
    public DbSet<Lead> Leads { get; set; } = null!;
    public DbSet<Opportunity> Opportunities { get; set; } = null!;
    public DbSet<FollowUp> FollowUps { get; set; } = null!;
    public DbSet<Activity> Activities { get; set; } = null!;
    public DbSet<AuditLog> AuditLogs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(e =>
        {
            e.Property(u => u.FullName).HasMaxLength(100).IsRequired();
            e.HasOne(u => u.Manager).WithMany().HasForeignKey(u => u.ManagerId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(u => u.IsActive);
        });

        builder.Entity<Customer>(e =>
        {
            e.ToTable("Customers");
            e.HasKey(c => c.CustomerId);
            e.HasIndex(c => c.CustomerCode).IsUnique();
            e.HasIndex(c => c.Email).IsUnique();
            e.HasIndex(c => c.Phone).IsUnique();
            e.HasIndex(c => c.CustomerName);
            e.HasIndex(c => c.AssignedTo);
            e.HasIndex(c => c.Status);
            e.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);
            e.HasOne(c => c.AssignedToUser).WithMany().HasForeignKey(c => c.AssignedTo).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(c => c.CreatedByUser).WithMany().HasForeignKey(c => c.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Lead>(e =>
        {
            e.ToTable("Leads");
            e.HasKey(l => l.LeadId);
            e.HasIndex(l => l.LeadCode).IsUnique();
            e.HasIndex(l => l.Status);
            e.HasIndex(l => l.AssignedTo);
            e.Property(l => l.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(l => l.Source).HasConversion<string>().HasMaxLength(30);
            e.Property(l => l.Priority).HasConversion<string>().HasMaxLength(20);
            // SQLite has no native decimal type; storing as REAL keeps ordering and filtering translatable.
            e.Property(l => l.ExpectedValue).HasConversion<double>();
            e.HasOne(l => l.AssignedToUser).WithMany().HasForeignKey(l => l.AssignedTo).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(l => l.CreatedByUser).WithMany().HasForeignKey(l => l.CreatedBy).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(l => l.ConvertedCustomer).WithMany().HasForeignKey(l => l.ConvertedCustomerId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Opportunity>(e =>
        {
            e.ToTable("Opportunities", t =>
            {
                t.HasCheckConstraint("CK_Opportunities_Probability", "\"Probability\" >= 0 AND \"Probability\" <= 100");
                t.HasCheckConstraint("CK_Opportunities_Amount", "\"Amount\" >= 0");
            });
            e.HasKey(o => o.OpportunityId);
            e.HasIndex(o => o.Stage);
            e.HasIndex(o => o.Status);
            e.HasIndex(o => o.AssignedTo);
            e.HasIndex(o => o.ExpectedCloseDate);
            e.Property(o => o.Stage).HasConversion<string>().HasMaxLength(20);
            e.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(o => o.Source).HasConversion<string>().HasMaxLength(30);
            e.Property(o => o.Amount).HasConversion<double>();
            e.HasOne(o => o.Customer).WithMany().HasForeignKey(o => o.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(o => o.Lead).WithMany().HasForeignKey(o => o.LeadId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(o => o.AssignedToUser).WithMany().HasForeignKey(o => o.AssignedTo).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(o => o.CreatedByUser).WithMany().HasForeignKey(o => o.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FollowUp>(e =>
        {
            e.ToTable("FollowUps");
            e.HasKey(f => f.FollowUpId);
            e.HasIndex(f => f.FollowUpDate);
            e.HasIndex(f => f.Status);
            e.HasIndex(f => f.AssignedTo);
            e.Property(f => f.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(f => f.FollowUpType).HasConversion<string>().HasMaxLength(20);
            e.HasOne(f => f.Customer).WithMany().HasForeignKey(f => f.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(f => f.Lead).WithMany().HasForeignKey(f => f.LeadId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(f => f.Opportunity).WithMany().HasForeignKey(f => f.OpportunityId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(f => f.AssignedToUser).WithMany().HasForeignKey(f => f.AssignedTo).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(f => f.CreatedByUser).WithMany().HasForeignKey(f => f.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Activity>(e =>
        {
            e.ToTable("Activities");
            e.HasKey(a => a.ActivityId);
            e.HasIndex(a => a.ActivityDate);
            e.HasIndex(a => a.ActivityType);
            e.HasIndex(a => a.AssignedTo);
            e.Property(a => a.ActivityType).HasConversion<string>().HasMaxLength(20);
            e.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
            e.HasOne(a => a.Customer).WithMany().HasForeignKey(a => a.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(a => a.Lead).WithMany().HasForeignKey(a => a.LeadId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(a => a.AssignedToUser).WithMany().HasForeignKey(a => a.AssignedTo).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(a => a.CreatedByUser).WithMany().HasForeignKey(a => a.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AuditLog>(e =>
        {
            e.ToTable("AuditLogs");
            e.HasKey(a => a.AuditLogId);
            e.HasIndex(a => a.CreatedDate);
            e.HasIndex(a => a.UserId);
            e.HasIndex(a => a.Action);
            e.HasIndex(a => new { a.EntityName, a.RecordId });
        });
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        GuardAuditLog();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        GuardAuditLog();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>Audit records are append-only: reject any attempt to modify or delete them.</summary>
    private void GuardAuditLog()
    {
        foreach (var entry in ChangeTracker.Entries<AuditLog>())
        {
            if (entry.State == EntityState.Modified || entry.State == EntityState.Deleted)
            {
                throw new InvalidOperationException("Audit log records are append-only and cannot be modified or deleted.");
            }
        }
    }
}
