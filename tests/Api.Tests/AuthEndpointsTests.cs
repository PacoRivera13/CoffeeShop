using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using CoffeeShop.Application.Auth;
using CoffeeShop.Infrastructure.Auth;
using CoffeeShop.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;

namespace CoffeeShop.Api.Tests;

[Collection(MsSqlCollection.Name)]
public class AuthEndpointsTests(MsSqlFixture fixture)
{
    private const string ValidPassword = "Str0ng!Passw0rd";

    private readonly HttpClient _client = fixture.Factory.CreateClient();

    private static string NewEmail() => $"{Guid.NewGuid():N}@example.test";

    private Task<HttpResponseMessage> RegisterAsync(string email, string password = ValidPassword) =>
        _client.PostAsJsonAsync("/auth/register", new RegisterRequest(email, password, "Test Customer"));

    private Task<HttpResponseMessage> LoginAsync(string email, string password = ValidPassword) =>
        _client.PostAsJsonAsync("/auth/login", new LoginRequest(email, password));

    private async Task<string> RegisterAndLoginAsync(string email)
    {
        Assert.Equal(HttpStatusCode.Created, (await RegisterAsync(email)).StatusCode);
        var response = await LoginAsync(email);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken;
    }

    private Task<HttpResponseMessage> GetMeAsync(string? token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/auth/me");
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    [Fact]
    public async Task Registered_user_can_log_in_and_call_a_protected_endpoint()
    {
        var email = NewEmail();

        var token = await RegisterAndLoginAsync(email);
        var response = await GetMeAsync(token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await response.Content.ReadFromJsonAsync<CurrentUserResponse>();
        Assert.Equal(email, me!.Email);
        Assert.Equal([AppRoles.Customer], me.Roles);
    }

    [Fact]
    public async Task Register_creates_a_customer_linked_to_the_user()
    {
        var email = NewEmail();

        var response = await RegisterAsync(email);

        var body = await response.Content.ReadFromJsonAsync<RegisterResponse>();
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        var customer = await db.Customers.SingleAsync(c => c.Id == body!.CustomerId);
        var user = await db.Users.SingleAsync(u => u.Email == email);
        Assert.Equal(user.Id, customer.IdentityUserId);
        Assert.Equal("Test Customer", customer.Name);
    }

    [Fact]
    public async Task Token_has_the_required_claims_and_lasts_sixty_minutes()
    {
        var email = NewEmail();
        await RegisterAsync(email);
        var login = (await (await LoginAsync(email)).Content.ReadFromJsonAsync<LoginResponse>())!;

        var token = new JsonWebToken(login.AccessToken);

        Assert.False(string.IsNullOrEmpty(token.Subject));
        Assert.Equal(email, token.GetClaim(JwtRegisteredClaimNames.Email).Value);
        Assert.Equal(AppRoles.Customer, token.GetClaim(AuthClaims.Role).Value);
        Assert.Equal(TimeSpan.FromMinutes(60), token.ValidTo - token.ValidFrom);
        Assert.Equal(3600, login.ExpiresIn);
        Assert.Equal("Bearer", login.TokenType);
    }

    [Fact]
    public async Task Wrong_password_is_unauthorized()
    {
        var email = NewEmail();
        await RegisterAsync(email);

        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(email, "Wr0ng!Passw0rd")).StatusCode);
    }

