using InventoryPOS.Domain.Entities;
using InventoryPOS.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryPOS.Infrastructure.Persistence.Configurations;

public class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> e)
    {
        e.ToTable("Sales");
        e.Property(x => x.Folio).HasMaxLength(20).IsRequired();
        e.Property(x => x.Status).HasMaxLength(20).IsRequired();
        e.Property(x => x.Notes).HasMaxLength(500);
        e.Property(x => x.CancelReason).HasMaxLength(300);
        e.Property(x => x.UserId).HasMaxLength(450);
        foreach (var money in new[] { nameof(Sale.Subtotal), nameof(Sale.Discount), nameof(Sale.Tax), nameof(Sale.Total), nameof(Sale.AmountPaid), nameof(Sale.Change) })
            e.Property(money).HasPrecision(18, 2);
        e.HasOne(x => x.Customer).WithMany(x => x.Sales).HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.SetNull);
        e.HasOne(x => x.CashSession).WithMany(x => x.Sales).HasForeignKey(x => x.CashSessionId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => x.Folio).IsUnique();
        e.HasIndex(x => new { x.CreatedAt, x.Status });
    }
}

public class SaleItemConfiguration : IEntityTypeConfiguration<SaleItem>
{
    public void Configure(EntityTypeBuilder<SaleItem> e)
    {
        e.ToTable("SaleItems");
        e.Property(x => x.Description).HasMaxLength(300).IsRequired();
        e.Property(x => x.Quantity).HasPrecision(18, 3);
        e.Property(x => x.UnitPrice).HasPrecision(18, 2);
        e.Property(x => x.UnitCost).HasPrecision(18, 2);
        e.Property(x => x.Discount).HasPrecision(18, 2);
        e.Property(x => x.Subtotal).HasPrecision(18, 2);
        e.HasOne(x => x.Sale).WithMany(x => x.Items).HasForeignKey(x => x.SaleId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Variant).WithMany().HasForeignKey(x => x.VariantId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class SalePaymentConfiguration : IEntityTypeConfiguration<SalePayment>
{
    public void Configure(EntityTypeBuilder<SalePayment> e)
    {
        e.ToTable("SalePayments");
        e.Property(x => x.Method).HasMaxLength(20).IsRequired();
        e.Property(x => x.Amount).HasPrecision(18, 2);
        e.Property(x => x.Reference).HasMaxLength(100);
        e.HasOne(x => x.Sale).WithMany(x => x.Payments).HasForeignKey(x => x.SaleId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class CashSessionConfiguration : IEntityTypeConfiguration<CashSession>
{
    public void Configure(EntityTypeBuilder<CashSession> e)
    {
        e.ToTable("CashSessions");
        e.Property(x => x.Folio).HasMaxLength(20).IsRequired();
        e.Property(x => x.UserId).HasMaxLength(450).IsRequired();
        e.Property(x => x.Status).HasMaxLength(20).IsRequired();
        e.Property(x => x.Notes).HasMaxLength(1000);
        foreach (var money in new[] { nameof(CashSession.OpeningAmount), nameof(CashSession.ExpectedCash), nameof(CashSession.CountedCash), nameof(CashSession.Difference) })
            e.Property(money).HasPrecision(18, 2);
        e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => x.Folio).IsUnique();
        e.HasIndex(x => new { x.UserId, x.Status });
    }
}

public class CashMovementConfiguration : IEntityTypeConfiguration<CashMovement>
{
    public void Configure(EntityTypeBuilder<CashMovement> e)
    {
        e.ToTable("CashMovements");
        e.Property(x => x.Type).HasMaxLength(20).IsRequired();
        e.Property(x => x.Amount).HasPrecision(18, 2);
        e.Property(x => x.Reason).HasMaxLength(300).IsRequired();
        e.Property(x => x.UserId).HasMaxLength(450);
        e.HasOne(x => x.CashSession).WithMany(x => x.Movements).HasForeignKey(x => x.CashSessionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class SettingConfiguration : IEntityTypeConfiguration<Setting>
{
    public void Configure(EntityTypeBuilder<Setting> e)
    {
        e.ToTable("Settings");
        e.Property(x => x.Key).HasMaxLength(100).IsRequired();
        e.Property(x => x.Value).HasMaxLength(2000);
        e.Property(x => x.Label).HasMaxLength(150).IsRequired();
        e.Property(x => x.Type).HasMaxLength(20).IsRequired();
        e.Property(x => x.Group).HasMaxLength(50).IsRequired();
        e.HasIndex(x => x.Key).IsUnique();
    }
}

public class UserModulePermissionConfiguration : IEntityTypeConfiguration<UserModulePermission>
{
    public void Configure(EntityTypeBuilder<UserModulePermission> e)
    {
        e.ToTable("UserModulePermissions");
        e.Property(x => x.UserId).HasMaxLength(450).IsRequired();
        e.Property(x => x.Module).HasMaxLength(50).IsRequired();
        e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        e.HasIndex(x => new { x.UserId, x.Module }).IsUnique();
    }
}

public class FolioSequenceConfiguration : IEntityTypeConfiguration<FolioSequence>
{
    public void Configure(EntityTypeBuilder<FolioSequence> e)
    {
        e.ToTable("FolioSequences");
        e.Property(x => x.Name).HasMaxLength(50).IsRequired();
        e.Property(x => x.Prefix).HasMaxLength(10).IsRequired();
        e.HasIndex(x => x.Name).IsUnique();
    }
}
