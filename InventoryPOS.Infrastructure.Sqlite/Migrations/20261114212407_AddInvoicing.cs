using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventoryPOS.Infrastructure.Sqlite.Migrations
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
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SaleId = table.Column<int>(type: "INTEGER", nullable: false),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ProviderId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Uuid = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ReceiverRfc = table.Column<string>(type: "TEXT", maxLength: 13, nullable: false),
                    ReceiverName = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    CfdiUse = table.Column<string>(type: "TEXT", maxLength: 4, nullable: false),
                    PaymentForm = table.Column<string>(type: "TEXT", maxLength: 2, nullable: false),
                    Total = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Sandbox = table.Column<bool>(type: "INTEGER", nullable: false),
                    Xml = table.Column<string>(type: "TEXT", nullable: true),
                    Pdf = table.Column<byte[]>(type: "BLOB", nullable: true),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    CancelMotive = table.Column<string>(type: "TEXT", maxLength: 2, nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Uid = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
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
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Sandbox = table.Column<bool>(type: "INTEGER", nullable: false),
                    UserName = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    ProtectedPassword = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    DefaultProductCode = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    Serie = table.Column<string>(type: "TEXT", maxLength: 25, nullable: true),
                    CsdRfc = table.Column<string>(type: "TEXT", maxLength: 13, nullable: true),
                    CsdCertificateNumber = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    CsdValidUntil = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CsdUploadedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Uid = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
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
