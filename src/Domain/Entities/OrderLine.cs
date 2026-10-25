using Domain.Exceptions;

namespace Domain.Entities;

/// <summary>
/// A line of an order. The product fields are a snapshot taken when the line was written, because
/// order-service can't read product-service; later catalog changes never alter an existing order.
/// </summary>
public class OrderLine
{
    public const int MaxQuantityDecimals = 3;
    public const int MaxPriceDecimals = 2;

    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public string Sku { get; private set; } = string.Empty;
    public string ProductName { get; private set; } = string.Empty;
    public string Unit { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal DiscountAmount { get; private set; }
    /// <summary>Fraction between 0 and 1 (0.15 = 15% IVA).</summary>
    public decimal TaxRate { get; private set; }
    /// <summary>Quantity × unit price, rounded to cents.</summary>
    public decimal LineGross { get; private set; }
    /// <summary>Gross minus discount.</summary>
    public decimal LineSubtotal { get; private set; }
    public decimal LineTax { get; private set; }
    public decimal LineTotal { get; private set; }

    private OrderLine() {}

    public OrderLine(Guid productId, string sku, string productName, string unit,
        decimal quantity, decimal unitPrice, decimal discountAmount, decimal taxRate)
    {
        if (productId == Guid.Empty) throw new DomainValidationException("Each line needs a product.");
        Sku = Required(sku, "SKU", 64);
        ProductName = Required(productName, "Product name", 200);
        Unit = Required(unit, "Unit", 32);

        if (quantity <= 0) throw new DomainValidationException($"Quantity for {Sku} must be greater than 0.");
        if (Money.Scale(quantity) > MaxQuantityDecimals)
            throw new DomainValidationException($"Quantity for {Sku} allows at most {MaxQuantityDecimals} decimals.");
        if (unitPrice < 0) throw new DomainValidationException($"Unit price for {Sku} cannot be negative.");
        if (Money.Scale(unitPrice) > MaxPriceDecimals)
            throw new DomainValidationException($"Unit price for {Sku} allows at most {MaxPriceDecimals} decimals.");
        if (discountAmount < 0) throw new DomainValidationException($"Discount for {Sku} cannot be negative.");
        if (Money.Scale(discountAmount) > MaxPriceDecimals)
            throw new DomainValidationException($"Discount for {Sku} allows at most {MaxPriceDecimals} decimals.");
        if (taxRate < 0 || taxRate > 1) throw new DomainValidationException($"Tax rate for {Sku} must be between 0 and 1.");

        Id = Guid.NewGuid();
        ProductId = productId;
        Quantity = quantity;
        UnitPrice = unitPrice;
        DiscountAmount = discountAmount;
        TaxRate = taxRate;

        LineGross = Money.Round(quantity * unitPrice);
        if (discountAmount > LineGross)
            throw new DomainValidationException($"Discount for {Sku} cannot exceed the line amount ({LineGross}).");
        LineSubtotal = LineGross - discountAmount;
        LineTax = Money.Round(LineSubtotal * taxRate);
        LineTotal = LineSubtotal + LineTax;
    }

    private static string Required(string? value, string field, int maxLength)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0) throw new DomainValidationException($"{field} is required on every line.");
        if (trimmed.Length > maxLength) throw new DomainValidationException($"{field} is longer than {maxLength} characters.");
        return trimmed;
    }
}
