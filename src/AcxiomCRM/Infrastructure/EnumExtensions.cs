using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace AcxiomCRM.Infrastructure;

public static class EnumExtensions
{
    public static string GetDisplayName(this Enum value)
    {
        var member = value.GetType().GetMember(value.ToString()).FirstOrDefault();
        return member?.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? value.ToString();
    }

    public static IEnumerable<TEnum> Values<TEnum>() where TEnum : struct, Enum => Enum.GetValues<TEnum>();
}
