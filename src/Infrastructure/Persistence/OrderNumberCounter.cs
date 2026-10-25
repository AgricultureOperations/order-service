namespace Infrastructure.Persistence;

/// <summary>
/// Last order number handed out per prefix and year (<c>ORD-2026-000042</c> → Prefix ORD, Year 2026, LastValue 42).
/// A new year or a new prefix starts at 1. Rows are only touched through OrderNumberGeneratorAdapter's upsert.
/// </summary>
public class OrderNumberCounter
{
    public string Prefix { get; set; } = string.Empty;
    public int Year { get; set; }
    public long LastValue { get; set; }
}
