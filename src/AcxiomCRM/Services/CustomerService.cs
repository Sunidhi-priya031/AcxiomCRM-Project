using AcxiomCRM.Data;
using AcxiomCRM.Dtos;
using AcxiomCRM.Infrastructure;
using AcxiomCRM.Models;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services;

public class CustomerSearch
{
    /// <summary>Matches customer name, email, phone, company or code.</summary>
    public string? Q { get; set; }
    public CustomerStatus? Status { get; set; }
    public string? AssignedTo { get; set; }
    public string? City { get; set; }
    public string? Sort { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }
}

public class CustomerService
{
    private readonly ApplicationDbContext _db;
    private readonly IUserContext _user;
    private readonly AuditService _audit;
    private readonly UserDirectoryService _users;

    public CustomerService(ApplicationDbContext db, IUserContext user, AuditService audit, UserDirectoryService users)
    {
        _db = db;
        _user = user;
        _audit = audit;
        _users = users;
    }

    public async Task<IQueryable<Customer>> ScopedQueryAsync() =>
        _db.Customers.InScope(await _user.GetScopeUserIdsAsync());

    public async Task<PagedResult<Customer>> SearchAsync(CustomerSearch s)
    {
        var query = (await ScopedQueryAsync())
            .Include(c => c.AssignedToUser)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(s.Q))
        {
            var term = s.Q.Trim().ToLower();
            query = query.Where(c =>
                c.CustomerName.ToLower().Contains(term) ||
                c.Email.ToLower().Contains(term) ||
                c.Phone.Contains(term) ||
                c.CustomerCode.ToLower().Contains(term) ||
                (c.CompanyName != null && c.CompanyName.ToLower().Contains(term)));
        }

        if (s.Status.HasValue)
        {
            query = query.Where(c => c.Status == s.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(s.AssignedTo))
        {
            query = query.Where(c => c.AssignedTo == s.AssignedTo);
        }

        if (!string.IsNullOrWhiteSpace(s.City))
        {
            var city = s.City.Trim().ToLower();
            query = query.Where(c => c.City != null && c.City.ToLower().Contains(city));
        }

        query = s.Sort switch
        {
            "name" => query.OrderBy(c => c.CustomerName),
            "name_desc" => query.OrderByDescending(c => c.CustomerName),
            "code" => query.OrderBy(c => c.CustomerCode),
            "code_desc" => query.OrderByDescending(c => c.CustomerCode),
            "company" => query.OrderBy(c => c.CompanyName),
            "company_desc" => query.OrderByDescending(c => c.CompanyName),
            "city" => query.OrderBy(c => c.City),
            "city_desc" => query.OrderByDescending(c => c.City),
            "status" => query.OrderBy(c => c.Status),
            "status_desc" => query.OrderByDescending(c => c.Status),
            "created" => query.OrderBy(c => c.CreatedDate),
            _ => query.OrderByDescending(c => c.CreatedDate)
        };

        return await query.ToPagedResultAsync(s.Page, s.PageSize);
    }

    /// <summary>Loads a customer and checks record-level access. Returns Forbidden for records outside the caller's scope.</summary>
    public async Task<ServiceResult<Customer>> GetAsync(int id)
    {
        var customer = await _db.Customers
            .Include(c => c.AssignedToUser)
            .Include(c => c.CreatedByUser)
            .FirstOrDefaultAsync(c => c.CustomerId == id);
        if (customer == null)
        {
            return ServiceResult<Customer>.NotFound();
        }

        var scope = await _user.GetScopeUserIdsAsync();
        return ScopeExtensions.IsInScope(scope, customer.AssignedTo, customer.CreatedBy)
            ? ServiceResult<Customer>.Ok(customer)
            : ServiceResult<Customer>.Forbidden("You do not have access to this customer.");
    }

    public async Task<List<Customer>> GetActiveLookupAsync()
    {
        return await (await ScopedQueryAsync())
            .AsNoTracking()
            .Where(c => c.Status == CustomerStatus.Active)
            .OrderBy(c => c.CustomerName)
            .ToListAsync();
    }

    public static string NormalizeEmail(string? email) => (email ?? string.Empty).Trim().ToLowerInvariant();
    public static string NormalizePhone(string? phone) => (phone ?? string.Empty).Trim();

    /// <summary>Duplicate check across ALL customers (not only those in scope) - email and phone are globally unique.</summary>
    public async Task<ValidationErrors> CheckDuplicatesAsync(string? email, string? phone, int? excludeId)
    {
        var errors = new ValidationErrors();
        var normalizedEmail = NormalizeEmail(email);
        var normalizedPhone = NormalizePhone(phone);

        if (normalizedEmail.Length > 0 &&
            await _db.Customers.AnyAsync(c => c.Email == normalizedEmail && (excludeId == null || c.CustomerId != excludeId)))
        {
            errors.Add(nameof(CustomerRequest.Email), "A customer with this email already exists.");
        }

        if (normalizedPhone.Length > 0 &&
            await _db.Customers.AnyAsync(c => c.Phone == normalizedPhone && (excludeId == null || c.CustomerId != excludeId)))
        {
            errors.Add(nameof(CustomerRequest.Phone), "A customer with this phone number already exists.");
        }

        return errors;
    }

