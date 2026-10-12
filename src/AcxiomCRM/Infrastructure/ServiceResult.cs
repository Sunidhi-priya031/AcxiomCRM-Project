using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace AcxiomCRM.Infrastructure;

public enum ServiceStatus
{
    Ok,
    ValidationFailed,
    NotFound,
    Forbidden,
    Conflict
}

/// <summary>
/// Outcome of a business operation. Services never throw for expected failures; controllers map the
/// status to ModelState errors (MVC) or HTTP status codes (API).
/// </summary>
public class ServiceResult
{
    public ServiceStatus Status { get; protected init; } = ServiceStatus.Ok;

    /// <summary>Field name (matches request property names) to messages. Empty key = form-level error.</summary>
    public Dictionary<string, List<string>> Errors { get; protected init; } = new();

    public bool Succeeded => Status == ServiceStatus.Ok;

    public string? FirstError => Errors.Values.SelectMany(v => v).FirstOrDefault();

    public static ServiceResult Ok() => new();
    public static ServiceResult NotFound() => new() { Status = ServiceStatus.NotFound };
    public static ServiceResult Forbidden(string message = "You are not authorised to perform this action.") =>
        new() { Status = ServiceStatus.Forbidden, Errors = ErrorMap(string.Empty, message) };
    public static ServiceResult Invalid(Dictionary<string, List<string>> errors) =>
        new() { Status = ServiceStatus.ValidationFailed, Errors = errors };
    public static ServiceResult Invalid(string field, string message) => Invalid(ErrorMap(field, message));
    public static ServiceResult Conflict(Dictionary<string, List<string>> errors) =>
        new() { Status = ServiceStatus.Conflict, Errors = errors };

    protected static Dictionary<string, List<string>> ErrorMap(string field, string message) =>
        new() { [field] = new List<string> { message } };

    public void CopyTo(ModelStateDictionary modelState, string prefix = "")
    {
        foreach (var (field, messages) in Errors)
        {
            var key = string.IsNullOrEmpty(field) || string.IsNullOrEmpty(prefix) ? field : $"{prefix}.{field}";
            foreach (var message in messages)
            {
                modelState.AddModelError(key, message);
            }
        }
    }

    /// <summary>Maps a failed result to a consistent API error response (RFC 7807 problem details).</summary>
    public IActionResult ToApiError(ControllerBase controller)
    {
        switch (Status)
        {
            case ServiceStatus.NotFound:
                return controller.NotFound(new ProblemDetails { Status = 404, Title = "The requested record was not found." });
            case ServiceStatus.Forbidden:
                return new ObjectResult(new ProblemDetails { Status = 403, Title = "Forbidden", Detail = FirstError })
                { StatusCode = 403 };
            case ServiceStatus.Conflict:
                return new ObjectResult(new ValidationProblemDetails(ToArrayMap())
                { Status = 409, Title = "The record conflicts with an existing record." })
                { StatusCode = 409 };
            default:
                return new BadRequestObjectResult(new ValidationProblemDetails(ToArrayMap())
                { Status = 400, Title = "One or more validation errors occurred." });
        }
    }

    private Dictionary<string, string[]> ToArrayMap() =>
        Errors.ToDictionary(e => string.IsNullOrEmpty(e.Key) ? "general" : e.Key, e => e.Value.ToArray());
}

public class ServiceResult<T> : ServiceResult
{
    public T? Value { get; private init; }

    public static ServiceResult<T> Ok(T value) => new() { Value = value };
    public static new ServiceResult<T> NotFound() => new() { Status = ServiceStatus.NotFound };
    public static new ServiceResult<T> Forbidden(string message = "You are not authorised to perform this action.") =>
        new() { Status = ServiceStatus.Forbidden, Errors = ErrorMap(string.Empty, message) };
    public static new ServiceResult<T> Invalid(Dictionary<string, List<string>> errors) =>
        new() { Status = ServiceStatus.ValidationFailed, Errors = errors };
    public static new ServiceResult<T> Invalid(string field, string message) => Invalid(ErrorMap(field, message));
    public static new ServiceResult<T> Conflict(Dictionary<string, List<string>> errors) =>
        new() { Status = ServiceStatus.Conflict, Errors = errors };

    public static ServiceResult<T> From(ServiceResult other) => new() { Status = other.Status, Errors = other.Errors };
}

/// <summary>Collects field errors during validation.</summary>
public class ValidationErrors
{
    public Dictionary<string, List<string>> Errors { get; } = new();

    public bool Any => Errors.Count > 0;

    public void Add(string field, string message)
    {
        if (!Errors.TryGetValue(field, out var list))
        {
            list = new List<string>();
            Errors[field] = list;
        }

        if (!list.Contains(message))
        {
            list.Add(message);
        }
    }
}
