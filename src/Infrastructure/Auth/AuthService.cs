using CoffeeShop.Application.Auth;
using CoffeeShop.Domain.Entities;
using CoffeeShop.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Infrastructure.Auth;

internal sealed class AuthService(UserManager<IdentityUser> users, CoffeeShopDbContext db, JwtTokenIssuer tokens) : IAuthService
{
    private const int MaxEmailLength = 256;
    private const int MaxNameLength = 200;
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    public async Task<RegisterResult> RegisterAsync(RegisterCommand command, CancellationToken ct)
    {
        var name = command.Name.Trim();
        if (name.Length is 0 or > MaxNameLength)
            return Invalid("name", $"Name is required and must have at most {MaxNameLength} characters.");
        if (command.Email.Length > MaxEmailLength)
            return Invalid("email", $"Email must have at most {MaxEmailLength} characters.");

        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            var user = new IdentityUser { UserName = command.Email, Email = command.Email };
            var created = await users.CreateAsync(user, command.Password);
            if (!created.Succeeded)
                return ToFailure(created);

            var assigned = await users.AddToRoleAsync(user, AppRoles.Customer);
            if (!assigned.Succeeded)
                throw new InvalidOperationException($"Could not assign role: {Codes(assigned)}.");

            var customer = new Customer(0, name, command.Email, user.Id);
            db.Customers.Add(customer);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return new RegisterResult.Success(customer.Id, command.Email);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: UniqueIndexViolation or UniqueConstraintViolation })
        {
            return new RegisterResult.DuplicateEmail();
        }
    }

    public async Task<AccessToken?> LoginAsync(string email, string password, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(email);
        if (user is null || !await users.CheckPasswordAsync(user, password))
            return null;

        return tokens.Issue(user, await users.GetRolesAsync(user));
    }

    private static RegisterResult ToFailure(IdentityResult result)
    {
        if (result.Errors.Any(e => e.Code is nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName)))
            return new RegisterResult.DuplicateEmail();

        var errors = result.Errors
            .GroupBy(e => e.Code.StartsWith("Password", StringComparison.Ordinal) ? "password" : "email")
            .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());

        return new RegisterResult.Invalid(errors);
    }

    private static RegisterResult Invalid(string field, string message) =>
        new RegisterResult.Invalid(new Dictionary<string, string[]> { [field] = [message] });

    private static string Codes(IdentityResult result) => string.Join(", ", result.Errors.Select(e => e.Code));
}
