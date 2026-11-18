using InventoryPOS.Domain.Entities;
using InventoryPOS.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryPOS.Infrastructure.Persistence.Configurations;

public class UserPasskeyConfiguration : IEntityTypeConfiguration<UserPasskey>
{
    public void Configure(EntityTypeBuilder<UserPasskey> e)
    {
        e.ToTable("UserPasskeys");
        e.Property(x => x.UserId).HasMaxLength(450).IsRequired();
        e.Property(x => x.CredentialId).HasMaxLength(450).IsRequired();
        e.Property(x => x.PublicKey).IsRequired();
        e.Property(x => x.Name).HasMaxLength(100).IsRequired();
        e.HasIndex(x => x.CredentialId).IsUnique();
        e.HasIndex(x => x.UserId);
        e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
