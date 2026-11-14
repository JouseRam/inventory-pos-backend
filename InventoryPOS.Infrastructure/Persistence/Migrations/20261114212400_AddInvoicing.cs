using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventoryPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoicing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Invoices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SaleId = table.Column<int>(type: "int", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProviderId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Uuid = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReceiverRfc = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: false),
                    ReceiverName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    CfdiUse = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    PaymentForm = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Sandbox = table.Column<bool>(type: "bit", nullable: false),
                    Xml = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Pdf = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CancelMotive = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Uid = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invoices_Sales_SaleId",
                        column: x => x.SaleId,
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InvoicingAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Provider = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Sandbox = table.Column<bool>(type: "bit", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ProtectedPassword = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DefaultProductCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    Serie = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: true),
                    CsdRfc = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: true),
                    CsdCertificateNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    CsdValidUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CsdUploadedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Uid = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoicingAccounts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_SaleId",
                table: "Invoices",
                column: "SaleId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Uid",
                table: "Invoices",
                column: "Uid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Uuid",
                table: "Invoices",
                column: "Uuid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoicingAccounts_Uid",
                table: "InvoicingAccounts",
                column: "Uid",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Invoices");

            migrationBuilder.DropTable(
                name: "InvoicingAccounts");
        }
    }
}
