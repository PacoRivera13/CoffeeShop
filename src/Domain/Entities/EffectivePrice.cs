namespace CoffeeShop.Domain.Entities;

public class EffectivePrice
{
    private EffectivePrice() { }

    public EffectivePrice(int id, int skuId, decimal amount, string currency, DateTimeOffset effectiveFrom)
    {
        if (amount < 0)
            throw new InvalidPriceException($"Price amount cannot be negative, got {amount}.");
        if (currency is not { Length: 3 } || !currency.All(char.IsAsciiLetterUpper))
            throw new InvalidPriceException($"Currency must be a 3-letter uppercase code, got '{currency}'.");

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
