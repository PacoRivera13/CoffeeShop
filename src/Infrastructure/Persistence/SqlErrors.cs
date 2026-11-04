using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Infrastructure.Persistence;

internal static class SqlErrors
{
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    public static bool IsUniqueViolation(this DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: UniqueIndexViolation or UniqueConstraintViolation };
}
