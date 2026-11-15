using InventoryPOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryPOS.Infrastructure.Persistence.Configurations;

public class PromotionConfiguration : IEntityTypeConfiguration<Promotion>
{
    public void Configure(EntityTypeBuilder<Promotion> e)
    {
        e.ToTable("Promotions");
        e.Property(x => x.Title).HasMaxLength(150).IsRequired();
        e.Property(x => x.Message).HasMaxLength(1500).IsRequired();
        e.Property(x => x.ImageType).HasMaxLength(30);
        e.Property(x => x.UserId).HasMaxLength(450);
    }
}

public class PromotionRecipientConfiguration : IEntityTypeConfiguration<PromotionRecipient>
{
    public void Configure(EntityTypeBuilder<PromotionRecipient> e)
    {
        e.ToTable("PromotionRecipients");
        e.Property(x => x.Name).HasMaxLength(200).IsRequired();
        e.Property(x => x.Phone).HasMaxLength(15).IsRequired();
        e.Property(x => x.Status).HasMaxLength(20).IsRequired();
        e.HasOne(x => x.Promotion).WithMany(x => x.Recipients).HasForeignKey(x => x.PromotionId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.PromotionId, x.CustomerId }).IsUnique();
    }
}
