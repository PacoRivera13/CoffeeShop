namespace CoffeeShop.Domain.Entities;

public class Sku
{
    private Sku() { }

    public Sku(int id, int productId, Grind grind, int sizeG)
    {
        if (sizeG is not (500 or 1000))
            throw new InvalidSkuSizeException(sizeG);

        Id = id;
        ProductId = productId;
        Grind = grind;
        SizeG = sizeG;
    }

    public int Id { get; private set; }
    public int ProductId { get; private set; }
    public Grind Grind { get; private set; }
    public int SizeG { get; private set; }
}

public sealed class InvalidSkuSizeException(int sizeG)
    : DomainException($"SKU size must be 500 or 1000 grams, got {sizeG}.");
