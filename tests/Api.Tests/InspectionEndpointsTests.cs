using System.Net;
using System.Net.Http.Json;
using static CoffeeShop.Api.Tests.OrderTestHelpers;

namespace CoffeeShop.Api.Tests;

/// <summary>GET /admin/outbox, GET /health and the X-Correlation-Id header.</summary>
[Collection(MsSqlCollection.Name)]
public class InspectionEndpointsTests(MsSqlFixture fixture)
{
    private async Task PlaceAnOrderAsync()
    {
        var product = await fixture.NewProductAsync();
        await PlaceOkAsync(await fixture.NewCustomerClientAsync(), Body((product.Sku500, 1)));
    }

    [Fact]
    public async Task Outbox_requires_a_token()
    {
        var response = await fixture.Factory.CreateClient().GetAsync("/admin/outbox");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Outbox_is_forbidden_to_customers()
    {
        var response = await (await fixture.NewCustomerClientAsync()).GetAsync("/admin/outbox");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Outbox_lists_messages_newest_first_for_admins()
    {
        await PlaceAnOrderAsync();
        await PlaceAnOrderAsync();
        var admin = await fixture.AdminClientAsync();

        var page = await admin.GetFromJsonAsync<OutboxPageResponse>("/admin/outbox");

        Assert.True(page!.TotalCount >= 2);
        Assert.Equal((1, 20), (page.Page, page.PageSize));
        Assert.Equal(page.Items.OrderByDescending(m => m.OccurredAt).Select(m => m.Id), page.Items.Select(m => m.Id));
        var created = page.Items.First(m => m.Type == "OrderCreated");
        Assert.Equal("Pending", created.Status);
        Assert.Equal(0, created.Attempts);
        Assert.Null(created.LastError);
    }

    [Fact]
    public async Task Outbox_filters_by_status_case_insensitively()
    {
        await PlaceAnOrderAsync();
        var admin = await fixture.AdminClientAsync();

        var pending = await admin.GetFromJsonAsync<OutboxPageResponse>("/admin/outbox?status=pending&pageSize=100");
        var failed = await admin.GetFromJsonAsync<OutboxPageResponse>("/admin/outbox?status=Failed&pageSize=100");

        Assert.NotEmpty(pending!.Items);
        Assert.All(pending.Items, m => Assert.Equal("Pending", m.Status));
        Assert.All(failed!.Items, m => Assert.Equal("Failed", m.Status));
    }

    [Fact]
    public async Task Outbox_pages_through_the_messages()
    {
        await PlaceAnOrderAsync();
        await PlaceAnOrderAsync();
        var admin = await fixture.AdminClientAsync();

        var first = await admin.GetFromJsonAsync<OutboxPageResponse>("/admin/outbox?page=1&pageSize=1");
        var second = await admin.GetFromJsonAsync<OutboxPageResponse>("/admin/outbox?page=2&pageSize=1");
        var beyond = await admin.GetFromJsonAsync<OutboxPageResponse>($"/admin/outbox?page={first!.TotalCount + 1}&pageSize=1");

        Assert.Single(first.Items);
        Assert.Single(second!.Items);
        Assert.NotEqual(first.Items[0].Id, second.Items[0].Id);
        Assert.Empty(beyond!.Items);
        Assert.Equal(first.TotalCount, beyond.TotalCount);
    }

    [Theory]
    [InlineData("status=Sent")]
    [InlineData("page=0")]
    [InlineData("pageSize=101")]
    public async Task Outbox_rejects_invalid_queries(string query)
    {
        var admin = await fixture.AdminClientAsync();

        var response = await admin.GetAsync($"/admin/outbox?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Health_reports_sql_and_storage_without_exposing_configuration()
    {
        var response = await fixture.Factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var health = (await response.Content.ReadFromJsonAsync<HealthResponse>())!;
        Assert.Equal("Healthy", health.Status);
        Assert.Equal(["self", "sql", "storage"], health.Checks.Select(c => c.Name).Order());
        Assert.All(health.Checks, c => Assert.Equal("Healthy", c.Status));

        var raw = await response.Content.ReadAsStringAsync();
        foreach (var secret in new[]
                 {
                     MsSqlFixture.InvoiceBucket, fixture.MinioAccessKey, fixture.MinioSecretKey,
                     new Uri(fixture.MinioEndpoint).Authority, "Server=", "Password", "CoffeeShopTests",
                 })
            Assert.DoesNotContain(secret, raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Health_is_503_when_storage_is_unreachable_but_liveness_stays_up()
    {
        await using var broken = fixture.CreateFactory(
            ("Storage:Endpoint", "http://127.0.0.1:9"), ("Storage:CreateBucketIfMissing", "false"));
        var client = broken.CreateClient();

        var health = await client.GetAsync("/health");
        var live = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, health.StatusCode);
        var report = (await health.Content.ReadFromJsonAsync<HealthResponse>())!;
        Assert.Equal("Unhealthy", report.Checks.Single(c => c.Name == "storage").Status);
        Assert.Equal("Healthy", report.Checks.Single(c => c.Name == "sql").Status);
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(["self"], (await live.Content.ReadFromJsonAsync<HealthResponse>())!.Checks.Select(c => c.Name));
    }

    [Fact]
    public async Task A_safe_correlation_id_is_echoed()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/products");
        request.Headers.Add("X-Correlation-Id", "trace-123.A_b");

        var response = await fixture.Factory.CreateClient().SendAsync(request);

        Assert.Equal("trace-123.A_b", Assert.Single(response.Headers.GetValues("X-Correlation-Id")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("has spaces")]
    [InlineData("xé")]
    public async Task A_missing_or_unsafe_correlation_id_is_replaced(string? given)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/products");
        if (given is not null)
            request.Headers.TryAddWithoutValidation("X-Correlation-Id", given);

        var response = await fixture.Factory.CreateClient().SendAsync(request);

        var id = Assert.Single(response.Headers.GetValues("X-Correlation-Id"));
        Assert.Matches("^[0-9a-f]{32}$", id);
    }

    [Fact]
    public async Task Error_responses_carry_the_correlation_id_too()
    {
        var response = await fixture.Factory.CreateClient().GetAsync("/orders");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(response.Headers.Contains("X-Correlation-Id"));
    }
}
