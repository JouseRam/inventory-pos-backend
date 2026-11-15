using InventoryPOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryPOS.Infrastructure.Persistence.Configurations;

public class StockAlertConfiguration : IEntityTypeConfiguration<StockAlert>
{
    public void Configure(EntityTypeBuilder<StockAlert> e)
    {
        e.ToTable("StockAlerts");
        e.Property(x => x.Stock).HasPrecision(18, 3);
        e.Property(x => x.MinStock).HasPrecision(18, 3);
        e.HasOne(x => x.Variant).WithMany().HasForeignKey(x => x.VariantId).OnDelete(DeleteBehavior.Cascade);
        e.HasIndex(x => new { x.VariantId, x.ResolvedAt });
    }
}
