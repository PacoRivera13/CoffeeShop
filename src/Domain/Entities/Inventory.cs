namespace CoffeeShop.Domain.Entities;

public class Inventory
{
    private Inventory() { }

    public Inventory(int productId, int stockG)
    {
        ProductId = productId;
        StockG = stockG;
    }

    public int ProductId { get; private set; }
    public int StockG { get; private set; }
}
