namespace Domain.Entities;

public static class Money
{
    /// <summary>Rounds to cents, half away from zero (0.005 → 0.01), the usual commercial rule.</summary>
    public static decimal Round(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);

    /// <summary>Number of significant decimal places (1.50 → 1, 1.234 → 3).</summary>
    public static int Scale(decimal value)
    {
        var normalized = value / 1.0000000000000000000000000000m;
        return (decimal.GetBits(normalized)[3] >> 16) & 0xFF;
    }
}
