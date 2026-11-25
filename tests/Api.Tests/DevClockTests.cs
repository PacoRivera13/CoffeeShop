using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using CoffeeShop.Domain.Entities;
using CoffeeShop.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CoffeeShop.Api.Tests;

[Collection(MsSqlCollection.Name)]
public partial class DevClockTests(MsSqlFixture fixture)
{
    [Fact]
    public async Task An_eight_day_offset_makes_the_scheduled_Yirgacheffe_price_current()
    {
        await using var shifted = await fixture.CreateDemoDatabaseFactoryAsync(("Dev:ClockOffsetDays", "8"));
        int skuId;
        using (var scope = shifted.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
            skuId = await db.Skus
                .Where(s => s.Grind == Grind.WholeBean && s.SizeG == 500
                    && s.ProductId == db.Products.Where(p => p.Name == "Yirgacheffe").Max(p => p.Id))
                .Select(s => s.Id).SingleAsync();
        }

        var shiftedPrice = await shifted.CreateClient().GetFromJsonAsync<PriceResponse>($"/skus/{skuId}/price");
        var realPrice = await (await fixture.DemoFactoryAsync()).CreateClient().GetFromJsonAsync<PriceResponse>($"/skus/{skuId}/price");

        Assert.Equal(284.90m, shiftedPrice!.Amount);
        Assert.Equal(259m, realPrice!.Amount);
    }

    [Fact]
    public async Task Tokens_stay_on_the_real_clock_while_the_app_clock_is_shifted()
    {
        await using var shifted = await fixture.CreateDemoDatabaseFactoryAsync(("Dev:ClockOffsetDays", "8"));

        var customer = await MsSqlFixture.ClientForAsync(shifted, MsSqlFixture.DemoCustomerEmail, MsSqlFixture.DemoCustomerPassword);

        Assert.Equal(HttpStatusCode.OK, (await customer.GetAsync("/orders")).StatusCode);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void The_offset_is_rejected_outside_Development(string environment)
    {
        using var factory = fixture.CreateFactory(("Dev:ClockOffsetDays", "8"))
            .WithWebHostBuilder(host => host.UseEnvironment(environment));

        var error = Assert.ThrowsAny<Exception>(() => factory.Server);

        Assert.Contains("Dev__ClockOffsetDays is only allowed", Flatten(error));
    }

    [Fact]
    public void A_malformed_offset_fails_startup()
    {
        using var factory = fixture.CreateFactory(("Dev:ClockOffsetDays", "next week"));

        var error = Assert.ThrowsAny<Exception>(() => factory.Server);

        Assert.Contains("whole number of days", Flatten(error));
    }

    /// <summary>Every "now" must come from the injected TimeProvider, so the dev clock and test clocks reach all of it.</summary>
    [Fact]
    public void Source_never_reads_the_system_clock_directly()
    {
        var src = Path.Combine(RepositoryRoot(), "src");
        var offenders = Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}")
                && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(f => SystemClock().IsMatch(File.ReadAllText(f)))
            .Select(f => Path.GetRelativePath(src, f))
            .ToList();

        Assert.Empty(offenders);
    }

    [GeneratedRegex(@"\bDate(Time|TimeOffset)\.(UtcNow|Now|Today)\b")]
    private static partial Regex SystemClock();

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CoffeeShop.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private static string Flatten(Exception error) =>
        error.InnerException is null ? error.Message : $"{error.Message} {Flatten(error.InnerException)}";
}