    public async Task<ServiceResult<Customer>> CreateAsync(CustomerRequest request)
    {
        var (assignee, assignError) = await _users.ResolveAssigneeAsync(request.AssignedTo);
        if (assignError != null)
        {
            return ServiceResult<Customer>.Invalid(nameof(CustomerRequest.AssignedTo), assignError);
        }

        var duplicates = await CheckDuplicatesAsync(request.Email, request.Phone, null);
        if (duplicates.Any)
        {
            return ServiceResult<Customer>.Conflict(duplicates.Errors);
        }

        var customer = new Customer
        {
            CustomerCode = "TMP-" + Guid.NewGuid().ToString("N")[..12],
            CreatedBy = _user.UserId,
            CreatedDate = DateTime.Now,
            AssignedTo = assignee!
        };
        Apply(customer, request);

        var saved = await SaveNewAsync(customer);
        if (!saved.Succeeded)
        {
            return saved;
        }

        await _audit.LogAsync(AuditActions.Create, AuditEntities.Customer, customer.CustomerId, null, AuditSnapshot.Of(customer));
        return ServiceResult<Customer>.Ok(customer);
    }

    /// <summary>Saves a new customer and assigns its sequential code (CUST-00001). Used by Create and lead conversion.</summary>
    internal async Task<ServiceResult<Customer>> SaveNewAsync(Customer customer)
    {
        try
        {
            _db.Customers.Add(customer);
            await _db.SaveChangesAsync();
            customer.CustomerCode = $"CUST-{customer.CustomerId:D5}";
            await _db.SaveChangesAsync();
            return ServiceResult<Customer>.Ok(customer);
        }
        catch (DbUpdateException)
        {
            // A concurrent request inserted the same email/phone between the check and the insert.
            _db.ChangeTracker.Clear();
            var duplicates = await CheckDuplicatesAsync(customer.Email, customer.Phone, null);
            return duplicates.Any
                ? ServiceResult<Customer>.Conflict(duplicates.Errors)
                : ServiceResult<Customer>.Invalid(string.Empty, "The customer could not be saved. Please try again.");
        }
    }

    public async Task<ServiceResult<Customer>> UpdateAsync(int id, CustomerRequest request)
    {
        var found = await GetAsync(id);
        if (!found.Succeeded)
        {
            return found;
        }

        var customer = found.Value!;
        var (assignee, assignError) = await _users.ResolveAssigneeAsync(request.AssignedTo, customer.AssignedTo);
        if (assignError != null)
        {
            return ServiceResult<Customer>.Invalid(nameof(CustomerRequest.AssignedTo), assignError);
        }

        var duplicates = await CheckDuplicatesAsync(request.Email, request.Phone, id);
        if (duplicates.Any)
        {
            return ServiceResult<Customer>.Conflict(duplicates.Errors);
        }

        var before = AuditSnapshot.Of(customer);
        Apply(customer, request);
        customer.AssignedTo = assignee!;
        customer.ModifiedDate = DateTime.Now;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            _db.ChangeTracker.Clear();
            var again = await CheckDuplicatesAsync(request.Email, request.Phone, id);
            return again.Any
                ? ServiceResult<Customer>.Conflict(again.Errors)
                : ServiceResult<Customer>.Invalid(string.Empty, "The customer could not be saved. Please try again.");
        }

        var (oldValues, newValues) = AuditSnapshot.Diff(before, AuditSnapshot.Of(customer));
        if (newValues.Count > 0)
        {
            var action = oldValues.ContainsKey(nameof(Customer.Status)) ? AuditActions.StatusChange : AuditActions.Update;
            await _audit.LogAsync(action, AuditEntities.Customer, customer.CustomerId, oldValues, newValues);
        }

        return ServiceResult<Customer>.Ok(customer);
    }

    public async Task<int> CountRelatedAsync(int customerId)
    {
        return await _db.Opportunities.CountAsync(o => o.CustomerId == customerId)
               + await _db.FollowUps.CountAsync(f => f.CustomerId == customerId)
               + await _db.Activities.CountAsync(a => a.CustomerId == customerId)
               + await _db.Leads.CountAsync(l => l.ConvertedCustomerId == customerId);
    }

    /// <summary>
    /// Deletes a customer that has no related records; otherwise deactivates it so history is preserved.
    /// Restricted to Admin and Manager. Value = true when permanently deleted.
    /// </summary>
    public async Task<ServiceResult<bool>> DeleteAsync(int id)
    {
        if (_user.IsSalesExecutive)
        {
            return ServiceResult<bool>.Forbidden("Only Admins and Managers can delete customers.");
        }

        var found = await GetAsync(id);
        if (!found.Succeeded)
        {
            return ServiceResult<bool>.From(found);
        }

        var customer = found.Value!;
        var snapshot = AuditSnapshot.Of(customer);
        if (await CountRelatedAsync(id) > 0)
        {
            if (customer.Status == CustomerStatus.Inactive)
            {
                return ServiceResult<bool>.Invalid(string.Empty, "This customer has related records and is already inactive.");
            }

            customer.Status = CustomerStatus.Inactive;
            customer.ModifiedDate = DateTime.Now;
            await _db.SaveChangesAsync();
            await _audit.LogAsync(AuditActions.Deactivate, AuditEntities.Customer, id,
                new { Status = nameof(CustomerStatus.Active) }, new { Status = nameof(CustomerStatus.Inactive) },
                details: "Customer has related records; deactivated instead of deleted.");
            return ServiceResult<bool>.Ok(false);
        }

        _db.Customers.Remove(customer);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(AuditActions.Delete, AuditEntities.Customer, id, snapshot, null);
        return ServiceResult<bool>.Ok(true);
    }

    private static void Apply(Customer customer, CustomerRequest r)
    {
        customer.CustomerName = r.CustomerName!.Trim();
        customer.Email = NormalizeEmail(r.Email);
        customer.Phone = NormalizePhone(r.Phone);
        customer.CompanyName = Clean(r.CompanyName);
        customer.Address = Clean(r.Address);
        customer.City = Clean(r.City);
        customer.State = Clean(r.State);
        customer.Status = r.Status;
        customer.Notes = Clean(r.Notes);
    }

    internal static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
