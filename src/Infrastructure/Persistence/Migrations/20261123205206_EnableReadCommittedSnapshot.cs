using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoffeeShop.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnableReadCommittedSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Readers see the last committed version instead of waiting on writers' locks. Without it, concurrent
            // sign-ups deadlock: Identity's lookups scan the small user tables and block on each other's new rows.
            // Writes still lock, so conditional updates, unique indexes and row versions keep protecting invariants.
            // ALTER DATABASE cannot run inside a transaction; ROLLBACK IMMEDIATE ends other sessions so it can apply.
            migrationBuilder.Sql(
                "ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;",
                suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT OFF WITH ROLLBACK IMMEDIATE;",
                suppressTransaction: true);
        }
    }
}
