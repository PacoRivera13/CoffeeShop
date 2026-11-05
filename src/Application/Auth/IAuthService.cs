namespace CoffeeShop.Application.Auth;

public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Customer = "Customer";
}

public static class AuthPolicies
{
    public const string AdminOnly = "AdminOnly";
    public const string CustomerOnly = "CustomerOnly";
}

public sealed record RegisterCommand(string Email, string Password, string Name);

public abstract record RegisterResult
{
    public sealed record Success(int CustomerId, string Email) : RegisterResult;

    public sealed record DuplicateEmail : RegisterResult;

    public sealed record Invalid(IReadOnlyDictionary<string, string[]> Errors) : RegisterResult;
}

public sealed record AccessToken(string Value, int ExpiresInSeconds);

public interface IAuthService
{
    Task<RegisterResult> RegisterAsync(RegisterCommand command, CancellationToken ct);

    /// <summary>Returns null for an unknown email or a wrong password, without telling them apart.</summary>
    Task<AccessToken?> LoginAsync(string email, string password, CancellationToken ct);
}
