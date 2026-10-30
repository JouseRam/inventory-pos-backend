using InventoryPOS.Application.Common;
using InventoryPOS.Application.Features.Sync;
using InventoryPOS.Domain.Common;
using InventoryPOS.Domain.Entities;
using InventoryPOS.Domain.Interfaces;

namespace InventoryPOS.Application.Features.Catalog;

public class BrandService(IUnitOfWork uow, PosGuard guard)
{
    private IRepository<Brand> Repo => uow.Repository<Brand>();

    public async Task<IReadOnlyList<BrandDto>> ListAsync(bool includeInactive, CancellationToken ct = default)
    {
        var brands = await Repo.ListAsync(includeInactive ? null : b => b.IsActive, ct);
        return brands.OrderBy(b => b.Name).Select(b => b.ToDto()).ToList();
    }

    public async Task<BrandDto> GetAsync(int id, CancellationToken ct = default) =>
        (await Repo.GetByIdAsync(id, ct) ?? throw NotFoundException.For("Marca", id)).ToDto();

    public async Task<BrandDto> CreateAsync(BrandSaveDto dto, CancellationToken ct = default)
    {
        await guard.EnsureCanAdministerAsync(ct);
        await EnsureUniqueAsync(dto.Name, null, ct);
        var brand = new Brand { Name = dto.Name.Trim(), IsActive = dto.IsActive };
        Repo.Add(brand);
        await uow.SaveChangesAsync(ct);
        return brand.ToDto();
    }

    public async Task<BrandDto> UpdateAsync(int id, BrandSaveDto dto, CancellationToken ct = default)
    {
        await guard.EnsureCanAdministerAsync(ct);
        var brand = await Repo.GetByIdAsync(id, ct) ?? throw NotFoundException.For("Marca", id);
        await EnsureUniqueAsync(dto.Name, id, ct);
        brand.Name = dto.Name.Trim();
        brand.IsActive = dto.IsActive;
        brand.UpdatedAt = DateTime.UtcNow;
        await uow.SaveChangesAsync(ct);
        return brand.ToDto();
    }

    /// <summary>Deactivates the brand when products use it, otherwise deletes it.</summary>
    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await guard.EnsureCanAdministerAsync(ct);
        var brand = await Repo.GetByIdAsync(id, ct) ?? throw NotFoundException.For("Marca", id);
        if (await uow.Products.AnyAsync(p => p.BrandId == id, ct))
            brand.IsActive = false;
        else
            Repo.Remove(brand);
        await uow.SaveChangesAsync(ct);
    }

    private async Task EnsureUniqueAsync(string name, int? excludeId, CancellationToken ct)
    {
        var trimmed = name.Trim();
        if (await Repo.AnyAsync(b => b.Name == trimmed && b.Id != excludeId, ct))
            throw new ConflictException($"Ya existe la marca '{trimmed}'.");
    }
}

public class CategoryService(IUnitOfWork uow, PosGuard guard)
{
    private IRepository<Category> Repo => uow.Repository<Category>();

    public async Task<IReadOnlyList<CategoryDto>> ListAsync(bool includeInactive, CancellationToken ct = default)
    {
        var all = await Repo.ListAsync(null, ct);
        var names = all.ToDictionary(c => c.Id, c => c.Name);
        return all.Where(c => includeInactive || c.IsActive)
            .OrderBy(c => c.ParentId.HasValue ? names.GetValueOrDefault(c.ParentId.Value) : c.Name)
            .ThenBy(c => c.ParentId.HasValue).ThenBy(c => c.Name)
            .Select(c => c.ToDto(c.ParentId.HasValue ? names.GetValueOrDefault(c.ParentId.Value) : null))
            .ToList();
    }

    public async Task<CategoryDto> GetAsync(int id, CancellationToken ct = default)
    {
        var category = await Repo.GetByIdAsync(id, ct) ?? throw NotFoundException.For("Categoría", id);
        return category.ToDto(await ParentNameAsync(category.ParentId, ct));
    }

    public async Task<CategoryDto> CreateAsync(CategorySaveDto dto, CancellationToken ct = default)
    {
        await guard.EnsureCanAdministerAsync(ct);
        await ValidateAsync(dto, null, ct);
        var category = new Category();
        Apply(category, dto);
        Repo.Add(category);
        await uow.SaveChangesAsync(ct);
        return category.ToDto(await ParentNameAsync(category.ParentId, ct));
    }

    public async Task<CategoryDto> UpdateAsync(int id, CategorySaveDto dto, CancellationToken ct = default)
    {
        await guard.EnsureCanAdministerAsync(ct);
        var category = await Repo.GetByIdAsync(id, ct) ?? throw NotFoundException.For("Categoría", id);
        await ValidateAsync(dto, id, ct);
        Apply(category, dto);
        category.UpdatedAt = DateTime.UtcNow;
        await uow.SaveChangesAsync(ct);
        return category.ToDto(await ParentNameAsync(category.ParentId, ct));
    }

