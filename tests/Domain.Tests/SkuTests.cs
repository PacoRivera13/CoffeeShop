using CoffeeShop.Domain.Entities;

namespace CoffeeShop.Domain.Tests;

public class SkuTests
{
    [Theory]
    [InlineData(500)]
    [InlineData(1000)]
    public void Accepts_allowed_sizes(int sizeG) =>
        Assert.Equal(sizeG, new Sku(1, 1, Grind.Ground, sizeG).SizeG);

    [Theory]
    [InlineData(0)]
    [InlineData(250)]
    public void Rejects_other_sizes(int sizeG) =>
        Assert.Throws<InvalidSkuSizeException>(() => new Sku(1, 1, Grind.Ground, sizeG));
}
