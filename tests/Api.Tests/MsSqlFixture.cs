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

    public const string DemoCustomerEmail = "demo-customer@example.test";
    public const string DemoCustomerPassword = "Demo!Passw0rd";

    private Task<WebApplicationFactory<Program>>? _demoFactory;

    public string MinioEndpoint => _minio.GetConnectionString();
    public string PublicMinioEndpoint => new UriBuilder(_minio.GetConnectionString()) { Host = "localhost" }.Uri.ToString();
    public string MinioAccessKey => _minio.GetAccessKey();
    public string MinioSecretKey => _minio.GetSecretKey();

    /// <summary>A dedicated database: the container's default is master, where READ_COMMITTED_SNAPSHOT cannot be enabled.</summary>
    private string ConnectionString => ConnectionStringFor("CoffeeShopTests");

    private string ConnectionStringFor(string database) =>
        new SqlConnectionStringBuilder(_container.GetConnectionString()) { InitialCatalog = database }.ConnectionString;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_container.StartAsync(), _minio.StartAsync());
        await MigrateAsync(ConnectionString);

        Factory = CreateFactory(("Admin:Email", AdminEmail), ("Admin:Password", AdminPassword));
    }

    private static async Task MigrateAsync(string connectionString)
    {
        await using var db = new CoffeeShopDbContext(
            new DbContextOptionsBuilder<CoffeeShopDbContext>().UseSqlServer(connectionString).Options);
        await db.Database.MigrateAsync();
    }

    /// <summary>
    /// An API seeded with the demo data on its own database in the same container, so demo rows never reach the
    /// database the other tests share. Created once and reused.
    /// </summary>
    public Task<WebApplicationFactory<Program>> DemoFactoryAsync() => _demoFactory ??= CreateDemoFactoryAsync();

    /// <summary>Another API over the already seeded demo database, with extra settings (the seed itself is skipped).</summary>
    public async Task<WebApplicationFactory<Program>> CreateDemoDatabaseFactoryAsync(params (string Key, string Value)[] settings)
    {
        await DemoFactoryAsync();
        return CreateFactory([("ConnectionStrings:Default", ConnectionStringFor(DemoDatabase)), .. settings]);
    }

    private const string DemoDatabase = "CoffeeShopDemo";

    private async Task<WebApplicationFactory<Program>> CreateDemoFactoryAsync()
    {
        var connectionString = ConnectionStringFor(DemoDatabase);
        await MigrateAsync(connectionString);

        var factory = CreateFactory(
            ("ConnectionStrings:Default", connectionString),
            ("Seed:Demo", "true"),
            ("Seed:CustomerEmail", DemoCustomerEmail),
            ("Seed:CustomerPassword", DemoCustomerPassword));
        _ = factory.Server; // Starts the host, which runs the demo seed.
        return factory;
    }

    public WebApplicationFactory<Program> CreateFactory(params (string Key, string Value)[] settings) =>
        new TestApiFactory().WithWebHostBuilder(host =>
        {
            host.UseSetting("ConnectionStrings:Default", ConnectionString);
            host.UseSetting("Storage:Endpoint", _minio.GetConnectionString());
            // Same server under another host name, so tests can tell public URLs from the internal endpoint.
            host.UseSetting("Storage:PublicEndpoint", PublicMinioEndpoint);
            host.UseSetting("Storage:Bucket", InvoiceBucket);
            host.UseSetting("Storage:AccessKey", _minio.GetAccessKey());
            host.UseSetting("Storage:SecretKey", _minio.GetSecretKey());
            host.UseSetting("Storage:CreateBucketIfMissing", "true");
            host.ConfigureTestServices(OutboxTestDoubles.Register);
            foreach (var (key, value) in settings)
                host.UseSetting(key, value);
        });

    public Task<HttpClient> AdminClientAsync() => ClientForAsync(Factory, AdminEmail, AdminPassword);

    public Task<HttpClient> NewCustomerClientAsync() => NewCustomerClientAsync(Factory);

    public async Task<HttpClient> NewCustomerClientAsync(WebApplicationFactory<Program> factory)
    {
        var email = $"{Guid.NewGuid():N}@example.test";
        var registered = await factory.CreateClient()
            .PostAsJsonAsync("/auth/register", new RegisterRequest(email, CustomerPassword, "Test Customer"));
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        return await ClientForAsync(factory, email, CustomerPassword);
    }

    public static async Task<HttpClient> ClientForAsync(WebApplicationFactory<Program> factory, string email, string password)
    {
        var login = await factory.CreateClient().PostAsJsonAsync("/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = (await login.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken;

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public async Task DisposeAsync()
    {
        if (_demoFactory is not null)
            await (await _demoFactory).DisposeAsync();
        await Factory.DisposeAsync();
        await _container.DisposeAsync();
        await _minio.DisposeAsync();
    }
}
