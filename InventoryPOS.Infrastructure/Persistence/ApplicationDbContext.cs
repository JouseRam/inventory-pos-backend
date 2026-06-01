using InventoryPOS.Domain;
using Microsoft.EntityFrameworkCore;

namespace InventoryPOS.Infrastructure.Persistence;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        base.OnModelCreating(model);

        model.Entity<Product>(e =>
        {
            e.Property(p => p.Price).HasPrecision(18, 2);
            e.Property(p => p.Cost).HasPrecision(18, 2);
            e.Property(p => p.Stock).HasPrecision(18, 3);
            e.Property(p => p.MinStock).HasPrecision(18, 3);
            e.HasOne(p => p.Category).WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(p => p.Supplier).WithMany(s => s.Products)
                .HasForeignKey(p => p.SupplierId).OnDelete(DeleteBehavior.SetNull);
        });

        model.Entity<Sale>(e =>
        {
            e.Property(s => s.Subtotal).HasPrecision(18, 2);
            e.Property(s => s.Discount).HasPrecision(18, 2);
            e.Property(s => s.Tax).HasPrecision(18, 2);
            e.Property(s => s.Total).HasPrecision(18, 2);
            e.Property(s => s.AmountPaid).HasPrecision(18, 2);
            e.Property(s => s.Change).HasPrecision(18, 2);
            e.HasOne(s => s.Customer).WithMany(c => c.Sales)
                .HasForeignKey(s => s.CustomerId).OnDelete(DeleteBehavior.SetNull);
        });

        model.Entity<SaleItem>(e =>
        {
            e.Property(i => i.Quantity).HasPrecision(18, 3);
            e.Property(i => i.UnitPrice).HasPrecision(18, 2);
            e.Property(i => i.Discount).HasPrecision(18, 2);
            e.Property(i => i.Subtotal).HasPrecision(18, 2);
            e.HasOne(i => i.Sale).WithMany(s => s.Items)
                .HasForeignKey(i => i.SaleId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(i => i.Product).WithMany(p => p.SaleItems)
                .HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        // Seed data
        model.Entity<Category>().HasData(
            new Category { Id = 1, Name = "General", Description = "Categoría general" },
            new Category { Id = 2, Name = "Electrónica", Description = "Productos electrónicos" },
            new Category { Id = 3, Name = "Herramientas", Description = "Herramientas y equipos" }
        );
    }
}
