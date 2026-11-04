using CoffeeShop.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace CoffeeShop.Api.Tests;

[CollectionDefinition(Name)]
public sealed class MsSqlCollection : ICollectionFixture<MsSqlFixture>
{
    public const string Name = "SQL Server container";
}

/// <summary>One migrated SQL Server container shared by every test in the collection.</summary>
public sealed class MsSqlFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var db = new CoffeeShopDbContext(
            new DbContextOptionsBuilder<CoffeeShopDbContext>().UseSqlServer(_container.GetConnectionString()).Options);
        await db.Database.MigrateAsync();

        Factory = CreateFactory();
    }

    public WebApplicationFactory<Program> CreateFactory(params (string Key, string Value)[] settings) =>
        new TestApiFactory().WithWebHostBuilder(host =>
        {
            host.UseSetting("ConnectionStrings:Default", _container.GetConnectionString());
            foreach (var (key, value) in settings)
                host.UseSetting(key, value);
        });

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _container.DisposeAsync();
    }
}