    /// <summary>Deactivates the category when it has products or subcategories, otherwise deletes it.</summary>
    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await guard.EnsureCanAdministerAsync(ct);
        var category = await Repo.GetByIdAsync(id, ct) ?? throw NotFoundException.For("Categoría", id);
        var inUse = await uow.Products.AnyAsync(p => p.CategoryId == id, ct) || await Repo.AnyAsync(c => c.ParentId == id, ct);
        if (inUse)
            category.IsActive = false;
        else
            Repo.Remove(category);
        await uow.SaveChangesAsync(ct);
    }

    private static void Apply(Category category, CategorySaveDto dto)
    {
        category.Name = dto.Name.Trim();
        category.Description = dto.Description?.Trim();
        category.ParentId = dto.ParentId;
        category.IsActive = dto.IsActive;
    }

    private async Task ValidateAsync(CategorySaveDto dto, int? id, CancellationToken ct)
    {
        var name = dto.Name.Trim();
        if (await Repo.AnyAsync(c => c.Name == name && c.ParentId == dto.ParentId && c.Id != id, ct))
            throw new ConflictException($"Ya existe la categoría '{name}'.");
        if (dto.ParentId.HasValue)
        {
            if (dto.ParentId == id) throw new DomainException("Una categoría no puede ser su propia categoría padre.");
            var parent = await Repo.GetByIdAsync(dto.ParentId.Value, ct) ?? throw NotFoundException.For("Categoría padre", dto.ParentId.Value);
            if (parent.ParentId.HasValue) throw new DomainException("Solo se permiten dos niveles de categorías.");
        }
    }

    private async Task<string?> ParentNameAsync(int? parentId, CancellationToken ct) =>
        parentId.HasValue ? (await Repo.GetByIdAsync(parentId.Value, ct))?.Name : null;
}

public class SupplierService(IUnitOfWork uow, PosGuard guard)
{
    private IRepository<Supplier> Repo => uow.Repository<Supplier>();

    public async Task<IReadOnlyList<SupplierDto>> ListAsync(string? q, bool includeInactive, CancellationToken ct = default)
    {
        var term = q?.Trim();
        var suppliers = await Repo.ListAsync(s => (includeInactive || s.IsActive) &&
            (string.IsNullOrEmpty(term) || s.Name.Contains(term) || (s.ContactName != null && s.ContactName.Contains(term))), ct);
        return suppliers.OrderBy(s => s.Name).Select(s => s.ToDto()).ToList();
    }

    public async Task<SupplierDto> GetAsync(int id, CancellationToken ct = default) =>
        (await Repo.GetByIdAsync(id, ct) ?? throw NotFoundException.For("Proveedor", id)).ToDto();

    public async Task<SupplierDto> CreateAsync(SupplierSaveDto dto, CancellationToken ct = default)
    {
        await guard.EnsureCanAdministerAsync(ct);
        var supplier = new Supplier();
        Apply(supplier, dto);
        Repo.Add(supplier);
        await uow.SaveChangesAsync(ct);
        return supplier.ToDto();
    }

    public async Task<SupplierDto> UpdateAsync(int id, SupplierSaveDto dto, CancellationToken ct = default)
    {
        await guard.EnsureCanAdministerAsync(ct);
        var supplier = await Repo.GetByIdAsync(id, ct) ?? throw NotFoundException.For("Proveedor", id);
        Apply(supplier, dto);
        supplier.UpdatedAt = DateTime.UtcNow;
        await uow.SaveChangesAsync(ct);
        return supplier.ToDto();
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await guard.EnsureCanAdministerAsync(ct);
        var supplier = await Repo.GetByIdAsync(id, ct) ?? throw NotFoundException.For("Proveedor", id);
        supplier.IsActive = false;
        supplier.UpdatedAt = DateTime.UtcNow;
        await uow.SaveChangesAsync(ct);
    }

    private static void Apply(Supplier s, SupplierSaveDto dto)
    {
        s.Name = dto.Name.Trim();
        s.ContactName = dto.ContactName?.Trim();
        s.Phone = dto.Phone?.Trim();
        s.Email = dto.Email?.Trim();
        s.Address = dto.Address?.Trim();
        s.IsActive = dto.IsActive;
    }
}

public class CustomerService(IUnitOfWork uow)
{
    private IRepository<Customer> Repo => uow.Repository<Customer>();

    public async Task<IReadOnlyList<CustomerDto>> ListAsync(string? q, bool includeInactive, CancellationToken ct = default)
    {
        var term = q?.Trim();
        var customers = await Repo.ListAsync(c => (includeInactive || c.IsActive) &&
            (string.IsNullOrEmpty(term) || c.Name.Contains(term) || (c.Phone != null && c.Phone.Contains(term)) || (c.Email != null && c.Email.Contains(term))), ct);
        return customers.OrderBy(c => c.Name).Select(c => c.ToDto()).ToList();
    }

    public async Task<CustomerDto> GetAsync(int id, CancellationToken ct = default) =>
        (await Repo.GetByIdAsync(id, ct) ?? throw NotFoundException.For("Cliente", id)).ToDto();

    public async Task<CustomerDto> CreateAsync(CustomerSaveDto dto, CancellationToken ct = default)
    {
        var customer = new Customer();
        Apply(customer, dto);
        Repo.Add(customer);
        await uow.SaveChangesAsync(ct);
        return customer.ToDto();
    }

    public async Task<CustomerDto> UpdateAsync(int id, CustomerSaveDto dto, CancellationToken ct = default)
    {
        var customer = await Repo.GetByIdAsync(id, ct) ?? throw NotFoundException.For("Cliente", id);
        Apply(customer, dto);
        customer.UpdatedAt = DateTime.UtcNow;
        await uow.SaveChangesAsync(ct);
        return customer.ToDto();
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var customer = await Repo.GetByIdAsync(id, ct) ?? throw NotFoundException.For("Cliente", id);
        customer.IsActive = false;
        customer.UpdatedAt = DateTime.UtcNow;
        await uow.SaveChangesAsync(ct);
    }

    private static void Apply(Customer c, CustomerSaveDto dto)
    {
        c.Name = dto.Name.Trim();
        c.Phone = dto.Phone?.Trim();
        c.Email = dto.Email?.Trim();
        c.Address = dto.Address?.Trim();
        c.Notes = dto.Notes?.Trim();
        c.IsActive = dto.IsActive;
    }
}
