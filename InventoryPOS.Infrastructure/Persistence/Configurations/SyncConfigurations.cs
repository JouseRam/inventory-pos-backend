using InventoryPOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryPOS.Infrastructure.Persistence.Configurations;

public class SyncOutboxConfiguration : IEntityTypeConfiguration<SyncOutbox>
{
    public void Configure(EntityTypeBuilder<SyncOutbox> e)
    {
        e.ToTable("SyncOutbox");
        e.Property(x => x.EntityType).HasMaxLength(50).IsRequired();
        e.Property(x => x.LastError).HasMaxLength(1000);
        // Not unique: every change adds a row and the sender collapses them per record.
        e.HasIndex(x => new { x.EntityType, x.EntityUid });
    }
}

public class SyncStateConfiguration : IEntityTypeConfiguration<SyncState>
{
    public void Configure(EntityTypeBuilder<SyncState> e)
    {
        e.ToTable("SyncStates");
        e.Property(x => x.Key).HasMaxLength(50).IsRequired();
        e.Property(x => x.Value).HasMaxLength(2000);
        e.HasIndex(x => x.Key).IsUnique();
    }
}

public class SyncDeviceConfiguration : IEntityTypeConfiguration<SyncDevice>
{
    public void Configure(EntityTypeBuilder<SyncDevice> e)
    {
        e.ToTable("SyncDevices");
        e.Property(x => x.Name).HasMaxLength(100).IsRequired();
        e.Property(x => x.TokenHash).HasMaxLength(100);
        e.Property(x => x.PairingCode).HasMaxLength(20);
        e.HasIndex(x => x.TokenHash);
    }
}
