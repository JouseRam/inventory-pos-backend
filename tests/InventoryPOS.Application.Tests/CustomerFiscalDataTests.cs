using InventoryPOS.Application.Features.Catalog;
using InventoryPOS.Application.Features.Sync;
using InventoryPOS.Domain.Entities;
using InventoryPOS.Infrastructure.Sync;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace InventoryPOS.Application.Tests;

public class CustomerFiscalDataTests
{
    private static readonly CustomerSaveValidator Validator = new();

    private static CustomerSaveDto Fiscal(string rfc, string regime, string use, string zip = "64000", string? legalName = "ANA LÓPEZ") =>
        new("Ana", null, null, null, null, true, rfc, legalName, regime, zip, use);

    [Fact]
    public void A_customer_without_fiscal_data_is_valid()
    {
        Assert.True(Validator.Validate(new CustomerSaveDto("Ana", null, null, null, null)).IsValid);
    }

    [Theory]
    [InlineData("LOAA800101AB1", "612", "G03")] // persona física
    [InlineData("loaa800101ab1", "626", "D01")] // lowercase is normalized
    [InlineData("CLU0101011A1", "601", "G01")]  // persona moral
    public void Valid_fiscal_data_passes(string rfc, string regime, string use)
    {
        Assert.True(Validator.Validate(Fiscal(rfc, regime, use)).IsValid);
    }

    [Theory]
    [InlineData("LOAA801301AB1", "612", "G03", "64000")] // month 13
    [InlineData("LOAA800101AB1", "601", "G03", "64000")] // persona moral regime for a persona física
    [InlineData("CLU0101011A1", "612", "G01", "64000")]  // persona física regime for a persona moral
    [InlineData("CLU0101011A1", "601", "D01", "64000")]  // deduction use for a persona moral
    [InlineData("LOAA800101AB1", "612", "G03", "640")]   // short zip
    public void Invalid_fiscal_data_is_rejected(string rfc, string regime, string use, string zip)
    {
        Assert.False(Validator.Validate(Fiscal(rfc, regime, use, zip)).IsValid);
    }

    [Fact]
    public void Partial_fiscal_data_is_rejected()
    {
        var result = Validator.Validate(new CustomerSaveDto("Ana", null, null, null, null, true, TaxId: "LOAA800101AB1"));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CustomerSaveDto.LegalName));
    }

    [Fact]
    public async Task Fiscal_data_syncs_and_an_older_sender_does_not_wipe_it()
    {
        using var central = new PosTestContext(PosRole.Central);
        var applier = new SyncApplier(central.Db, new UpperInvariantLookupNormalizer());
        var uid = Guid.NewGuid();
        var created = DateTime.UtcNow.AddMinutes(-10);

        await applier.ApplyCustomersAsync([new CustomerSync(uid, "Ana", null, null, null, null, true, created, null,
            true, "LOAA800101AB1", "ANA LÓPEZ", "612", "64000", "G03")], default);
        central.Db.ChangeTracker.Clear();

        // A register still on the previous version edits the phone: it knows nothing about fiscal data.
        await applier.ApplyCustomersAsync([new CustomerSync(uid, "Ana", "8110000000", null, null, null, true, created, DateTime.UtcNow)], default);
        central.Db.ChangeTracker.Clear();

        var customer = await central.Db.Set<Customer>().SingleAsync(c => c.Uid == uid);
        Assert.Equal("8110000000", customer.Phone);
        Assert.Equal("LOAA800101AB1", customer.TaxId);
        Assert.Equal("612", customer.TaxRegime);
        Assert.Equal("G03", customer.CfdiUse);
    }
}
