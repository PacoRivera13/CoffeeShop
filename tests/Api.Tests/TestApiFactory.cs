using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CoffeeShop.Api.Tests;

/// <summary>Hosts the API with a JWT key generated for the test run, since the app refuses to start without one.</summary>
public class TestApiFactory : WebApplicationFactory<Program>
{
    public static readonly string JwtKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder
            .UseSetting("Jwt:Key", JwtKey)
            .UseSetting("Jwt:Issuer", "coffeeshop-tests")
            .UseSetting("Jwt:Audience", "coffeeshop-tests");
}
