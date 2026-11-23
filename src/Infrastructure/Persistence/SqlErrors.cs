using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Infrastructure.Persistence;

internal static class SqlErrors
{
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    private const int DeadlockVictim = 1205;

    public static bool IsUniqueViolation(this DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: UniqueIndexViolation or UniqueConstraintViolation };

    /// <summary>True when SQL Server chose this transaction as a deadlock victim; it was rolled back and can be rerun.</summary>
    public static bool IsDeadlock(this Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException { Number: DeadlockVictim })
                return true;
        }

        return false;
    }
}