    [Fact]
    public async Task Unknown_email_is_unauthorized()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(NewEmail())).StatusCode);
    }

    [Fact]
    public async Task Duplicate_email_is_a_conflict()
    {
        var email = NewEmail();
        await RegisterAsync(email);

        Assert.Equal(HttpStatusCode.Conflict, (await RegisterAsync(email)).StatusCode);
    }

    [Fact]
    public async Task Duplicate_email_ignores_case()
    {
        var email = NewEmail();
        await RegisterAsync(email);

        Assert.Equal(HttpStatusCode.Conflict, (await RegisterAsync(email.ToUpperInvariant())).StatusCode);
    }

    [Fact]
    public async Task Simultaneous_registrations_of_an_email_create_one_account()
    {
        var email = NewEmail();

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => RegisterAsync(email)));

        var statuses = responses.Select(r => r.StatusCode).ToList();
        Assert.Single(statuses, HttpStatusCode.Created);
        Assert.Equal(4, statuses.Count(s => s == HttpStatusCode.Conflict));
    }

    [Theory]
    [InlineData("short1!")]
    [InlineData("alllowercase1!")]
    [InlineData("NoDigitsHere!")]
    public async Task Weak_password_is_bad_request_and_creates_nothing(string password)
    {
        var email = NewEmail();

        Assert.Equal(HttpStatusCode.BadRequest, (await RegisterAsync(email, password)).StatusCode);

        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        Assert.False(await db.Users.AnyAsync(u => u.Email == email));
    }

    [Theory]
    [InlineData("not-an-email", ValidPassword, "Name")]
    [InlineData(null, ValidPassword, "Name")]
    [InlineData("a@example.test", null, "Name")]
    [InlineData("a@example.test", ValidPassword, null)]
    [InlineData("a@example.test", ValidPassword, "   ")]
    public async Task Invalid_registration_is_bad_request(string? email, string? password, string? name)
    {
        var response = await _client.PostAsJsonAsync("/auth/register", new RegisterRequest(email, password, name));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_without_credentials_is_bad_request()
    {
        var response = await _client.PostAsJsonAsync("/auth/login", new LoginRequest(null, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Protected_endpoint_rejects_a_missing_token()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetMeAsync(null)).StatusCode);
    }

    [Fact]
    public async Task Protected_endpoint_rejects_a_tampered_token()
    {
        var token = await RegisterAndLoginAsync(NewEmail());
        var signatureStart = token.LastIndexOf('.') + 1;
        var tampered = token[..signatureStart] + new string(token[signatureStart..].Reverse().ToArray());

        Assert.Equal(HttpStatusCode.Unauthorized, (await GetMeAsync(tampered)).StatusCode);
    }

    [Fact]
    public async Task Catalog_stays_anonymous()
    {
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/products")).StatusCode);
    }

    [Fact]
    public async Task Admin_is_seeded_when_both_settings_are_present()
    {
        var email = NewEmail();
        await using var factory = AdminFactory(email, ValidPassword);

        var token = await LoginWith(factory, email, ValidPassword);

        Assert.Equal(AppRoles.Admin, new JsonWebToken(token).GetClaim(AuthClaims.Role).Value);
    }

    [Fact]
    public async Task Admin_seed_is_repeatable_and_keeps_the_existing_password()
    {
        var email = NewEmail();
        await using (var first = AdminFactory(email, ValidPassword))
            await LoginWith(first, email, ValidPassword);

        await using var second = AdminFactory(email, "An0ther!Passw0rd");

        await LoginWith(second, email, ValidPassword);
    }

    [Theory]
    [InlineData("Admin:Email")]
    [InlineData("Admin:Password")]
    public async Task Admin_is_not_seeded_with_only_one_setting(string setting)
    {
        var email = NewEmail();
        var value = setting == "Admin:Email" ? email : ValidPassword;
        await using var factory = fixture.CreateFactory((setting, value));
        factory.CreateClient();

        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        Assert.Null(await users.FindByEmailAsync(email));
    }

    [Fact]
    public void Weak_admin_password_fails_at_startup()
    {
        using var factory = AdminFactory(NewEmail(), "weak");

        Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
    }

    [Fact]
    public async Task AdminOnly_policy_allows_admins_and_rejects_customers()
    {
        var adminEmail = NewEmail();
        await using var factory = AdminFactory(adminEmail, ValidPassword);
        var adminToken = await LoginWith(factory, adminEmail, ValidPassword);
        var customerToken = await RegisterAndLoginAsync(NewEmail());
        var authorization = factory.Services.GetRequiredService<IAuthorizationService>();

        Assert.True((await authorization.AuthorizeAsync(Principal(adminToken), AuthPolicies.AdminOnly)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(Principal(customerToken), AuthPolicies.AdminOnly)).Succeeded);
    }

    private WebApplicationFactory<Program> AdminFactory(string email, string password) =>
        fixture.CreateFactory(("Admin:Email", email), ("Admin:Password", password));

    private static async Task<string> LoginWith(WebApplicationFactory<Program> factory, string email, string password)
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken;
    }

    private static ClaimsPrincipal Principal(string token) =>
        new(new ClaimsIdentity(new JsonWebToken(token).Claims, "Test", JwtRegisteredClaimNames.Sub, AuthClaims.Role));
}
