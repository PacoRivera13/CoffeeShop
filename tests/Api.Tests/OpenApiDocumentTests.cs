using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CoffeeShop.Api.Tests;

public class OpenApiDocumentTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private const string DocumentUrl = "/openapi/v1.json";

    private async Task<JsonNode> GetDocumentAsync()
    {
        var document = JsonNode.Parse(await factory.CreateClient().GetStringAsync(DocumentUrl))!;
        document.AsObject().Remove("servers");
        return document;
    }

    [Fact]
    public async Task Document_describes_the_catalog_endpoints()
    {
        var paths = (await GetDocumentAsync())["paths"]!;

        var products = paths["/products"]!["get"]!["responses"]!.AsObject();
        Assert.Contains("200", products.Select(r => r.Key));

        var price = paths["/skus/{id}/price"]!["get"]!;
        Assert.Equal(["200", "400", "404"], price["responses"]!.AsObject().Select(r => r.Key).Order());
        var asOf = price["parameters"]!.AsArray().Single(p => p!["name"]!.GetValue<string>() == "asOf")!;
        Assert.Contains("ISO 8601", asOf["description"]!.GetValue<string>());
    }

    /// <summary>After an intended API change, regenerate with UPDATE_OPENAPI=1 dotnet test --filter Committed_document.</summary>
    [Fact]
    public async Task Committed_document_matches_the_api()
    {
        var path = Path.Combine(FindRepositoryRoot(), "openapi.json");
        var document = await GetDocumentAsync();

        if (Environment.GetEnvironmentVariable("UPDATE_OPENAPI") == "1")
            await File.WriteAllTextAsync(path, document.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");

        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(await File.ReadAllTextAsync(path)), document),
            "openapi.json is out of date. Run: UPDATE_OPENAPI=1 dotnet test --filter Committed_document");
    }

    [Fact]
    public async Task Swagger_ui_is_available_in_development()
    {
        var response = await factory.CreateClient().GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/swagger/index.html")]
    [InlineData(DocumentUrl)]
    public async Task Documentation_is_not_exposed_in_production(string url)
    {
        var client = factory.WithWebHostBuilder(host => host
            .UseEnvironment("Production")
            .UseSetting("ConnectionStrings:Default", "Server=unused"))
            .CreateClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CoffeeShop.slnx")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
