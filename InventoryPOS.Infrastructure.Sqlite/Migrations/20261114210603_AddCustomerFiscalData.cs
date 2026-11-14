using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventoryPOS.Infrastructure.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerFiscalData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CfdiUse",
                table: "Customers",
                type: "TEXT",
                maxLength: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalName",
                table: "Customers",
                type: "TEXT",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxId",
                table: "Customers",
                type: "TEXT",
                maxLength: 13,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxRegime",
                table: "Customers",
                type: "TEXT",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxZipCode",
                table: "Customers",
                type: "TEXT",
                maxLength: 5,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_TaxId",
                table: "Customers",
                column: "TaxId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Customers_TaxId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "CfdiUse",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "LegalName",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "TaxId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "TaxRegime",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "TaxZipCode",
                table: "Customers");
        }
    }
}
