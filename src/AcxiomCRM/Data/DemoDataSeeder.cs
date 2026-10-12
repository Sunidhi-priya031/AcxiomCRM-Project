using AcxiomCRM.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Data;

/// <summary>
/// Local-development demo data: five users across the three roles and a small, realistic CRM data set
/// spread over the last eight months so the dashboard and reports have something to show.
/// Runs only when Seed:SeedDemoData is true (Development) and the database has no users yet.
/// </summary>
public static class DemoDataSeeder
{
    public const string AdminEmail = "admin@acxiomcrm.local";
    public const string ManagerEmail = "manager@acxiomcrm.local";
    public const string Sales1Email = "rohan.das@acxiomcrm.local";
    public const string Sales2Email = "sneha.patnaik@acxiomcrm.local";
    public const string Sales3Email = "vikram.rao@acxiomcrm.local";

    public static async Task SeedAsync(ApplicationDbContext db, UserManager<ApplicationUser> userManager, string password, ILogger logger)
    {
        if (await db.Users.AnyAsync())
        {
            return;
        }

        await CreateUserAsync(userManager, AdminEmail, "Aarav Mehta", AppRoles.Admin, null, password);
        var manager = await CreateUserAsync(userManager, ManagerEmail, "Priya Nair", AppRoles.Manager, null, password);
        var rohan = await CreateUserAsync(userManager, Sales1Email, "Rohan Das", AppRoles.SalesExecutive, manager.Id, password);
        var sneha = await CreateUserAsync(userManager, Sales2Email, "Sneha Patnaik", AppRoles.SalesExecutive, manager.Id, password);
        var vikram = await CreateUserAsync(userManager, Sales3Email, "Vikram Rao", AppRoles.SalesExecutive, null, password);

        var today = DateTime.Today;
        DateTime Ago(int days, int hour = 10) => today.AddDays(-days).AddHours(hour);

        // ---- Customers
        var customerData = new (string Name, string Company, string Email, string Phone, string City, string State, ApplicationUser Owner, int DaysAgo)[]
        {
            ("Sanjay Mohanty", "Kalinga Steel Traders", "sanjay.mohanty@kalingasteel.in", "9437012345", "Bhubaneswar", "Odisha", rohan, 230),
            ("Anita Behera", "Mahanadi Agro Foods", "anita@mahanadiagro.in", "9438123456", "Cuttack", "Odisha", rohan, 205),
            ("Deepak Sahoo", "Utkal Logistics Pvt Ltd", "deepak.sahoo@utkallogistics.com", "9861234567", "Bhubaneswar", "Odisha", sneha, 190),
            ("Meera Iyer", "Konark Textiles", "meera.iyer@konarktextiles.com", "9845098765", "Puri", "Odisha", sneha, 170),
            ("Rajesh Panda", "Rourkela Engineering Works", "rajesh.panda@rewindia.com", "9776543210", "Rourkela", "Odisha", vikram, 150),
            ("Kavita Mishra", "Cuttack Pharma Distributors", "kavita@cuttackpharma.in", "9937654321", "Cuttack", "Odisha", rohan, 120),
            ("Arjun Reddy", "Deccan IT Services", "arjun.reddy@deccanit.com", "9849012367", "Hyderabad", "Telangana", vikram, 96),
            ("Pooja Sharma", "Chilika Seafoods", "pooja.sharma@chilikaseafoods.in", "9090123456", "Berhampur", "Odisha", sneha, 75),
            ("Sunil Nayak", "Jajpur Cement Agencies", "sunil.nayak@jajpurcement.in", "9853210987", "Jajpur", "Odisha", rohan, 48),
            ("Farah Khan", "Eastern Hospitality Group", "farah.khan@easternhospitality.com", "9820456789", "Kolkata", "West Bengal", manager, 30),
            ("Manoj Tripathy", "Sambalpur Handloom House", "manoj@sambalpurhandloom.in", "9439876543", "Sambalpur", "Odisha", sneha, 14),
            ("Ritu Agarwal", "Agarwal Electricals", "ritu.agarwal@agarwalelectricals.com", "9831122334", "Balasore", "Odisha", rohan, 5)
        };

        var customers = new List<Customer>();
        foreach (var c in customerData)
        {
            customers.Add(new Customer
            {
                CustomerCode = "TMP-" + Guid.NewGuid().ToString("N")[..12],
                CustomerName = c.Name,
                CompanyName = c.Company,
                Email = c.Email,
                Phone = c.Phone,
                City = c.City,
                State = c.State,
                Address = $"{c.City}, {c.State}",
                Status = CustomerStatus.Active,
                AssignedTo = c.Owner.Id,
                CreatedBy = c.Owner.Id,
                CreatedDate = Ago(c.DaysAgo)
            });
        }

        customers[4].Status = CustomerStatus.Inactive;
        db.Customers.AddRange(customers);
        await db.SaveChangesAsync();
        foreach (var c in customers)
        {
            c.CustomerCode = $"CUST-{c.CustomerId:D5}";
        }

        // ---- Leads
        var leadData = new (string Name, string Company, string Email, string Phone, LeadSource Source, LeadStatus Status, LeadPriority Priority, decimal Value, ApplicationUser Owner, int DaysAgo)[]
        {
            ("Nikhil Jena", "Odisha Solar Systems", "nikhil.jena@odishasolar.in", "9437551122", LeadSource.Website, LeadStatus.New, LeadPriority.High, 850000, rohan, 3),
            ("Swati Rath", "Rath Interiors", "swati@rathinteriors.com", "9861445566", LeadSource.Referral, LeadStatus.Contacted, LeadPriority.Medium, 240000, rohan, 12),
            ("Imran Ali", "Paradip Port Services", "imran.ali@paradipport.in", "9438778899", LeadSource.TradeShow, LeadStatus.Qualified, LeadPriority.High, 1200000, sneha, 20),
            ("Lakshmi Pradhan", "Pradhan Dairy Farms", "lakshmi@pradhandairy.in", "9776112233", LeadSource.ColdCall, LeadStatus.New, LeadPriority.Low, 90000, sneha, 6),
            ("Gaurav Singh", "Singh Auto Components", "gaurav.singh@singhauto.com", "9937221144", LeadSource.EmailCampaign, LeadStatus.Unqualified, LeadPriority.Low, 60000, vikram, 40),
            ("Bhavna Das", "Das Medical Supplies", "bhavna.das@dasmedical.in", "9853667788", LeadSource.SocialMedia, LeadStatus.Contacted, LeadPriority.Medium, 310000, vikram, 18),
            ("Tarun Biswal", "Biswal Constructions", "tarun@biswalconstructions.in", "9090778866", LeadSource.Partner, LeadStatus.Lost, LeadPriority.Medium, 450000, rohan, 65),
            ("Neha Kapoor", "Kapoor Retail Chain", "neha.kapoor@kapoorretail.com", "9811223344", LeadSource.Website, LeadStatus.Qualified, LeadPriority.High, 760000, manager, 9),
            ("Ashok Mallick", "Mallick Printing Press", "ashok@mallickprinting.in", "9439001122", LeadSource.Referral, LeadStatus.New, LeadPriority.Medium, 150000, sneha, 1),
            ("Ramesh Swain", "Swain Agro Equipments", "ramesh.swain@swainagro.in", "9438665544", LeadSource.TradeShow, LeadStatus.Converted, LeadPriority.High, 540000, rohan, 52)
        };

        var leads = new List<Lead>();
        foreach (var l in leadData)
        {
            leads.Add(new Lead
            {
                LeadCode = "TMP-" + Guid.NewGuid().ToString("N")[..12],
                LeadName = l.Name,
                CompanyName = l.Company,
                Email = l.Email,
                Phone = l.Phone,
                Source = l.Source,
                Status = l.Status,
                Priority = l.Priority,
                ExpectedValue = l.Value,
                AssignedTo = l.Owner.Id,
                CreatedBy = l.Owner.Id,
                CreatedDate = Ago(l.DaysAgo)
            });
        }

        db.Leads.AddRange(leads);
        await db.SaveChangesAsync();
        foreach (var l in leads)
        {
            l.LeadCode = $"LEAD-{l.LeadId:D5}";
        }

        var convertedLead = leads.Single(l => l.Status == LeadStatus.Converted);
        var convertedCustomer = customers[8]; // Jajpur Cement Agencies - created when the lead was converted
        convertedLead.ConvertedCustomerId = convertedCustomer.CustomerId;
        convertedLead.ConvertedDate = convertedCustomer.CreatedDate;
        await db.SaveChangesAsync();

        // ---- Opportunities (closed ones spread over past months for the monthly chart)
        Opportunity Opp(string name, Customer customer, decimal amount, OpportunityStage stage, int probability, int createdAgo, int closeOffsetDays, int? closedAgo = null, Lead? lead = null) => new()
        {
            OpportunityName = name,
            CustomerId = customer.CustomerId,
            LeadId = lead?.LeadId,
            Amount = amount,
            Stage = stage,
            Status = Opportunity.StatusForStage(stage),
            Probability = probability,
            ExpectedCloseDate = closedAgo.HasValue ? today.AddDays(-closedAgo.Value) : today.AddDays(closeOffsetDays),
            ClosedDate = closedAgo.HasValue ? Ago(closedAgo.Value, 16) : null,
            Source = lead?.Source,
            AssignedTo = customer.AssignedTo,
            CreatedBy = customer.AssignedTo,
            CreatedDate = Ago(createdAgo)
        };

        var opportunities = new List<Opportunity>
        {
            Opp("TMT bar annual supply contract", customers[0], 1850000, OpportunityStage.Won, 100, 220, 0, closedAgo: 180),
            Opp("Cold storage expansion", customers[1], 960000, OpportunityStage.Won, 100, 200, 0, closedAgo: 150),
            Opp("Fleet tracking rollout", customers[2], 720000, OpportunityStage.Lost, 0, 185, 0, closedAgo: 140),
            Opp("Warehouse management system", customers[2], 1340000, OpportunityStage.Won, 100, 160, 0, closedAgo: 110),
            Opp("Dyeing unit automation", customers[3], 580000, OpportunityStage.Won, 100, 150, 0, closedAgo: 85),
            Opp("Plant maintenance AMC", customers[4], 410000, OpportunityStage.Lost, 0, 140, 0, closedAgo: 70),
            Opp("Distribution software licence", customers[5], 640000, OpportunityStage.Won, 100, 110, 0, closedAgo: 45),
            Opp("Cloud migration phase 1", customers[6], 1150000, OpportunityStage.Won, 100, 90, 0, closedAgo: 20),
            Opp("Seafood export ERP", customers[7], 870000, OpportunityStage.Negotiation, 75, 70, 18),
            Opp("Cement depot automation", customers[8], 540000, OpportunityStage.Proposal, 50, 46, 30, lead: convertedLead),
            Opp("Hotel booking integration", customers[9], 1260000, OpportunityStage.Qualification, 20, 25, 60),
            Opp("Handloom e-commerce storefront", customers[10], 380000, OpportunityStage.Proposal, 45, 12, 40),
            Opp("Cloud migration phase 2", customers[6], 1480000, OpportunityStage.Negotiation, 70, 15, 25),
            Opp("Billing system upgrade", customers[11], 260000, OpportunityStage.Qualification, 25, 4, 75),
            Opp("Pharma cold-chain monitoring", customers[5], 690000, OpportunityStage.Lost, 0, 60, 0, closedAgo: 10)
        };
        db.Opportunities.AddRange(opportunities);
        await db.SaveChangesAsync();

        // ---- Follow-ups (planned upcoming, overdue, completed, missed)
        FollowUp Fu(string subject, FollowUpType type, DateTime when, FollowUpStatus status, ApplicationUser owner,
            Customer? customer = null, Lead? lead = null, Opportunity? opportunity = null, string? remarks = null) => new()
        {
            Subject = subject,
            FollowUpType = type,
            FollowUpDate = when,
            Status = status,
            Remarks = remarks,
            CustomerId = customer?.CustomerId,
            LeadId = lead?.LeadId,
            OpportunityId = opportunity?.OpportunityId,
            AssignedTo = owner.Id,
            CreatedBy = owner.Id,
            CreatedDate = when.AddDays(-5),
            CompletedDate = status == FollowUpStatus.Completed ? when.AddHours(1) : null
        };

        db.FollowUps.AddRange(
            Fu("Share revised ERP quotation", FollowUpType.Email, today.AddDays(1).AddHours(11), FollowUpStatus.Planned, sneha, opportunity: opportunities[8]),
            Fu("Site visit for depot automation", FollowUpType.Visit, today.AddDays(3).AddHours(15), FollowUpStatus.Planned, rohan, opportunity: opportunities[9]),
            Fu("Introductory call", FollowUpType.Call, today.AddHours(16), FollowUpStatus.Planned, rohan, lead: leads[0]),
            Fu("Product demo for dairy automation", FollowUpType.Meeting, today.AddDays(5).AddHours(12), FollowUpStatus.Planned, sneha, lead: leads[3]),
            Fu("Requirement discussion", FollowUpType.Meeting, today.AddDays(-2).AddHours(14), FollowUpStatus.Planned, vikram, lead: leads[5]),
            Fu("Payment follow-up for licence", FollowUpType.Call, today.AddDays(-4).AddHours(10), FollowUpStatus.Planned, rohan, customer: customers[5]),
            Fu("Kick-off meeting", FollowUpType.Meeting, today.AddDays(-20).AddHours(11), FollowUpStatus.Completed, vikram, customer: customers[6], remarks: "Scope for phase 2 agreed."),
            Fu("Send storefront mock-ups", FollowUpType.Email, today.AddDays(-6).AddHours(9), FollowUpStatus.Missed, sneha, opportunity: opportunities[11]),
            Fu("Quarterly review", FollowUpType.Visit, today.AddDays(9).AddHours(11), FollowUpStatus.Planned, manager, customer: customers[9]),
            Fu("Proposal walkthrough", FollowUpType.Call, today.AddDays(2).AddHours(17), FollowUpStatus.Planned, manager, lead: leads[7]));

        // ---- Activities
        Activity Act(ActivityType type, string subject, int daysAgo, ActivityStatus status, ApplicationUser owner, Customer? customer = null, Lead? lead = null, string? description = null) => new()
        {
            ActivityType = type,
            Subject = subject,
            Description = description,
            ActivityDate = Ago(daysAgo, 12),
            Status = status,
            CustomerId = customer?.CustomerId,
            LeadId = lead?.LeadId,
            AssignedTo = owner.Id,
            CreatedBy = owner.Id,
            CreatedDate = Ago(daysAgo, 12)
        };

        db.Activities.AddRange(
            Act(ActivityType.Call, "Discussed annual volumes", 2, ActivityStatus.Completed, rohan, customer: customers[0], description: "Expect 15% higher volume next year."),
            Act(ActivityType.Meeting, "Phase 2 scoping workshop", 20, ActivityStatus.Completed, vikram, customer: customers[6]),
            Act(ActivityType.Email, "Sent company profile", 11, ActivityStatus.Completed, rohan, lead: leads[1]),
            Act(ActivityType.Task, "Prepare pricing sheet", -2, ActivityStatus.Planned, sneha, customer: customers[7]),
            Act(ActivityType.Call, "Qualification call", 19, ActivityStatus.Completed, sneha, lead: leads[2], description: "Budget approved for Q3."),
            Act(ActivityType.Meeting, "Contract renewal meeting", 1, ActivityStatus.Completed, manager, customer: customers[9]),
            Act(ActivityType.Task, "Collect GST documents", 3, ActivityStatus.Cancelled, rohan, customer: customers[11]),
            Act(ActivityType.Email, "Shared case studies", 7, ActivityStatus.Completed, vikram, lead: leads[5]));

        await db.SaveChangesAsync();
        logger.LogInformation("Demo data seeded: 5 users, {Customers} customers, {Leads} leads, {Opportunities} opportunities.",
            customers.Count, leads.Count, opportunities.Count);
    }

    private static async Task<ApplicationUser> CreateUserAsync(UserManager<ApplicationUser> userManager, string email, string name,
        string role, string? managerId, string password)
    {
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = name,
            IsActive = true,
            ManagerId = managerId,
            CreatedDate = DateTime.Now.AddDays(-240)
        };
        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Demo user could not be created (check that Seed:DemoPassword meets the password policy): " +
                                                string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        await userManager.AddToRoleAsync(user, role);
        return user;
    }
}
