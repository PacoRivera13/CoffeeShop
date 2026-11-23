using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CoffeeShop.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Testcontainers.Minio;
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
    // Docker Hub and Quay no longer serve MinIO images; Chainguard's maintained build is pinned by digest.
    public const string MinioImage = "cgr.dev/chainguard/minio@sha256:bd92a45b821c0d85772c607ed7b49693efe78b3ffffae301b5d1b0981660de22";
    public const string InvoiceBucket = "coffeeshop-invoices";

    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
    private readonly MinioContainer _minio = new MinioBuilder(MinioImage).Build();

    private const string CustomerPassword = "Str0ng!Passw0rd";

    public string AdminEmail { get; } = $"{Guid.NewGuid():N}@example.test";
    public string AdminPassword { get; } = $"{Guid.NewGuid():N}Aa1!";

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public string MinioEndpoint => _minio.GetConnectionString();
    public string MinioAccessKey => _minio.GetAccessKey();
    public string MinioSecretKey => _minio.GetSecretKey();

    /// <summary>A dedicated database: the container's default is master, where READ_COMMITTED_SNAPSHOT cannot be enabled.</summary>
    private string ConnectionString =>
        new SqlConnectionStringBuilder(_container.GetConnectionString()) { InitialCatalog = "CoffeeShopTests" }.ConnectionString;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_container.StartAsync(), _minio.StartAsync());

        await using var db = new CoffeeShopDbContext(
            new DbContextOptionsBuilder<CoffeeShopDbContext>().UseSqlServer(ConnectionString).Options);
        await db.Database.MigrateAsync();

        Factory = CreateFactory(("Admin:Email", AdminEmail), ("Admin:Password", AdminPassword));
    }

    public WebApplicationFactory<Program> CreateFactory(params (string Key, string Value)[] settings) =>
        new TestApiFactory().WithWebHostBuilder(host =>
        {
            host.UseSetting("ConnectionStrings:Default", ConnectionString);
            host.UseSetting("Storage:Endpoint", _minio.GetConnectionString());
            host.UseSetting("Storage:Bucket", InvoiceBucket);
            host.UseSetting("Storage:AccessKey", _minio.GetAccessKey());
            host.UseSetting("Storage:SecretKey", _minio.GetSecretKey());
            host.UseSetting("Storage:CreateBucketIfMissing", "true");
            host.ConfigureTestServices(OutboxTestDoubles.Register);
            foreach (var (key, value) in settings)
                host.UseSetting(key, value);
        });

    public Task<HttpClient> AdminClientAsync() => ClientForAsync(AdminEmail, AdminPassword);

    public async Task<HttpClient> NewCustomerClientAsync()
    {
        var email = $"{Guid.NewGuid():N}@example.test";
        var registered = await Factory.CreateClient()
            .PostAsJsonAsync("/auth/register", new RegisterRequest(email, CustomerPassword, "Test Customer"));
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        return await ClientForAsync(email, CustomerPassword);
    }

    private async Task<HttpClient> ClientForAsync(string email, string password)
    {
        var login = await Factory.CreateClient().PostAsJsonAsync("/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = (await login.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken;

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _container.DisposeAsync();
        await _minio.DisposeAsync();
    }
}
