namespace CoffeeShop.Domain.Entities;

public class EffectivePrice
{
    private const int MaxDecimals = 2;
    private const decimal MaxAmountExclusive = 10_000_000_000_000_000m;

    private EffectivePrice() { }

    public EffectivePrice(int id, int skuId, decimal amount, string currency, DateTimeOffset effectiveFrom)
    {
        if (amount <= 0)
            throw new InvalidPriceException($"Price amount must be positive, got {amount}.");
        if (amount >= MaxAmountExclusive || decimal.Round(amount, MaxDecimals) != amount)
            throw new InvalidPriceException($"Price amount must have at most 16 integer digits and {MaxDecimals} decimals, got {amount}.");
        if (!IsoCurrencies.IsValid(currency))
            throw new InvalidPriceException($"Currency must be an uppercase ISO 4217 code, got '{currency}'.");

        Id = id;
        SkuId = skuId;
        Amount = amount;
        Currency = currency;
        EffectiveFrom = effectiveFrom;
    }

    public int Id { get; private set; }
    public int SkuId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = null!;
    public DateTimeOffset EffectiveFrom { get; private set; }
}

public sealed class InvalidPriceException(string message) : DomainException(message);
