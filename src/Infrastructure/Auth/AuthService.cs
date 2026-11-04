using System.Security.Cryptography;
using System.Text;
using CoffeeShop.Application.Auth;
using CoffeeShop.Domain.Entities;
using CoffeeShop.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Infrastructure.Auth;

internal sealed class AuthService(UserManager<IdentityUser> users, CoffeeShopDbContext db, JwtTokenIssuer tokens) : IAuthService
{
    private const int MaxEmailLength = 256;
    private const int MaxNameLength = 200;

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
            await LockEmailAsync(command.Email, ct);

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
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
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

    /// <summary>Serializes registrations of one email: concurrent Identity inserts otherwise deadlock (SQL error 1205).</summary>
    private async Task LockEmailAsync(string email, CancellationToken ct)
    {
        var resource = "register:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(users.NormalizeEmail(email))));

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            DECLARE @result int;
            EXEC @result = sp_getapplock @Resource = {resource}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000;
            IF @result < 0 THROW 51001, 'Could not lock the email for registration.', 1;
            """,
            ct);
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
