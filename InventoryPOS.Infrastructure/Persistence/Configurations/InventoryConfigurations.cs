using InventoryPOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryPOS.Infrastructure.Persistence.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> e)
    {
        e.ToTable("Products");
        e.Property(x => x.Name).HasMaxLength(200).IsRequired();
        e.Property(x => x.Description).HasMaxLength(1000);
        // Removing a catalog entry must not delete products; they just lose the reference.
        e.HasOne(x => x.Brand).WithMany(x => x.Products).HasForeignKey(x => x.BrandId).OnDelete(DeleteBehavior.SetNull);
        e.HasOne(x => x.Category).WithMany(x => x.Products).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.SetNull);
        e.HasOne(x => x.Supplier).WithMany(x => x.Products).HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.SetNull);
        e.HasIndex(x => x.Name);
    }
}

public class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> e)
    {
        e.ToTable("ProductVariants");
        e.Property(x => x.SKU).HasMaxLength(50).IsRequired();
        e.Property(x => x.Barcode).HasMaxLength(50);
        e.Property(x => x.ShadeCode).HasMaxLength(20);
        e.Property(x => x.ShadeName).HasMaxLength(100);
        e.Property(x => x.Unit).HasMaxLength(10).IsRequired();
        e.Property(x => x.Size).HasPrecision(10, 2);
        e.Property(x => x.Price).HasPrecision(18, 2);
        e.Property(x => x.Cost).HasPrecision(18, 2);
        e.Property(x => x.Stock).HasPrecision(18, 3);
        e.Property(x => x.MinStock).HasPrecision(18, 3);
        e.Property(x => x.RowVersion).IsRowVersion();
        // Variants hold sales history; they are deactivated, never deleted.
        e.HasOne(x => x.Product).WithMany(x => x.Variants).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => x.SKU).IsUnique();
        e.HasIndex(x => x.Barcode).IsUnique().HasFilter("[Barcode] IS NOT NULL");
        e.HasIndex(x => x.ShadeCode);
    }
}

public class ProductBatchConfiguration : IEntityTypeConfiguration<ProductBatch>
{
    public void Configure(EntityTypeBuilder<ProductBatch> e)
    {
        e.ToTable("ProductBatches");
        e.Property(x => x.LotNumber).HasMaxLength(50).IsRequired();
        e.Property(x => x.ExpirationDate).HasColumnType("date");
        e.Property(x => x.Quantity).HasPrecision(18, 3);
        e.HasOne(x => x.Variant).WithMany(x => x.Batches).HasForeignKey(x => x.VariantId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.VariantId, x.LotNumber }).IsUnique();
        e.HasIndex(x => x.ExpirationDate);
    }
}

public class InventoryMovementConfiguration : IEntityTypeConfiguration<InventoryMovement>
{
    public void Configure(EntityTypeBuilder<InventoryMovement> e)
    {
        e.ToTable("InventoryMovements");
        e.Property(x => x.Type).HasMaxLength(30).IsRequired();
        e.Property(x => x.Quantity).HasPrecision(18, 3);
        e.Property(x => x.StockAfter).HasPrecision(18, 3);
        e.Property(x => x.UnitCost).HasPrecision(18, 2);
        e.Property(x => x.Reference).HasMaxLength(50);
        e.Property(x => x.Notes).HasMaxLength(300);
        e.Property(x => x.UserId).HasMaxLength(450);
        e.HasOne(x => x.Variant).WithMany().HasForeignKey(x => x.VariantId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Batch).WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.VariantId, x.CreatedAt });
        e.HasIndex(x => x.Reference);
    }
}

public class PurchaseConfiguration : IEntityTypeConfiguration<Purchase>
{
    public void Configure(EntityTypeBuilder<Purchase> e)
    {
        e.ToTable("Purchases");
        e.Property(x => x.Folio).HasMaxLength(20).IsRequired();
        e.Property(x => x.InvoiceNumber).HasMaxLength(50);
        e.Property(x => x.Notes).HasMaxLength(500);
        e.Property(x => x.Total).HasPrecision(18, 2);
        e.Property(x => x.UserId).HasMaxLength(450);
        e.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.SetNull);
        e.HasIndex(x => x.Folio).IsUnique();
        e.HasIndex(x => x.CreatedAt);
    }
}

public class PurchaseItemConfiguration : IEntityTypeConfiguration<PurchaseItem>
{
    public void Configure(EntityTypeBuilder<PurchaseItem> e)
    {
        e.ToTable("PurchaseItems");
        e.Property(x => x.Quantity).HasPrecision(18, 3);
        e.Property(x => x.UnitCost).HasPrecision(18, 2);
        e.Property(x => x.Subtotal).HasPrecision(18, 2);
        e.Property(x => x.LotNumber).HasMaxLength(50);
        e.Property(x => x.ExpirationDate).HasColumnType("date");
        e.HasOne(x => x.Purchase).WithMany(x => x.Items).HasForeignKey(x => x.PurchaseId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Variant).WithMany().HasForeignKey(x => x.VariantId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Batch).WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
    }
}
