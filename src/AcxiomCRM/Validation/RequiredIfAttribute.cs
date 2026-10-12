using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace AcxiomCRM.Validation;

/// <summary>
/// Field is required when another property equals a target value (e.g. new-customer fields during lead conversion).
/// Works server-side and client-side (adapter "requiredif" in site.js).
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class RequiredIfAttribute : ValidationAttribute, IClientModelValidator
{
    public RequiredIfAttribute(string dependentProperty, object targetValue) : base("{0} is required.")
    {
        DependentProperty = dependentProperty;
        TargetValue = targetValue;
    }

    public string DependentProperty { get; }
    public object TargetValue { get; }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var dependent = validationContext.ObjectInstance.GetType().GetProperty(DependentProperty)?.GetValue(validationContext.ObjectInstance);
        if (!string.Equals(dependent?.ToString(), TargetValue.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return ValidationResult.Success;
        }

        var missing = value == null || (value is string s && string.IsNullOrWhiteSpace(s));
        return missing
            ? new ValidationResult(FormatErrorMessage(validationContext.DisplayName), new[] { validationContext.MemberName ?? string.Empty })
            : ValidationResult.Success;
    }

    public void AddValidation(ClientModelValidationContext context)
    {
        context.Attributes.TryAdd("data-val", "true");
        context.Attributes.TryAdd("data-val-requiredif", FormatErrorMessage(context.ModelMetadata.GetDisplayName()));
        context.Attributes.TryAdd("data-val-requiredif-dependentproperty", DependentProperty);
        context.Attributes.TryAdd("data-val-requiredif-targetvalue", TargetValue.ToString()!.ToLowerInvariant());
    }
}
