using System.Security.Claims;
using CoffeeShop.Application.Auth;
using CoffeeShop.Infrastructure.Auth;
using Microsoft.IdentityModel.JsonWebTokens;

namespace CoffeeShop.Api;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/auth").WithTags("Auth");

        auth.MapPost("/register", Register)
            .WithName("Register")
            .WithSummary("Creates a user with the Customer role and its customer record.")
            .Produces<RegisterResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        auth.MapPost("/login", Login)
            .WithName("Login")
            .WithSummary("Returns a JWT access token valid for 60 minutes.")
            .Produces<LoginResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        auth.MapGet("/me", GetCurrentUser)
            .RequireAuthorization()
            .WithName("GetCurrentUser")
            .WithSummary("Returns the identity carried by the access token.")
            .Produces<CurrentUserResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }

    private static async Task<IResult> Register(RegisterRequest request, IAuthService auth, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password) || request.Name is null)
            return MissingFields("email, password and name are required.");

        var result = await auth.RegisterAsync(new RegisterCommand(request.Email, request.Password, request.Name), ct);

        return result switch
        {
            RegisterResult.Success success => Results.Created((string?)null, new RegisterResponse(success.CustomerId, success.Email)),
            RegisterResult.DuplicateEmail => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Email already registered"),
            RegisterResult.Invalid invalid => Results.ValidationProblem(invalid.Errors.ToDictionary()),
            _ => throw new InvalidOperationException($"Unexpected result {result.GetType().Name}."),
        };
    }

    private static async Task<IResult> Login(LoginRequest request, IAuthService auth, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password))
            return MissingFields("email and password are required.");

        var token = await auth.LoginAsync(request.Email, request.Password, ct);

        return token is null
            ? Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid credentials")
            : Results.Ok(new LoginResponse(token.Value, "Bearer", token.ExpiresInSeconds));
    }

    private static IResult GetCurrentUser(ClaimsPrincipal user) =>
        Results.Ok(new CurrentUserResponse(
            user.FindFirstValue(JwtRegisteredClaimNames.Sub)!,
            user.FindFirstValue(JwtRegisteredClaimNames.Email)!,
            user.FindAll(AuthClaims.Role).Select(c => c.Value).ToList()));

    private static IResult MissingFields(string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { ["body"] = [message] });
}

public sealed record RegisterRequest(string? Email, string? Password, string? Name);

public sealed record RegisterResponse(int CustomerId, string Email);

public sealed record LoginRequest(string? Email, string? Password);

public sealed record LoginResponse(string AccessToken, string TokenType, int ExpiresIn);

public sealed record CurrentUserResponse(string Sub, string Email, IReadOnlyList<string> Roles);
