using InventoryPOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryPOS.Infrastructure.Persistence.Configurations;

public class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> e)
    {
        e.ToTable("PurchaseOrders");
        e.Property(x => x.Folio).HasMaxLength(30).IsRequired();
        e.Property(x => x.Status).HasMaxLength(20).IsRequired();
        e.Property(x => x.Notes).HasMaxLength(1000);
        e.Property(x => x.Carrier).HasMaxLength(30);
        e.Property(x => x.TrackingNumber).HasMaxLength(60);
        e.Property(x => x.UserId).HasMaxLength(450);
        e.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.SetNull);
        e.HasIndex(x => x.Folio).IsUnique();
        e.HasIndex(x => x.Status);
    }
}

public class PurchaseOrderItemConfiguration : IEntityTypeConfiguration<PurchaseOrderItem>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderItem> e)
    {
        e.ToTable("PurchaseOrderItems");
        e.Property(x => x.Description).HasMaxLength(300).IsRequired();
        e.Property(x => x.Quantity).HasPrecision(18, 3);
        e.Property(x => x.UnitCost).HasPrecision(18, 2);
        e.HasOne(x => x.PurchaseOrder).WithMany(x => x.Items).HasForeignKey(x => x.PurchaseOrderId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Variant).WithMany().HasForeignKey(x => x.VariantId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PurchaseOrderEventConfiguration : IEntityTypeConfiguration<PurchaseOrderEvent>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderEvent> e)
    {
        e.ToTable("PurchaseOrderEvents");
        e.Property(x => x.Text).HasMaxLength(300).IsRequired();
        e.Property(x => x.UserId).HasMaxLength(450);
        e.HasOne(x => x.PurchaseOrder).WithMany(x => x.Events).HasForeignKey(x => x.PurchaseOrderId).OnDelete(DeleteBehavior.Cascade);
    }
}
