using System.Globalization;
using CoffeeShop.Domain.Entities;
using CoffeeShop.Domain.Pricing;

namespace CoffeeShop.Domain.Tests;

public class PriceResolverTests
{
    private static readonly DateTimeOffset Jan = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Jun = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Next = new(2027, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static EffectivePrice Price(int id, decimal amount, DateTimeOffset from) =>
        new(id, skuId: 1, amount, "EUR", from);

    [Fact]
    public void Future_price_does_not_apply_yet()
    {
        var prices = new[] { Price(1, 12m, Jan), Price(2, 15m, Next) };

        Assert.Equal(12m, PriceResolver.Resolve(prices, Next.AddTicks(-1)).Amount);
    }

    [Fact]
    public void Price_changes_exactly_on_its_effective_date()
    {
        var prices = new[] { Price(1, 12m, Jan), Price(2, 14m, Jun) };

        Assert.Equal(12m, PriceResolver.Resolve(prices, Jun.AddTicks(-1)).Amount);
        Assert.Equal(14m, PriceResolver.Resolve(prices, Jun).Amount);
    }

    [Theory]
    [InlineData("2026-03-01T00:00:00Z", 12)]
    [InlineData("2026-07-01T00:00:00Z", 14)]
    [InlineData("2027-02-01T00:00:00Z", 15)]
    public void Picks_latest_applicable_price_regardless_of_input_order(string asOf, int expected)
    {
        var prices = new[] { Price(3, 15m, Next), Price(1, 12m, Jan), Price(2, 14m, Jun) };

        var resolved = PriceResolver.Resolve(prices, DateTimeOffset.Parse(asOf, CultureInfo.InvariantCulture));

        Assert.Equal(expected, resolved.Amount);
    }

    [Fact]
    public void Compares_instants_across_offsets()
    {
        var prices = new[] { Price(1, 12m, Jan), Price(2, 14m, Jun) };
        var justBeforeJunUtc = new DateTimeOffset(2026, 6, 1, 1, 59, 0, TimeSpan.FromHours(2));

        Assert.Equal(12m, PriceResolver.Resolve(prices, justBeforeJunUtc).Amount);
    }

    [Fact]
    public void No_effective_price_throws_domain_error()
    {
        var prices = new[] { Price(1, 12m, Jan) };

        var ex = Assert.Throws<NoEffectivePriceException>(() => PriceResolver.Resolve(prices, Jan.AddTicks(-1)));
        Assert.IsAssignableFrom<DomainException>(ex);
    }

    [Fact]
    public void Prices_from_different_skus_are_rejected()
    {
        var prices = new[] { Price(1, 12m, Jan), new EffectivePrice(2, skuId: 2, 14m, "EUR", Jun) };

        Assert.Throws<ArgumentException>(() => PriceResolver.Resolve(prices, Jun));
    }

    [Theory]
    [InlineData(-0.01, "EUR")]
    [InlineData(1, "eur")]
    [InlineData(1, "EURO")]
    [InlineData(1, "")]
    public void Invalid_prices_are_rejected(double amount, string currency) =>
        Assert.Throws<InvalidPriceException>(() => new EffectivePrice(1, 1, (decimal)amount, currency, Jan));

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1, -1)]
    public void Invalid_order_lines_are_rejected(int qty, int unitPrice) =>
        Assert.Throws<InvalidOrderLineException>(() => new OrderLine(1, 1, 1, qty, unitPrice, 1));

    [Fact]
    public void Empty_history_throws_domain_error()
    {
        Assert.Throws<NoEffectivePriceException>(() => PriceResolver.Resolve([], Jan));
    }
}
