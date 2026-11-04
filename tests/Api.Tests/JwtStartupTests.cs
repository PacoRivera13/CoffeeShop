using Microsoft.AspNetCore.Hosting;

namespace CoffeeShop.Api.Tests;

public class JwtStartupTests
{
    [Theory]
    [InlineData("Jwt:Key", "")]
    [InlineData("Jwt:Key", "0123456789012345678901234567890")]
    [InlineData("Jwt:Issuer", "")]
    [InlineData("Jwt:Audience", "")]
    public void App_fails_at_startup_with_invalid_jwt_settings(string setting, string value)
    {
        using var factory = new TestApiFactory().WithWebHostBuilder(host => host.UseSetting(setting, value));

        var ex = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains(setting, ex.Message);
    }

    [Fact]
    public void App_starts_with_a_key_of_exactly_32_characters()
    {
        using var factory = new TestApiFactory().WithWebHostBuilder(host =>
            host.UseSetting("Jwt:Key", "01234567890123456789012345678901"));

        factory.CreateClient();
    }
}
