using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventoryPOS.Infrastructure.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddItemCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "ProductVariants",
                type: "TEXT",
                maxLength: 12,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "ProductBatches",
                type: "TEXT",
                maxLength: 12,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_Code",
                table: "ProductVariants",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductBatches_Code",
                table: "ProductBatches",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProductVariants_Code",
                table: "ProductVariants");

            migrationBuilder.DropIndex(
                name: "IX_ProductBatches_Code",
                table: "ProductBatches");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "ProductVariants");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "ProductBatches");
        }
    }
}
