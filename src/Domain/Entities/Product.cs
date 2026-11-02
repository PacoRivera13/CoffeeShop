namespace CoffeeShop.Domain.Entities;

public class Product
{
    private Product() { }

    public Product(int id, string name, string origin, string roast)
    {
        Id = id;
        Name = name;
        Origin = origin;
        Roast = roast;
    }

    public int Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string Origin { get; private set; } = null!;
    public string Roast { get; private set; } = null!;
    public List<Sku> Skus { get; private set; } = [];
}
