using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Infrastructure;

/// <summary>Writes report rows to CSV. Column headers come from [Display(Name)] on the row type.</summary>
public static class CsvExport
{
    public static FileContentResult ToFile<T>(IEnumerable<T> rows, string fileNamePrefix)
    {
        var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<CsvIgnoreAttribute>() == null)
            .ToArray();

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", props.Select(p => Escape(p.GetCustomAttribute<DisplayAttribute>()?.Name ?? p.Name))));
        foreach (var row in rows)
        {
            sb.AppendLine(string.Join(",", props.Select(p => Escape(Format(p.GetValue(row))))));
        }

        // UTF-8 with BOM so Excel opens non-ASCII names correctly.
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        var fileName = $"{fileNamePrefix}-{DateTime.Now:yyyyMMdd-HHmm}.csv";
        return new FileContentResult(bytes, "text/csv") { FileDownloadName = fileName };
    }

    private static string Format(object? value) => value switch
    {
        null => string.Empty,
        DateTime d => d.TimeOfDay == TimeSpan.Zero ? d.ToString("yyyy-MM-dd") : d.ToString("yyyy-MM-dd HH:mm"),
        decimal m => m.ToString("0.00", CultureInfo.InvariantCulture),
        double db => db.ToString("0.##", CultureInfo.InvariantCulture),
        Enum e => e.GetDisplayName(),
        bool b => b ? "Yes" : "No",
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
    };

    private static string Escape(string value)
    {
        // Neutralise spreadsheet formula injection.
        if (value.Length > 0 && "=+-@\t\r".Contains(value[0]) && !decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
        {
            value = "'" + value;
        }

        return value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class CsvIgnoreAttribute : Attribute
{
}
