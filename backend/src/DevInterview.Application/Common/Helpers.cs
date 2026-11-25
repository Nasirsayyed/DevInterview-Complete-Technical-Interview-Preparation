using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DevInterview.Contracts.Common;
using Microsoft.EntityFrameworkCore;

namespace DevInterview.Application.Common;

public static partial class Slug
{
    public static string From(string text)
    {
        var normalized = text.Trim().ToLowerInvariant()
            .Replace("c#", "csharp").Replace("c++", "cpp").Replace(".net", "dotnet").Replace("f#", "fsharp");
        normalized = NonAlnum().Replace(normalized, "-").Trim('-');
        return normalized.Length > 120 ? normalized[..120].TrimEnd('-') : normalized;
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlnum();
}

public static class EnumParser
{
    public static TEnum Parse<TEnum>(string? value, string field) where TEnum : struct, Enum
    {
        if (TryParse<TEnum>(value, out var result)) return result;
        throw ValidationAppException.For(field,
            $"'{value}' is not a valid {field}. Allowed: {string.Join(", ", Enum.GetNames<TEnum>())}.");
    }

    public static TEnum? ParseOptional<TEnum>(string? value, string field) where TEnum : struct, Enum =>
        string.IsNullOrWhiteSpace(value) ? null : Parse<TEnum>(value, field);

    public static bool TryParse<TEnum>(string? value, out TEnum result) where TEnum : struct, Enum
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var compact = value.Replace("-", "").Replace("_", "").Replace(" ", "");
        return Enum.TryParse(compact, true, out result) && Enum.IsDefined(result) && !int.TryParse(compact, out _);
    }
}

public static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T? Deserialize<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return default;
        try { return JsonSerializer.Deserialize<T>(json, Options); }
        catch (JsonException) { return default; }
    }

    public static IReadOnlyList<string> StringList(string? json) => Deserialize<List<string>>(json) ?? new List<string>();
}

public static class QueryableExtensions
{
    public static async Task<PagedResult<T>> ToPagedAsync<T>(this IQueryable<T> query, PageQuery page, CancellationToken ct)
    {
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page.Page - 1) * page.PageSize).Take(page.PageSize).ToListAsync(ct);
        return new PagedResult<T>(items, page.Page, page.PageSize, total);
    }

    public static PagedResult<TOut> Map<TIn, TOut>(this PagedResult<TIn> source, Func<TIn, TOut> map) =>
        new(source.Items.Select(map).ToList(), source.Page, source.PageSize, source.TotalCount);
}

public static class TimeZones
{
    public static TimeZoneInfo Find(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return TimeZoneInfo.Utc;
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException) { return TimeZoneInfo.Utc; }
    }

    public static bool IsValid(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        try { TimeZoneInfo.FindSystemTimeZoneById(id); return true; }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException) { return false; }
    }

    public static DateOnly LocalDate(DateTime utc, string? timeZoneId) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Find(timeZoneId)));

    /// <summary>Monday of the ISO week containing the date.</summary>
    public static DateOnly WeekStart(DateOnly date) =>
        date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
}

public static class Text
{
    public static string Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) ? string.Empty : value.Length <= max ? value : value[..max] + "…";

    public static string Csv(string? value)
    {
        var v = value ?? string.Empty;
        return v.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? $"\"{v.Replace("\"", "\"\"")}\"" : v;
    }

    public static string Percent(decimal value) => value.ToString("0.#", CultureInfo.InvariantCulture);

    public static string StripControl(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var c in value) if (!char.IsControl(c) || c is '\n' or '\r' or '\t') sb.Append(c);
        return sb.ToString();
    }
}
