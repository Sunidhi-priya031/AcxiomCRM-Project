using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AcxiomCRM.Infrastructure;

/// <summary>Captures the scalar state of an entity for audit old/new values.</summary>
public static class AuditSnapshot
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly HashSet<Type> ScalarTypes = new()
    {
        typeof(string), typeof(int), typeof(long), typeof(decimal), typeof(double), typeof(bool), typeof(DateTime)
    };

    /// <summary>Security-sensitive fields that must never be written to the audit log.</summary>
    private static readonly HashSet<string> SensitiveFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "PasswordHash", "SecurityStamp", "ConcurrencyStamp", "Password", "ConfirmPassword", "NewPassword",
        "CurrentPassword", "Token", "AccessToken", "RefreshToken"
    };

    public static Dictionary<string, object?> Of(object entity)
    {
        var result = new Dictionary<string, object?>();
        foreach (var prop in entity.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.GetCustomAttribute<NotMappedAttribute>() != null || !prop.CanWrite || SensitiveFields.Contains(prop.Name))
            {
                continue;
            }

            var type = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
            if (!ScalarTypes.Contains(type) && !type.IsEnum)
            {
                continue;
            }

            var value = prop.GetValue(entity);
            result[prop.Name] = value is Enum e ? e.ToString() : value;
        }

        return result;
    }

    /// <summary>Returns only the fields whose values changed.</summary>
    public static (Dictionary<string, object?> Old, Dictionary<string, object?> New) Diff(
        Dictionary<string, object?> before, Dictionary<string, object?> after)
    {
        var oldValues = new Dictionary<string, object?>();
        var newValues = new Dictionary<string, object?>();
        foreach (var (key, newValue) in after)
        {
            before.TryGetValue(key, out var oldValue);
            if (!Equals(oldValue, newValue) && key != "ModifiedDate")
            {
                oldValues[key] = oldValue;
                newValues[key] = newValue;
            }
        }

        return (oldValues, newValues);
    }

    public static string? ToJson(object? value)
    {
        if (value == null)
        {
            return null;
        }

        if (value is string s)
        {
            return s;
        }

        if (value is Dictionary<string, object?> dict && dict.Count == 0)
        {
            return null;
        }

        return JsonSerializer.Serialize(value, JsonOptions);
    }
}
