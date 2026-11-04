using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoffeeShop.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RequirePositivePriceAmount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_EffectivePrices_Amount",
                table: "EffectivePrices");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EffectivePrices_Amount",
                table: "EffectivePrices",
                sql: "[Amount] > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_EffectivePrices_Amount",
                table: "EffectivePrices");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EffectivePrices_Amount",
                table: "EffectivePrices",
                sql: "[Amount] >= 0");
        }
    }
}
