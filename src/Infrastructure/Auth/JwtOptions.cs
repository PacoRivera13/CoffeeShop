using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace CoffeeShop.Infrastructure.Auth;

public sealed class JwtOptions
{
    private const int MinKeyLength = 32;

    private JwtOptions(string key, string issuer, string audience)
    {
        SigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        Issuer = issuer;
        Audience = audience;
    }

    public SymmetricSecurityKey SigningKey { get; }
    public string Issuer { get; }
    public string Audience { get; }

    /// <summary>Reads Jwt:Key, Jwt:Issuer and Jwt:Audience; throws when any is missing or the key is too short.</summary>
    public static JwtOptions Load(IConfiguration configuration)
    {
        var key = configuration["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(key) || key.Length < MinKeyLength)
            throw new InvalidOperationException($"Jwt:Key must be configured with at least {MinKeyLength} characters.");

        return new JwtOptions(key, Require(configuration, "Jwt:Issuer"), Require(configuration, "Jwt:Audience"));
    }

    private static string Require(IConfiguration configuration, string name)
    {
        var value = configuration[name];
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"{name} must be configured.")
            : value;
    }
}
