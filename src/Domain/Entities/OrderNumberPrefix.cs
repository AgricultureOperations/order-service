using System.Text.RegularExpressions;
using Domain.Exceptions;

namespace Domain.Entities;

/// <summary>The configurable first part of an order number: <c>{prefix}-{yyyy}-{000000}</c>.</summary>
public static class OrderNumberPrefix
{
    public const string Default = "ORD";
    private static readonly Regex Pattern = new("^[A-Z][A-Z0-9]{1,9}$", RegexOptions.Compiled);

    /// <summary>Upper-cases and validates a prefix: a letter followed by 1–9 letters or digits.</summary>
    public static string Normalize(string? value)
    {
        var prefix = value?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!Pattern.IsMatch(prefix))
            throw new DomainValidationException(
                "The order number prefix must be 2 to 10 characters: a letter followed by letters or digits (e.g. ORD).");
        return prefix;
    }

    public static string Format(string prefix, int year, long sequence) => $"{prefix}-{year:D4}-{sequence:D6}";
}
