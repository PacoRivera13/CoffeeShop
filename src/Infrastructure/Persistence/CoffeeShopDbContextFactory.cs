using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CoffeeShop.Infrastructure.Persistence;

/// <summary>Lets dotnet ef work without starting the API, which requires the JWT configuration.</summary>
internal sealed class CoffeeShopDbContextFactory : IDesignTimeDbContextFactory<CoffeeShopDbContext>
{
    private const string LocalDb = @"Server=(localdb)\MSSQLLocalDB;Database=CoffeeShop;Trusted_Connection=True;TrustServerCertificate=True";

    public CoffeeShopDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default") ?? LocalDb;

        return new CoffeeShopDbContext(
            new DbContextOptionsBuilder<CoffeeShopDbContext>().UseSqlServer(connectionString).Options);
    }
}
