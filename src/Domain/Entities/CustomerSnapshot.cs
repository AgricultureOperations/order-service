using System.Text.RegularExpressions;
using Domain.Exceptions;

namespace Domain.Entities;

/// <summary>
/// The buyer as written on the order. Buyers are external parties, not AgriOps users, so the order keeps
/// its own copy instead of a reference into auth-service.
/// </summary>
public sealed record CustomerSnapshot
{
    private static readonly Regex EmailPattern = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
    private static readonly Regex TaxIdPattern = new(@"^[A-Za-z0-9-]{5,20}$", RegexOptions.Compiled);

    public string Name { get; }
    public string? TaxId { get; }
    public string? Email { get; }
    public string? Phone { get; }

    public CustomerSnapshot(string? name, string? taxId, string? email, string? phone)
    {
        Name = name?.Trim() ?? string.Empty;
        if (Name.Length < 2) throw new DomainValidationException("Customer name must have at least 2 characters.");
        if (Name.Length > 200) throw new DomainValidationException("Customer name is longer than 200 characters.");

        TaxId = Optional(taxId);
        if (TaxId is not null && !TaxIdPattern.IsMatch(TaxId))
            throw new DomainValidationException("Customer tax ID must be 5 to 20 letters, digits or dashes.");

        Email = Optional(email);
        if (Email is not null && (Email.Length > 254 || !EmailPattern.IsMatch(Email)))
            throw new DomainValidationException("Customer email is not valid.");

        Phone = Optional(phone);
        if (Phone is not null && Phone.Length > 30)
            throw new DomainValidationException("Customer phone is longer than 30 characters.");
    }

    private static string? Optional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
