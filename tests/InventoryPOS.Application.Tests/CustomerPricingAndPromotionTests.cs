using InventoryPOS.Application.Features.CashRegister;
using InventoryPOS.Application.Features.Catalog;
using InventoryPOS.Application.Features.Promotions;
using InventoryPOS.Application.Features.Sales;
using InventoryPOS.Application.Features.Settings;
using InventoryPOS.Application.Features.Sync;
using InventoryPOS.Domain.Common;
using InventoryPOS.Domain.Constants;
using Microsoft.EntityFrameworkCore;

namespace InventoryPOS.Application.Tests;

public class CustomerPricingAndPromotionTests
{
    private static async Task<(PosTestContext Pos, int VariantId)> ShopAsync()
    {
        var pos = new PosTestContext();
        var dye = await pos.CreateDyeAsync(); // priced at 200
        await pos.ReceiveAsync(dye.Id, ("A", "2027-01-31", 20));
        await pos.Cash.OpenAsync(new CashOpenDto(0, null));
        return (pos, dye.Id);
    }

    private static SaleCreateDto Sale(int variantId, decimal qty, int? customerId = null, decimal discount = 0,
        string? phone = null, string? name = null, bool consent = false) =>
        new(customerId, discount, null, [new SaleItemSaveDto(variantId, qty, null)], [new SalePaymentSaveDto(PaymentMethods.Cash, 5000, null)],
            phone, name, consent);

    [Theory]
    [InlineData("771 104 2290", "7711042290")]
    [InlineData("(771) 104-2290", "7711042290")]
    [InlineData("+52 771 104 2290", "7711042290")]
    [InlineData("52 1 771 104 2290", "7711042290")]
    [InlineData("104 2290", null)]
    public void Phones_are_kept_as_ten_digits(string input, string? expected)
    {
        Assert.Equal(expected, PhoneNumber.Normalize(input));
    }

    [Fact]
    public async Task A_wholesale_customer_gets_their_percentage_off_on_top_of_the_cashiers_discount()
    {
        var (pos, variantId) = await ShopAsync();
        using var _ = pos;
        var customers = new CustomerService(pos.Uow);
        var shop = await customers.CreateAsync(new CustomerSaveDto("Estética Luna", "771 222 4500", null, null, null,
            Type: CustomerTypes.Wholesale, DiscountPercent: 15));

        var sale = await pos.Sales.CreateAsync(Sale(variantId, 3, shop.Id, discount: 10));

        Assert.Equal(600m, sale.Subtotal);
        Assert.Equal(90m + 10m, sale.Discount); // 15 % of 600 plus the cashier's 10
        Assert.Equal(500m, sale.Total);
        Assert.Equal(15m, sale.CustomerDiscountPercent);
    }

    [Fact]
    public async Task A_retail_customer_never_gets_the_wholesale_discount()
    {
        var (pos, variantId) = await ShopAsync();
        using var _ = pos;
        var customers = new CustomerService(pos.Uow);
        // A percentage saved on a retail customer is ignored.
        var ana = await customers.CreateAsync(new CustomerSaveDto("Ana", "7711042290", null, null, null, Type: CustomerTypes.Retail, DiscountPercent: 30));
        Assert.Equal(0m, ana.DiscountPercent);

        var sale = await pos.Sales.CreateAsync(Sale(variantId, 1, ana.Id));

        Assert.Equal(200m, sale.Total);
        Assert.Equal(0m, sale.CustomerDiscountPercent);
    }

    [Fact]
    public async Task A_phone_at_checkout_registers_the_customer_and_links_later_sales()
    {
        var (pos, variantId) = await ShopAsync();
        using var _ = pos;

        var first = await pos.Sales.CreateAsync(Sale(variantId, 1, phone: "771 555 0142", name: "Sofía", consent: true));
        var second = await pos.Sales.CreateAsync(Sale(variantId, 1, phone: "(771) 555-0142"));

        var customer = await pos.Db.Customers.AsNoTracking().SingleAsync(c => c.Phone == "7715550142");
        Assert.Equal("Sofía", customer.Name);
        Assert.True(customer.AcceptsPromotions);
        Assert.NotNull(customer.PromotionsConsentAt);
        Assert.Equal(customer.Id, first.CustomerId);
        Assert.Equal(customer.Id, second.CustomerId);
        Assert.Equal(1, await pos.Db.Customers.CountAsync());
    }

    [Fact]
    public async Task A_phone_without_name_gets_a_readable_placeholder()
    {
        var (pos, variantId) = await ShopAsync();
        using var _ = pos;

        var sale = await pos.Sales.CreateAsync(Sale(variantId, 1, phone: "7715550142"));

        Assert.Equal("Cliente 771 555 0142", sale.CustomerName);
    }

    [Fact]
    public async Task Promotions_only_reach_customers_who_agreed_and_have_a_phone()
    {
        using var pos = new PosTestContext();
        var customers = new CustomerService(pos.Uow);
        var yes = await customers.CreateAsync(new CustomerSaveDto("Ana", "7711042290", null, null, null, AcceptsPromotions: true));
        var no = await customers.CreateAsync(new CustomerSaveDto("Beto", "7711042291", null, null, null));
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(async () =>
        {
            var result = new CustomerSaveValidator().Validate(new CustomerSaveDto("Sin teléfono", null, null, null, null, AcceptsPromotions: true));
            if (!result.IsValid) throw new FluentValidation.ValidationException(result.Errors);
            await Task.CompletedTask;
        });
        var promotions = new PromotionService(pos.Uow, new SettingService(pos.Uow, new PosGuard(pos.Env)), pos.User);
        var promo = await promotions.CreateAsync(new PromotionSaveDto("2x1 en tintes", "Este sábado en todos los tonos Igora.",
            Convert.ToBase64String([0xFF, 0xD8, 0xFF]), "image/jpeg"));

        var audience = await promotions.AudienceAsync(null, null);
        Assert.Equal([yes.Id], audience.Customers.Select(c => c.Id));
        Assert.Equal(1, audience.WithoutConsent);

        var withRecipients = await promotions.AddRecipientsAsync(promo.Id, new RecipientsAddDto([yes.Id, no.Id, yes.Id]));
        var recipient = Assert.Single(withRecipients.Recipients);
        Assert.Equal("7711042290", recipient.Phone);
        Assert.Contains("*2x1 en tintes*", withRecipients.WhatsAppText);
        Assert.Contains("BAJA", withRecipients.WhatsAppText);

        var sent = await promotions.SetStatusAsync(promo.Id, recipient.Id, new RecipientStatusDto(PromotionRecipientStatus.Sent));
        Assert.NotNull(sent.SentAt);
        Assert.Equal(1, (await promotions.ListAsync()).Single().Sent);
    }
}
