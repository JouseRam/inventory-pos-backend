using InventoryPOS.Application.Common;
using InventoryPOS.Application.Features.Inventory;
using InventoryPOS.Application.Features.Sync;
using InventoryPOS.Domain.Constants;
using InventoryPOS.Domain.Entities;
using InventoryPOS.Domain.Interfaces;

namespace InventoryPOS.Application.Features.Products;

public class ProductService(IUnitOfWork uow, StockService stock, PosGuard guard)
{
    private const string InitialStockReference = "INICIAL";

    public async Task<IReadOnlyList<ProductDto>> ListAsync(string? q, int? categoryId, int? brandId, bool includeInactive, bool lowStockOnly,
        CancellationToken ct = default)
    {
        var products = await uow.Products.SearchAsync(q, categoryId, brandId, includeInactive, ct);
        var dtos = products.Select(p => p.ToDto());
        if (lowStockOnly) dtos = dtos.Where(p => p.HasLowStock);
        return dtos.ToList();
    }

    public async Task<ProductDto> GetAsync(int id, CancellationToken ct = default) =>
        (await LoadAsync(id, ct)).ToDto(includeInactiveVariants: true);

    public async Task<ProductDto> CreateAsync(ProductCreateDto dto, CancellationToken ct = default)
    {
        await guard.EnsureCanAdministerAsync(ct);
        if (dto.Variants.Any(v => v.InitialStock > 0)) guard.EnsureCanOperate();
        await ValidateReferencesAsync(dto.BrandId, dto.CategoryId, dto.SupplierId, ct);
        foreach (var v in dto.Variants)
            await EnsureUniqueCodesAsync(v.SKU, v.Barcode, null, ct);

        var productId = await uow.ExecuteInTransactionAsync(async () =>
        {
            var product = new Product
            {
                Name = dto.Name.Trim(),
                Description = dto.Description?.Trim(),
                BrandId = dto.BrandId,
                CategoryId = dto.CategoryId,
                SupplierId = dto.SupplierId,
                TracksBatches = dto.TracksBatches,
            };
            uow.Products.Add(product);
            foreach (var v in dto.Variants)
                await AddVariantAsync(product, v, ct);
            await uow.SaveChangesAsync(ct);
            return product.Id;
        }, ct);

        return await GetAsync(productId, ct);
    }

    public async Task<ProductDto> UpdateAsync(int id, ProductUpdateDto dto, CancellationToken ct = default)
    {
        await guard.EnsureCanAdministerAsync(ct);
        var product = await LoadAsync(id, ct);
        await ValidateReferencesAsync(dto.BrandId, dto.CategoryId, dto.SupplierId, ct);

        // Turning lot tracking on moves current stock into a placeholder lot so batches keep adding up to Stock.
        if (dto.TracksBatches && !product.TracksBatches)
        {
            foreach (var v in product.Variants.Where(v => v.Stock > 0))
            {
                var existing = await uow.Batches.FindByLotAsync(v.Id, StockService.NoLot, ct);
                if (existing != null) existing.Quantity += v.Stock;
                else uow.Batches.Add(new ProductBatch { VariantId = v.Id, LotNumber = StockService.NoLot, Quantity = v.Stock });
            }
        }

        product.Name = dto.Name.Trim();
        product.Description = dto.Description?.Trim();
        product.BrandId = dto.BrandId;
        product.CategoryId = dto.CategoryId;
        product.SupplierId = dto.SupplierId;
        product.TracksBatches = dto.TracksBatches;
        product.IsActive = dto.IsActive;
        product.UpdatedAt = DateTime.UtcNow;
        await uow.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await guard.EnsureCanAdministerAsync(ct);
        var product = await LoadAsync(id, ct);
        product.IsActive = false;
        product.UpdatedAt = DateTime.UtcNow;
        await uow.SaveChangesAsync(ct);
    }

    public async Task<ProductDto> AddVariantAsync(int productId, VariantSaveDto dto, CancellationToken ct = default)
    {
        await guard.EnsureCanAdministerAsync(ct);
        if (dto.InitialStock > 0) guard.EnsureCanOperate();
        var product = await LoadAsync(productId, ct);
        await EnsureUniqueCodesAsync(dto.SKU, dto.Barcode, null, ct);
        await uow.ExecuteInTransactionAsync(async () =>
        {
            await AddVariantAsync(product, dto, ct);
            return await uow.SaveChangesAsync(ct);
        }, ct);
        return await GetAsync(productId, ct);
    }

