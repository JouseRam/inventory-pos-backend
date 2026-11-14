using InventoryPOS.Application.Common;
using InventoryPOS.Application.Features.Settings;
using InventoryPOS.Domain.Entities;
using InventoryPOS.Domain.Interfaces;

namespace InventoryPOS.Application.Features.Labels;

/// <summary>
/// One label as it is printed. Code is what the QR or barcode carries: the lot's ID when the piece belongs to a lot,
/// otherwise the presentation's. The rest is printed as text; the price is read from the catalog at the register,
/// so a label never sells at an old price.
/// </summary>
public record LabelDto(
    string Code, string Name, string? Brand, string? Shade, string? Size, string? Category,
    string? Lot, DateTime? ExpirationDate, decimal Price, int Copies, string Business);

/// <summary>Copies null takes what is on hand (or what was received).</summary>
public record LabelItemRequest(int VariantId, int? BatchId, int? Copies);

public class LabelService(IUnitOfWork uow, SettingService settings)
{
    /// <summary>Labels for what is on hand of a product: one entry per lot with stock, or per presentation without lots.</summary>
    public async Task<IReadOnlyList<LabelDto>> ForProductAsync(int productId, CancellationToken ct = default)
    {
        var product = await uow.Products.GetWithVariantsAsync(productId, ct) ?? throw NotFoundException.For("Producto", productId);
        var requests = new List<LabelItemRequest>();
        foreach (var v in product.Variants.Where(v => v.IsActive).OrderBy(v => v.ShadeCode).ThenBy(v => v.SKU))
        {
            if (product.TracksBatches)
            {
                var batches = await uow.Batches.GetAvailableFefoAsync(v.Id, ct);
                if (batches.Count == 0) requests.Add(new(v.Id, null, 1));
                requests.AddRange(batches.Select(b => new LabelItemRequest(v.Id, b.Id, Pieces(b.Quantity))));
            }
            else
            {
                requests.Add(new(v.Id, null, Math.Max(1, Pieces(v.Stock))));
            }
        }
        return await BuildAsync(requests, ct);
    }

    /// <summary>Labels for a reception: one per piece received, on the lot it came in.</summary>
    public async Task<IReadOnlyList<LabelDto>> ForPurchaseAsync(int purchaseId, CancellationToken ct = default)
    {
        var purchase = await uow.Purchases.GetWithItemsAsync(purchaseId, ct) ?? throw NotFoundException.For("Recepción", purchaseId);
        return await BuildAsync(purchase.Items.OrderBy(i => i.Id).Select(i => new LabelItemRequest(i.VariantId, i.BatchId, Pieces(i.Quantity))).ToList(), ct);
    }

    public async Task<IReadOnlyList<LabelDto>> BuildAsync(IReadOnlyList<LabelItemRequest> items, CancellationToken ct = default)
    {
        var variants = (await uow.Variants.GetByIdsWithProductAsync(items.Select(i => i.VariantId).Distinct(), ct)).ToDictionary(v => v.Id);
        var categories = (await uow.Repository<Category>().ListAsync(null, ct)).ToDictionary(c => c.Id);
        var business = await settings.GetAsync(SettingKeys.BusinessName, ct) ?? "";
        var labels = new List<LabelDto>();
        foreach (var item in items)
        {
            if (!variants.TryGetValue(item.VariantId, out var v)) throw NotFoundException.For("Presentación", item.VariantId);
            var batch = item.BatchId is { } batchId ? await uow.Batches.GetByIdAsync(batchId, ct) : null;
            if (batch != null && batch.VariantId != v.Id) batch = null;
            // A lot without number (stock from before lots were tracked) prints no lot.
            var lot = batch == null || batch.LotNumber == Inventory.StockService.NoLot ? null : batch.LotNumber;
            labels.Add(new LabelDto(
                batch?.Code ?? v.Code ?? v.SKU,
                v.Product.Name,
                v.Product.Brand?.Name,
                Shade(v),
                v.Size is { } size ? $"{size:0.##} {v.Unit}" : null,
                CategoryPath(v.Product.CategoryId, categories),
                lot,
                batch?.ExpirationDate,
                v.Price,
                Math.Clamp(item.Copies ?? 1, 0, 500),
                business));
        }
        return labels;
    }

    private static int Pieces(decimal quantity) => (int)Math.Ceiling(Math.Max(0, quantity));

    private static string? Shade(ProductVariant v) =>
        string.Join(" ", new[] { v.ShadeCode, v.ShadeName }.Where(s => !string.IsNullOrWhiteSpace(s))) is { Length: > 0 } s ? s : null;

    /// <summary>"Cabello - Tintes": the parent category and the subcategory.</summary>
    private static string? CategoryPath(int? categoryId, IReadOnlyDictionary<int, Category> categories)
    {
        if (categoryId is not { } id || !categories.TryGetValue(id, out var category)) return null;
        return category.ParentId is { } parentId && categories.TryGetValue(parentId, out var parent)
            ? $"{parent.Name} - {category.Name}"
            : category.Name;
    }
}
