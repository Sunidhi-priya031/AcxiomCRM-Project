using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Validation;

/// <summary>
/// Rejects enum values that are not defined. Model binding happily binds "99" to an enum, so a
/// tampered request could otherwise store an invalid status.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ValidEnumAttribute : ValidationAttribute
{
    public ValidEnumAttribute() : base("Select a valid {0}.")
    {
    }

    public override bool IsValid(object? value) =>
        value == null || (value.GetType().IsEnum && Enum.IsDefined(value.GetType(), value));
}
