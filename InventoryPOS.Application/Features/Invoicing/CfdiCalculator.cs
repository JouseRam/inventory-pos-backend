using InventoryPOS.Application.Common;

namespace InventoryPOS.Application.Features.Invoicing;

/// <summary>A sale line as the ticket shows it: prices include IVA.</summary>
public record TaxInclusiveLine(decimal Quantity, decimal UnitPrice, decimal Discount);

/// <summary>
/// One CFDI concept: amounts before IVA. <c>Amount</c> is the Importe (quantity × unit value),
/// <c>Base</c> = Amount − Discount, and <c>Total</c> = Base + Tax is what the line costs with IVA.
/// </summary>
public record CfdiLine(decimal Quantity, decimal UnitValue, decimal Amount, decimal Discount, decimal Base, decimal Tax, decimal Total);

public record CfdiAmounts(IReadOnlyList<CfdiLine> Lines, decimal Subtotal, decimal Discount, decimal Tax, decimal Total);

/// <summary>
/// Turns a tax-inclusive sale into CFDI concepts. The SAT checks each concept's IVA against its base to the cent,
/// and the customer expects the invoice to total exactly what they paid, so the general discount is spread across
/// lines and each line's base is chosen so base + IVA adds up to the line's paid amount.
/// </summary>
public static class CfdiCalculator
{
    public static CfdiAmounts Calculate(IReadOnlyList<TaxInclusiveLine> lines, decimal generalDiscount, decimal taxRatePercent)
    {
        if (lines.Count == 0) throw new ArgumentException("A sale without lines cannot be invoiced.", nameof(lines));
        var rate = taxRatePercent / 100m;

        var gross = lines.Select(l => Money.Round(l.Quantity * l.UnitPrice)).ToList();
        var paidBeforeGeneral = lines.Select((l, i) => gross[i] - Money.Round(l.Discount)).ToList();
        var shares = Spread(generalDiscount, paidBeforeGeneral);
        var paid = paidBeforeGeneral.Select((p, i) => p - shares[i]).ToList();

        // Each line gets the base whose base + IVA is closest to what was paid for it; a few amounts
        // cannot be hit exactly to the cent, and the leftover cents are absorbed by lines that can move.
        var options = paid.Select(p => Options(p, rate)).ToList();
        var chosen = options.Select(o => o.OrderBy(c => Math.Abs(c.Total - c.Target)).First()).ToList();
        var residual = paid.Sum() - chosen.Sum(c => c.Total);
        for (var i = 0; i < chosen.Count && residual != 0; i++)
        {
            var better = options[i].Where(c => Math.Abs(residual - (c.Total - chosen[i].Total)) < Math.Abs(residual))
                .OrderBy(c => Math.Abs(residual - (c.Total - chosen[i].Total))).FirstOrDefault();
            if (better == null) continue;
            residual -= better.Total - chosen[i].Total;
            chosen[i] = better;
        }
        if (residual != 0)
        {
            // No combination closes it to the cent (always the case for some one-line tickets): the SAT accepts
            // concepts with up to 6 decimals, so the largest line carries the exact amount with 6 decimals.
            var i = Enumerable.Range(0, paid.Count).MaxBy(k => paid[k]);
            var target = chosen[i].Total + residual;
            var precise = Math.Round(target / (1 + rate), 6, MidpointRounding.AwayFromZero);
            chosen[i] = new Option(target, precise, Math.Round(precise * rate, 6, MidpointRounding.AwayFromZero));
        }

        var result = new List<CfdiLine>();
        for (var i = 0; i < lines.Count; i++)
        {
            var (@base, tax) = (chosen[i].Base, chosen[i].Tax);
            // Without any discount the Importe is the base itself; otherwise the undiscounted price before IVA.
            var amount = gross[i] == paid[i] ? @base : Math.Max(@base, Money.Round(gross[i] / (1 + rate)));
            var unitValue = Math.Round(amount / lines[i].Quantity, 6, MidpointRounding.AwayFromZero);
            result.Add(new CfdiLine(lines[i].Quantity, unitValue, amount, amount - @base, @base, tax, @base + tax));
        }

        // Invoice-level amounts carry 2 decimals, as the CFDI header does.
        var subtotal = Money.Round(result.Sum(l => l.Amount));
        var discount = Money.Round(result.Sum(l => l.Discount));
        var totalTax = Money.Round(result.Sum(l => l.Tax));
        return new CfdiAmounts(result, subtotal, discount, totalTax, subtotal - discount + totalTax);
    }

    private record Option(decimal Target, decimal Base, decimal Tax)
    {
        public decimal Total => Base + Tax;
    }

    /// <summary>Bases around paid / (1 + rate), each with its IVA rounded to the cent as the SAT validates it.</summary>
    private static List<Option> Options(decimal paid, decimal rate)
    {
        var center = Money.Round(paid / (1 + rate));
        return Enumerable.Range(-3, 7)
            .Select(step => center + step * 0.01m)
            .Where(b => b >= 0)
            .Select(b => new Option(paid, b, Money.Round(b * rate)))
            .ToList();
    }

    /// <summary>Spreads an amount over the lines in proportion to their weight; the cents left by rounding go to the largest lines.</summary>
    public static List<decimal> Spread(decimal amount, IReadOnlyList<decimal> weights)
    {
        var total = weights.Sum();
        if (amount == 0 || total == 0) return weights.Select(_ => 0m).ToList();
        var shares = weights.Select(w => Math.Floor(amount * w / total * 100m) / 100m).ToList();
        var left = amount - shares.Sum();
        foreach (var i in Enumerable.Range(0, weights.Count).OrderByDescending(i => weights[i]))
        {
            if (left <= 0) break;
            var cent = Math.Min(0.01m, left);
            if (shares[i] + cent > weights[i]) continue;
            shares[i] += cent;
            left -= cent;
        }
        return shares;
    }
}
