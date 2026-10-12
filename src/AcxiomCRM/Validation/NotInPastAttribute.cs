using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace AcxiomCRM.Validation;

/// <summary>
/// The date part must be today or later. Optionally skipped when another property has one of the
/// given values (e.g. an opportunity whose Stage is Won or Lost is no longer active).
/// Works server-side and client-side (adapter "notinpast" in site.js).
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class NotInPastAttribute : ValidationAttribute, IClientModelValidator
{
    public NotInPastAttribute() : base("{0} cannot be in the past.")
    {
    }

    /// <summary>Name of a sibling property that can switch the rule off.</summary>
    public string? UnlessProperty { get; set; }

    /// <summary>Comma separated values of <see cref="UnlessProperty"/> for which the rule does not apply.</summary>
    public string? UnlessValues { get; set; }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not DateTime date)
        {
            return ValidationResult.Success; // [Required] handles missing values.
        }

        if (IsSuspended(validationContext.ObjectInstance))
        {
            return ValidationResult.Success;
        }

        return date.Date < DateTime.Today
            ? new ValidationResult(FormatErrorMessage(validationContext.DisplayName), new[] { validationContext.MemberName ?? string.Empty })
            : ValidationResult.Success;
    }

    private bool IsSuspended(object instance)
    {
        if (string.IsNullOrEmpty(UnlessProperty) || string.IsNullOrEmpty(UnlessValues))
        {
            return false;
        }

        var other = instance.GetType().GetProperty(UnlessProperty)?.GetValue(instance)?.ToString();
        return other != null && UnlessValues.Split(',').Any(v => string.Equals(v.Trim(), other, StringComparison.OrdinalIgnoreCase));
    }

    public void AddValidation(ClientModelValidationContext context)
    {
        Merge(context.Attributes, "data-val", "true");
        Merge(context.Attributes, "data-val-notinpast", FormatErrorMessage(context.ModelMetadata.GetDisplayName()));
        if (!string.IsNullOrEmpty(UnlessProperty))
        {
            Merge(context.Attributes, "data-val-notinpast-unlessproperty", UnlessProperty);
            Merge(context.Attributes, "data-val-notinpast-unlessvalues", UnlessValues ?? string.Empty);
        }
    }

    private static void Merge(IDictionary<string, string> attributes, string key, string value)
    {
        attributes.TryAdd(key, value);
    }
}
