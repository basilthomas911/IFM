using TomasAI.IFM.Domain.Trade.Shared;
namespace TomasAI.IFM.UI.Net.ViewModels.Trade;

/// <summary>Reprices bounded option payoff risk at the limit actually submitted by the operator.</summary>
public static class SelectedOrderPriceRisk
{
    public static decimal MaximumOptionLoss(IReadOnlyList<TradeLegDefinition> legs, decimal signedDebit, int units)
    {
        if (legs.Count == 0 || units < 1 || legs.Any(x => x.Strike is null || x.PutCall is not (1 or 2) || x.CashMultiplier <= 0)
            || legs.Select(x => x.CashMultiplier).Distinct().Count() != 1
            || legs.Where(x => x.PutCall == 1).Sum(x => x.SignedQuantity) != 0)
            throw new InvalidOperationException("A bounded option strategy with complete strikes and multipliers is required.");
        var cost = signedDebit * units * legs[0].CashMultiplier;
        return Math.Max(0m, -legs.Select(x => x.Strike!.Value).Append(0m).Distinct().Min(spot =>
            legs.Sum(x => x.SignedQuantity * x.CashMultiplier * Math.Max(x.PutCall == 1 ? spot - x.Strike!.Value : x.Strike!.Value - spot, 0m)) - cost));
    }
}
