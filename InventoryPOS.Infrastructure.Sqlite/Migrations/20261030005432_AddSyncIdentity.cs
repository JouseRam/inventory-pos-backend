using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventoryPOS.Infrastructure.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddSyncIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "Uid",
                table: "UserModulePermissions",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "Uid",
                table: "Suppliers",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "Uid",
                table: "Settings",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "Uid",
                table: "Sales",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "Uid",
                table: "SalePayments",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "Uid",
                table: "SaleItems",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "Uid",
                table: "Purchases",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "Uid",
                table: "PurchaseItems",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "Uid",
                table: "ProductVariants",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "Uid",
                table: "Products",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "Uid",
                table: "ProductBatches",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "Uid",
                table: "InventoryMovements",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "Uid",
                table: "FolioSequences",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "Uid",
                table: "Customers",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "Uid",
                table: "Categories",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "Uid",
                table: "CashSessions",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "Uid",
                table: "CashMovements",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "Uid",
                table: "Brands",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateTable(
                name: "SyncDevices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    TokenHash = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    PairingCode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    PairingExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PairedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastSeenAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastPushAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Uid = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncDevices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SyncOutbox",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EntityType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    EntityUid = table.Column<Guid>(type: "TEXT", nullable: false),
                    Attempts = table.Column<int>(type: "INTEGER", nullable: false),
                    LastError = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Uid = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncOutbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SyncStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Key = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Uid = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncStates", x => x.Id);
                });

            // Existing rows get their own Uid; the default (all zeros) would break the unique indexes below.
            migrationBuilder.Sql("UPDATE \"UserModulePermissions\" SET \"Uid\" = upper(hex(randomblob(4))) || '-' || upper(hex(randomblob(2))) || '-' || upper(hex(randomblob(2))) || '-' || upper(hex(randomblob(2))) || '-' || upper(hex(randomblob(6)));");
            migrationBuilder.Sql("UPDATE \"Suppliers\" SET \"Uid\" = upper(hex(randomblob(4))) || '-' || upper(hex(randomblob(2))) || '-' || upper(hex(randomblob(2))) || '-' || upper(hex(randomblob(2))) || '-' || upper(hex(randomblob(6)));");
            migrationBuilder.Sql("UPDATE \"Settings\" SET \"Uid\" = upper(hex(randomblob(4))) || '-' || upper(hex(randomblob(2))) || '-' || upper(hex(randomblob(2))) || '-' || upper(hex(randomblob(2))) || '-' || upper(hex(randomblob(6)));");
            migrationBuilder.Sql("UPDATE \"Sales\" SET \"Uid\" = upper(hex(randomblob(4))) || '-' || upper(hex(randomblob(2))) || '-' || upper(hex(randomblob(2))) || '-' || upper(hex(randomblob(2))) || '-' || upper(hex(randomblob(6)));");
            migrationBuilder.Sql("UPDATE \"SalePayments\" SET \"Uid\" = upper(hex(randomblob(4))) || '-' || upper(hex(randomblob(2))) || '-' || upper(hex(randomblob(2))) || '-' || upper(hex(randomblob(2))) || '-' || upper(hex(randomblob(6)));");
            migrationBuilder.Sql("UPDATE \"SaleItems\" SET \"Uid\" = upper(hex(randomblob(4))) || '-' || upper(hex(randomblob(2))) || '-' || upper(hex(randomblob(2))) || '-' || upper(hex(randomblob(2))) || '-' || upper(hex(randomblob(6)));");
            migrationBuilder.Sql("UPDATE \"Purchases\" SET \"Uid\" = upper(hex(randomblob(4))) || '-' || upper(hex(randomblob(2))) || '-' || upper(hex(randomblob(2))) || '-' || upper(hex(randomblob(2))) || '-' || upper(hex(randomblob(6)));");
            migrationBuilder.Sql("UPDATE \"PurchaseItems\" SET \"Uid\" = upper(hex(randomblob(4))) || '-' || upper(hex(randomblob(2))) || '-' || upper(hex(randomblob(2))) || '-' || upper(hex(randomblob(2))) || '-' || upper(hex(randomblob(6)));");
            migrationBuilder.Sql("UPDATE \"ProductVariants\" SET \"Uid\" = upper(hex(randomblob(4))) || '-' || upper(hex(randomblob(2))) || '-' || upper(hex(randomblob(2))) || '-' || upper(hex(randomblob(2))) || '-' || upper(hex(randomblob(6)));");
            migrationBuilder.Sql("UPDATE \"Products\" SET \"Uid\" = upper(hex(randomblob(4))) || '-' || upper(hex(randomblob(2))) || '-' || upper(hex(randomblob(2))) || '-' || upper(hex(randomblob(2))) || '-' || upper(hex(randomblob(6)));");
            migrationBuilder.Sql("UPDATE \"ProductBatches\" SET \"Uid\" = upper(hex(randomblob(4))) || '-' || upper(hex(randomblob(2))) || '-' || upper(hex(randomblob(2))) || '-' || upper(hex(randomblob(2))) || '-' || upper(hex(randomblob(6)));");
            migrationBuilder.Sql("UPDATE \"InventoryMovements\" SET \"Uid\" = upper(hex(randomblob(4))) || '-' || upper(hex(randomblob(2))) || '-' || upper(hex(randomblob(2))) || '-' || upper(hex(randomblob(2))) || '-' || upper(hex(randomblob(6)));");
            migrationBuilder.Sql("UPDATE \"FolioSequences\" SET \"Uid\" = upper(hex(randomblob(4))) || '-' || upper(hex(randomblob(2))) || '-' || upper(hex(randomblob(2))) || '-' || upper(hex(randomblob(2))) || '-' || upper(hex(randomblob(6)));");
            migrationBuilder.Sql("UPDATE \"Customers\" SET \"Uid\" = upper(hex(randomblob(4))) || '-' || upper(hex(randomblob(2))) || '-' || upper(hex(randomblob(2))) || '-' || upper(hex(randomblob(2))) || '-' || upper(hex(randomblob(6)));");
            migrationBuilder.Sql("UPDATE \"Categories\" SET \"Uid\" = upper(hex(randomblob(4))) || '-' || upper(hex(randomblob(2))) || '-' || upper(hex(randomblob(2))) || '-' || upper(hex(randomblob(2))) || '-' || upper(hex(randomblob(6)));");
            migrationBuilder.Sql("UPDATE \"CashSessions\" SET \"Uid\" = upper(hex(randomblob(4))) || '-' || upper(hex(randomblob(2))) || '-' || upper(hex(randomblob(2))) || '-' || upper(hex(randomblob(2))) || '-' || upper(hex(randomblob(6)));");
            migrationBuilder.Sql("UPDATE \"CashMovements\" SET \"Uid\" = upper(hex(randomblob(4))) || '-' || upper(hex(randomblob(2))) || '-' || upper(hex(randomblob(2))) || '-' || upper(hex(randomblob(2))) || '-' || upper(hex(randomblob(6)));");
            migrationBuilder.Sql("UPDATE \"Brands\" SET \"Uid\" = upper(hex(randomblob(4))) || '-' || upper(hex(randomblob(2))) || '-' || upper(hex(randomblob(2))) || '-' || upper(hex(randomblob(2))) || '-' || upper(hex(randomblob(6)));");
            migrationBuilder.Sql("UPDATE \"AspNetUsers\" SET \"UpdatedAt\" = \"CreatedAt\";");

            migrationBuilder.CreateIndex(
                name: "IX_UserModulePermissions_Uid",
                table: "UserModulePermissions",
                column: "Uid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_Uid",
                table: "Suppliers",
                column: "Uid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Settings_Uid",
                table: "Settings",
                column: "Uid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sales_Uid",
                table: "Sales",
                column: "Uid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalePayments_Uid",
                table: "SalePayments",
                column: "Uid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SaleItems_Uid",
                table: "SaleItems",
                column: "Uid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Purchases_Uid",
                table: "Purchases",
                column: "Uid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseItems_Uid",
                table: "PurchaseItems",
                column: "Uid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_Uid",
                table: "ProductVariants",
                column: "Uid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_Uid",
                table: "Products",
                column: "Uid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductBatches_Uid",
                table: "ProductBatches",
                column: "Uid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_Uid",
                table: "InventoryMovements",
                column: "Uid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FolioSequences_Uid",
                table: "FolioSequences",
                column: "Uid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Uid",
                table: "Customers",
                column: "Uid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Uid",
                table: "Categories",
                column: "Uid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashSessions_Uid",
                table: "CashSessions",
                column: "Uid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_Uid",
                table: "CashMovements",
                column: "Uid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Brands_Uid",
                table: "Brands",
                column: "Uid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SyncDevices_TokenHash",
                table: "SyncDevices",
                column: "TokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_SyncDevices_Uid",
                table: "SyncDevices",
                column: "Uid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SyncOutbox_EntityType_EntityUid",
                table: "SyncOutbox",
                columns: new[] { "EntityType", "EntityUid" });

            migrationBuilder.CreateIndex(
                name: "IX_SyncOutbox_Uid",
                table: "SyncOutbox",
                column: "Uid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SyncStates_Key",
                table: "SyncStates",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SyncStates_Uid",
                table: "SyncStates",
                column: "Uid",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SyncDevices");

            migrationBuilder.DropTable(
                name: "SyncOutbox");

            migrationBuilder.DropTable(
                name: "SyncStates");

            migrationBuilder.DropIndex(
                name: "IX_UserModulePermissions_Uid",
                table: "UserModulePermissions");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_Uid",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Settings_Uid",
                table: "Settings");

            migrationBuilder.DropIndex(
                name: "IX_Sales_Uid",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_SalePayments_Uid",
                table: "SalePayments");

            migrationBuilder.DropIndex(
                name: "IX_SaleItems_Uid",
                table: "SaleItems");

            migrationBuilder.DropIndex(
                name: "IX_Purchases_Uid",
                table: "Purchases");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseItems_Uid",
                table: "PurchaseItems");

            migrationBuilder.DropIndex(
                name: "IX_ProductVariants_Uid",
                table: "ProductVariants");

            migrationBuilder.DropIndex(
                name: "IX_Products_Uid",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_ProductBatches_Uid",
                table: "ProductBatches");

            migrationBuilder.DropIndex(
                name: "IX_InventoryMovements_Uid",
                table: "InventoryMovements");

            migrationBuilder.DropIndex(
                name: "IX_FolioSequences_Uid",
                table: "FolioSequences");

            migrationBuilder.DropIndex(
                name: "IX_Customers_Uid",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Categories_Uid",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_CashSessions_Uid",
                table: "CashSessions");

            migrationBuilder.DropIndex(
                name: "IX_CashMovements_Uid",
                table: "CashMovements");

            migrationBuilder.DropIndex(
                name: "IX_Brands_Uid",
                table: "Brands");

            migrationBuilder.DropColumn(
                name: "Uid",
                table: "UserModulePermissions");

            migrationBuilder.DropColumn(
                name: "Uid",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "Uid",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "Uid",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "Uid",
                table: "SalePayments");

            migrationBuilder.DropColumn(
                name: "Uid",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "Uid",
                table: "Purchases");

            migrationBuilder.DropColumn(
                name: "Uid",
                table: "PurchaseItems");

            migrationBuilder.DropColumn(
                name: "Uid",
                table: "ProductVariants");

            migrationBuilder.DropColumn(
                name: "Uid",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Uid",
                table: "ProductBatches");

            migrationBuilder.DropColumn(
                name: "Uid",
                table: "InventoryMovements");

            migrationBuilder.DropColumn(
                name: "Uid",
                table: "FolioSequences");

            migrationBuilder.DropColumn(
                name: "Uid",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Uid",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "Uid",
                table: "CashSessions");

            migrationBuilder.DropColumn(
                name: "Uid",
                table: "CashMovements");

            migrationBuilder.DropColumn(
                name: "Uid",
                table: "Brands");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "AspNetUsers");
        }
    }
}