    /// <summary>Updates variant data; stock is never edited here, only through inventory movements.</summary>
    public async Task<VariantDto> UpdateVariantAsync(int variantId, VariantSaveDto dto, CancellationToken ct = default)
    {
        await guard.EnsureCanAdministerAsync(ct);
        var variant = await uow.Variants.GetWithProductAsync(variantId, ct) ?? throw NotFoundException.For("Variante", variantId);
        await EnsureUniqueCodesAsync(dto.SKU, dto.Barcode, variantId, ct);
        Apply(variant, dto);
        variant.UpdatedAt = DateTime.UtcNow;
        await uow.SaveChangesAsync(ct);
        return variant.ToDto();
    }

    public async Task DeleteVariantAsync(int variantId, CancellationToken ct = default)
    {
        await guard.EnsureCanAdministerAsync(ct);
        var variant = await uow.Variants.GetByIdAsync(variantId, ct) ?? throw NotFoundException.For("Variante", variantId);
        variant.IsActive = false;
        variant.UpdatedAt = DateTime.UtcNow;
        await uow.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<VariantDto>> SearchVariantsAsync(string? q, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q)) return [];
        var variants = await uow.Variants.SearchAsync(q.Trim(), 20, ct);
        return variants.Select(v => v.ToDto()).ToList();
    }

    /// <summary>Exact lookup by barcode or SKU, used by the scanner in the POS.</summary>
    public async Task<VariantDto> GetVariantByCodeAsync(string code, CancellationToken ct = default)
    {
        var variant = await uow.Variants.GetByCodeAsync(code.Trim(), ct) ?? throw new NotFoundException($"No hay producto con el código '{code}'.");
        return variant.ToDto();
    }

    public async Task<IReadOnlyList<VariantDto>> LowStockAsync(CancellationToken ct = default) =>
        (await uow.Variants.GetLowStockAsync(null, ct)).Select(v => v.ToDto()).ToList();

    private async Task AddVariantAsync(Product product, VariantSaveDto dto, CancellationToken ct)
    {
        var variant = new ProductVariant { Product = product };
        Apply(variant, dto);
        product.Variants.Add(variant);
        uow.Variants.Add(variant);
        if (dto.InitialStock > 0)
            await stock.AddAsync(variant, dto.InitialStock, dto.Cost, MovementTypes.AdjustmentIn, InitialStockReference,
                dto.LotNumber, dto.ExpirationDate, "Inventario inicial", ct);
    }

    private static void Apply(ProductVariant v, VariantSaveDto dto)
    {
        v.SKU = dto.SKU.Trim().ToUpperInvariant();
        v.Barcode = string.IsNullOrWhiteSpace(dto.Barcode) ? null : dto.Barcode.Trim();
        v.ShadeCode = dto.ShadeCode?.Trim();
        v.ShadeName = dto.ShadeName?.Trim();
        v.Size = dto.Size;
        v.Unit = dto.Unit ?? UnitsOfMeasure.Piece;
        v.Price = Money.Round(dto.Price);
        v.Cost = Money.Round(dto.Cost);
        v.MinStock = dto.MinStock;
        v.IsActive = dto.IsActive;
    }

    private async Task<Product> LoadAsync(int id, CancellationToken ct) =>
        await uow.Products.GetWithVariantsAsync(id, ct) ?? throw NotFoundException.For("Producto", id);

    private async Task EnsureUniqueCodesAsync(string sku, string? barcode, int? excludeVariantId, CancellationToken ct)
    {
        var normalizedSku = sku.Trim().ToUpperInvariant();
        if (await uow.Variants.AnyAsync(v => v.SKU == normalizedSku && v.Id != excludeVariantId, ct))
            throw new ConflictException($"El SKU '{normalizedSku}' ya está registrado.");
        if (!string.IsNullOrWhiteSpace(barcode))
        {
            var code = barcode.Trim();
            if (await uow.Variants.AnyAsync(v => v.Barcode == code && v.Id != excludeVariantId, ct))
                throw new ConflictException($"El código de barras '{code}' ya está registrado.");
        }
    }

    private async Task ValidateReferencesAsync(int? brandId, int? categoryId, int? supplierId, CancellationToken ct)
    {
        if (brandId.HasValue && !await uow.Repository<Brand>().AnyAsync(b => b.Id == brandId, ct))
            throw NotFoundException.For("Marca", brandId.Value);
        if (categoryId.HasValue && !await uow.Repository<Category>().AnyAsync(c => c.Id == categoryId, ct))
            throw NotFoundException.For("Categoría", categoryId.Value);
        if (supplierId.HasValue && !await uow.Repository<Supplier>().AnyAsync(s => s.Id == supplierId, ct))
            throw NotFoundException.For("Proveedor", supplierId.Value);
    }
}
