using InventoryPOS.Application.Features.Invoicing;

namespace InventoryPOS.Application.Tests;

public class CfdiCalculatorTests
{
    /// <summary>The checks the SAT runs on each concept: IVA is the base times the rate to the cent, and the amounts chain up.</summary>
    private static void AssertSatConsistent(CfdiAmounts cfdi, decimal rate = 0.16m)
    {
        foreach (var l in cfdi.Lines)
        {
            // The SAT rounds the IVA to as many decimals as the base carries (2, or up to 6).
            var decimals = l.Base == Math.Round(l.Base, 2) ? 2 : 6;
            Assert.Equal(Math.Round(l.Base * rate, decimals, MidpointRounding.AwayFromZero), l.Tax);
            Assert.Equal(l.Amount - l.Discount, l.Base);
            Assert.True(l.Discount >= 0);
            Assert.True(Math.Abs(l.UnitValue * l.Quantity - l.Amount) <= 0.01m);
        }
        Assert.Equal(cfdi.Subtotal - cfdi.Discount + cfdi.Tax, cfdi.Total);
    }

    [Fact]
    public void A_single_line_totals_what_the_ticket_charged()
    {
        var cfdi = CfdiCalculator.Calculate([new TaxInclusiveLine(1, 116m, 0)], 0, 16);

        var line = Assert.Single(cfdi.Lines);
        Assert.Equal(100m, line.Base);
        Assert.Equal(16m, line.Tax);
        Assert.Equal(116m, cfdi.Total);
        AssertSatConsistent(cfdi);
    }

    [Theory]
    [InlineData(189.90, 3, 0, 0)]
    [InlineData(45.50, 7, 12.30, 0)]
    [InlineData(299.00, 1, 0, 50)]
    [InlineData(0.99, 13, 0, 0.37)]
    public void Odd_prices_and_discounts_still_add_up_to_the_cent(double price, int qty, double lineDiscount, double generalDiscount)
    {
        var lines = new List<TaxInclusiveLine>
        {
            new(qty, (decimal)price, (decimal)lineDiscount),
            new(2, 87.35m, 0),
            new(0.5m, 133.33m, 0),
        };
        var paid = lines.Sum(l => Math.Round(l.Quantity * l.UnitPrice, 2, MidpointRounding.AwayFromZero) - l.Discount) - (decimal)generalDiscount;

        var cfdi = CfdiCalculator.Calculate(lines, (decimal)generalDiscount, 16);

        Assert.Equal(paid, cfdi.Total);
        AssertSatConsistent(cfdi);
    }

    [Fact]
    public void Every_ticket_amount_from_one_peso_to_a_thousand_is_invoiced_exactly()
    {
        for (var cents = 100; cents <= 100_000; cents++)
        {
            var price = cents / 100m;
            var single = CfdiCalculator.Calculate([new TaxInclusiveLine(1, price, 0)], 0, 16);
            Assert.Equal(price, single.Total);
            AssertSatConsistent(single);

            var two = CfdiCalculator.Calculate([new TaxInclusiveLine(1, price, 0), new TaxInclusiveLine(3, 35.10m, 1.15m)], 2.5m, 16);
            Assert.Equal(price + 105.30m - 1.15m - 2.5m, two.Total);
            AssertSatConsistent(two);
        }
    }

    [Fact]
    public void The_general_discount_is_spread_by_weight_without_losing_cents()
    {
        var shares = CfdiCalculator.Spread(10m, [100m, 50m, 50m]);

        Assert.Equal(10m, shares.Sum());
        Assert.Equal([5m, 2.5m, 2.5m], shares);
    }

    [Fact]
    public void Without_tax_the_base_is_the_price()
    {
        var cfdi = CfdiCalculator.Calculate([new TaxInclusiveLine(2, 50m, 0)], 0, 0);

        Assert.Equal(100m, cfdi.Total);
        Assert.Equal(0m, cfdi.Tax);
        AssertSatConsistent(cfdi, 0m);
    }
}
