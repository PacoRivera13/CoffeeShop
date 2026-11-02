using CoffeeShop.Domain.Entities;

namespace CoffeeShop.Infrastructure.Persistence;

internal static class SeedData
{
    private const string Currency = "EUR";
    private static readonly DateTimeOffset Launch = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static readonly Product[] Products =
    [
        new(1, "Yirgacheffe", "Ethiopia", "Light"),
        new(2, "Huila", "Colombia", "Medium"),
        new(3, "Mandheling", "Indonesia", "Dark"),
    ];

    public static readonly Sku[] Skus =
    [
        new(1, 1, Grind.WholeBean, 500),
        new(2, 1, Grind.WholeBean, 1000),
        new(3, 1, Grind.Ground, 500),
        new(4, 1, Grind.Ground, 1000),
        new(5, 2, Grind.WholeBean, 500),
        new(6, 2, Grind.WholeBean, 1000),
        new(7, 2, Grind.Ground, 500),
        new(8, 2, Grind.Ground, 1000),
        new(9, 3, Grind.WholeBean, 500),
        new(10, 3, Grind.WholeBean, 1000),
        new(11, 3, Grind.Ground, 500),
        new(12, 3, Grind.Ground, 1000),
    ];

    public static readonly EffectivePrice[] Prices =
    [
        new(1, 1, 12.00m, Currency, Launch),
        new(2, 2, 22.00m, Currency, Launch),
        new(3, 3, 12.50m, Currency, Launch),
        new(4, 4, 23.00m, Currency, Launch),
        new(5, 5, 10.00m, Currency, Launch),
        new(6, 6, 18.50m, Currency, Launch),
        new(7, 7, 10.50m, Currency, Launch),
        new(8, 8, 19.50m, Currency, Launch),
        new(9, 9, 11.00m, Currency, Launch),
        new(10, 10, 20.00m, Currency, Launch),
        new(11, 11, 11.50m, Currency, Launch),
        new(12, 12, 21.00m, Currency, Launch),
        new(13, 1, 14.00m, Currency, new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero)),
        new(14, 1, 15.00m, Currency, new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero)),
    ];
}
