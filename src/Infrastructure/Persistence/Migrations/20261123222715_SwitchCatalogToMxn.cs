using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoffeeShop.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SwitchCatalogToMxn : Migration
    {
        /// <summary>Fixed conversion used once to move the seeded catalogue from EUR to MXN.</summary>
        private const int EurToMxnRate = 20;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Prices are append-only, so the switch to MXN adds rows instead of rewriting the EUR history
            // (1 EUR = 20 MXN, rounded to cents). Past orders and invoices keep their EUR snapshots.
            migrationBuilder.Sql($$"""
                DECLARE @now datetimeoffset = SYSDATETIMEOFFSET();

                -- The EUR price in force now becomes an MXN price from now on.
                INSERT INTO [EffectivePrices] ([SkuId], [Amount], [Currency], [EffectiveFrom])
                SELECT p.[SkuId], ROUND(p.[Amount] * {{EurToMxnRate}}, 2), 'MXN', @now
                FROM [EffectivePrices] p
                WHERE p.[Currency] = 'EUR'
                  AND p.[EffectiveFrom] = (
                      SELECT MAX(x.[EffectiveFrom]) FROM [EffectivePrices] x
                      WHERE x.[SkuId] = p.[SkuId] AND x.[EffectiveFrom] <= @now);

                -- Scheduled EUR changes get an MXN twin 100 ns later, so they do not switch the SKU back to EUR.
                INSERT INTO [EffectivePrices] ([SkuId], [Amount], [Currency], [EffectiveFrom])
                SELECT p.[SkuId], ROUND(p.[Amount] * {{EurToMxnRate}}, 2), 'MXN', DATEADD(NANOSECOND, 100, p.[EffectiveFrom])
                FROM [EffectivePrices] p
                WHERE p.[Currency] = 'EUR' AND p.[EffectiveFrom] > @now;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty: price rows are append-only (a trigger rejects DELETE), so rolling back the schema
            // keeps the MXN price history.
        }
    }
}
