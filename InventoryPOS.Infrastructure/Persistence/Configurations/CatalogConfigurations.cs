using InventoryPOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryPOS.Infrastructure.Persistence.Configurations;

public class BrandConfiguration : IEntityTypeConfiguration<Brand>
{
    public void Configure(EntityTypeBuilder<Brand> e)
    {
        e.ToTable("Brands");
        e.Property(x => x.Name).HasMaxLength(100).IsRequired();
        e.HasIndex(x => x.Name).IsUnique();
    }
}

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> e)
    {
        e.ToTable("Categories");
        e.Property(x => x.Name).HasMaxLength(100).IsRequired();
        e.Property(x => x.Description).HasMaxLength(500);
        // Subcategories keep their parent; categories are deactivated, never cascaded away.
        e.HasOne(x => x.Parent).WithMany(x => x.Children).HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.ParentId, x.Name }).IsUnique();
    }
}

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> e)
    {
        e.ToTable("Suppliers");
        e.Property(x => x.Name).HasMaxLength(200).IsRequired();
        e.Property(x => x.ContactName).HasMaxLength(150);
        e.Property(x => x.Phone).HasMaxLength(30);
        e.Property(x => x.Email).HasMaxLength(150);
        e.Property(x => x.Address).HasMaxLength(300);
    }
}

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> e)
    {
        e.ToTable("Customers");
        e.Property(x => x.Name).HasMaxLength(200).IsRequired();
        e.Property(x => x.Phone).HasMaxLength(30);
        e.Property(x => x.Email).HasMaxLength(150);
        e.Property(x => x.Address).HasMaxLength(300);
        e.Property(x => x.Notes).HasMaxLength(1000);
        e.HasIndex(x => x.Phone);
    }
}
