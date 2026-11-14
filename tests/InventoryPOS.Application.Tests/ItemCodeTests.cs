using InventoryPOS.Application.Features.CashRegister;
using InventoryPOS.Application.Features.Products;
using InventoryPOS.Application.Features.Sales;
using InventoryPOS.Domain.Common;
using InventoryPOS.Domain.Constants;
using Microsoft.EntityFrameworkCore;

namespace InventoryPOS.Application.Tests;

public class ItemCodeTests
{
    [Fact]
    public void Ids_only_use_characters_every_scanner_and_keyboard_layout_types_the_same()
    {
        var ids = Enumerable.Range(0, 2000).Select(_ => ItemCode.New(ItemCode.VariantPrefix)).ToList();

        Assert.All(ids, id => Assert.Matches("^P[0-9A-HJKMNP-TV-Z]{8}$", id));
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.Matches("^L", ItemCode.New(ItemCode.BatchPrefix));
    }

    [Theory]
    [InlineData("p7k2-qx9m z", "P7K2QX9MZ")]   // lowercase, dash and space as someone may type it
    [InlineData("P7K2QX9MO", "P7K2QX9M0")]     // O read as zero
    [InlineData("LABCDEFGI", "LABCDEFG1")]     // I read as one; the L prefix stays
    [InlineData("TINTE-71", null)]
    [InlineData("P123", null)]
    public void Typed_or_scanned_ids_are_normalized(string input, string? expected)
    {
        Assert.Equal(expected, ItemCode.Normalize(input));
    }

    [Fact]
    public async Task A_presentation_without_sku_is_known_by_its_id_and_found_by_it()
    {
        using var pos = new PosTestContext();
        var product = await pos.Products.CreateAsync(new ProductCreateDto("Delineador", null, null, null, null, false,
            [new VariantSaveDto(null, null, null, "Negro", 1.2m, UnitsOfMeasure.Gram, 150, 70, 2)]));
        var variant = product.Variants.Single();

        Assert.NotNull(variant.Code);
        Assert.Equal(variant.Code, variant.SKU);
        var found = await pos.Products.GetVariantByCodeAsync(variant.Code!.ToLowerInvariant());
        Assert.Equal(variant.Id, found.Id);
        Assert.Null(found.ScannedBatchId);
        Assert.Single(await pos.Products.SearchVariantsAsync(variant.Code));
    }

    [Fact]
    public async Task Scanning_a_lot_label_sells_from_that_lot_instead_of_the_earliest()
    {
        using var pos = new PosTestContext();
        var dye = await pos.CreateDyeAsync();
        await pos.ReceiveAsync(dye.Id, ("A", "2027-01-31", 5), ("B", "2027-06-30", 5));
        await pos.Cash.OpenAsync(new CashOpenDto(0, null));
        var lotB = await pos.Db.ProductBatches.AsNoTracking().SingleAsync(b => b.LotNumber == "B");

        var scanned = await pos.Products.GetVariantByCodeAsync(lotB.Code!);
        Assert.Equal(dye.Id, scanned.Id);
        Assert.Equal(lotB.Id, scanned.ScannedBatchId);
        Assert.Equal("B", scanned.ScannedLot);

        await pos.Sales.CreateAsync(new SaleCreateDto(null, 0, null,
            [new SaleItemSaveDto(dye.Id, 2, null, 0, scanned.ScannedBatchId)], [new SalePaymentSaveDto(PaymentMethods.Cash, 1000, null)]));

        var lots = await pos.LotsOfAsync(dye.Id);
        Assert.Equal(5, lots["A"]);  // the earliest lot stays on the shelf
        Assert.Equal(3, lots["B"]);
    }

    [Fact]
    public async Task Without_a_scanned_lot_the_earliest_expiration_still_goes_first()
    {
        using var pos = new PosTestContext();
        var dye = await pos.CreateDyeAsync();
        await pos.ReceiveAsync(dye.Id, ("A", "2027-01-31", 5), ("B", "2027-06-30", 5));
        await pos.Cash.OpenAsync(new CashOpenDto(0, null));

        await pos.Sales.CreateAsync(PosTestContext.Sale(dye.Id, 2, (PaymentMethods.Cash, 1000)));

        Assert.Equal(3, (await pos.LotsOfAsync(dye.Id))["A"]);
    }
}
