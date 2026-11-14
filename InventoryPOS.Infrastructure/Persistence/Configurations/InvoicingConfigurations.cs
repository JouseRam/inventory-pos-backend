using InventoryPOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryPOS.Infrastructure.Persistence.Configurations;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> e)
    {
        e.ToTable("Invoices");
        e.Property(x => x.Provider).HasMaxLength(50).IsRequired();
        e.Property(x => x.ProviderId).HasMaxLength(100).IsRequired();
        e.Property(x => x.Uuid).HasMaxLength(36).IsRequired();
        e.Property(x => x.Status).HasMaxLength(20).IsRequired();
        e.Property(x => x.ReceiverRfc).HasMaxLength(13).IsRequired();
        e.Property(x => x.ReceiverName).HasMaxLength(300).IsRequired();
        e.Property(x => x.CfdiUse).HasMaxLength(4).IsRequired();
        e.Property(x => x.PaymentForm).HasMaxLength(2).IsRequired();
        e.Property(x => x.Total).HasPrecision(18, 2);
        e.Property(x => x.UserId).HasMaxLength(450);
        e.Property(x => x.CancelMotive).HasMaxLength(2);
        e.HasOne(x => x.Sale).WithMany().HasForeignKey(x => x.SaleId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => x.SaleId);
        e.HasIndex(x => x.Uuid).IsUnique();
    }
}

public class InvoicingAccountConfiguration : IEntityTypeConfiguration<InvoicingAccount>
{
    public void Configure(EntityTypeBuilder<InvoicingAccount> e)
    {
        e.ToTable("InvoicingAccounts");
        e.Property(x => x.Provider).HasMaxLength(50).IsRequired();
        e.Property(x => x.UserName).HasMaxLength(150).IsRequired();
        e.Property(x => x.ProtectedPassword).HasMaxLength(500);
        e.Property(x => x.DefaultProductCode).HasMaxLength(8).IsRequired();
        e.Property(x => x.Serie).HasMaxLength(25);
        e.Property(x => x.CsdRfc).HasMaxLength(13);
        e.Property(x => x.CsdCertificateNumber).HasMaxLength(40);
    }
}
