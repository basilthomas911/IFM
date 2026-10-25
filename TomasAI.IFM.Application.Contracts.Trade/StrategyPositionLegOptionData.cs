using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

namespace TomasAI.IFM.Domain.Trade.Shared;

/// <summary>Validates and projects the one retained option-leg state. Age is metadata, not a reason to erase values.</summary>
public static class StrategyPositionLegOptionData
{
    /// <summary>Only matching enriched contract metadata may supply currency per price point.</summary>
    public static decimal? ContractMultiplier(StrategyPositionLeg leg)
    {
        var tick = leg.OptionTick?.PricingInputs is null ? leg.OptionRisk : leg.OptionTick;
        return tick?.ContractId == leg.ContractId && tick.PricingInputs?.Multiplier is > 0
            ? tick.PricingInputs.Multiplier : null;
    }

    /// <summary>A strategy multiplier exists only when every leg has the same validated contract multiplier.</summary>
    public static decimal? CommonContractMultiplier(StrategyPositionSnapshot position)
    {
        var values = position.Legs.Select(ContractMultiplier).Distinct().ToArray();
        return position.Legs.Length > 0 && values.Length == 1 && values[0] is > 0 ? values[0] : null;
    }

    public static bool IsValidQuote(FuturesOptionTickDataV2ReadModel value, string contractId) =>
        value.ContractId == contractId && double.IsFinite(value.OptionPrice) && value.OptionPrice > 0 && value.OptionPrice < (double)decimal.MaxValue
        && value.ObservedAtUtc != default && value.ObservedAtUtc.Offset == TimeSpan.Zero
        && (value.IsTradeObservation || double.IsFinite(value.BidPrice) && double.IsFinite(value.AskPrice)
            && value.BidPrice >= 0 && value.AskPrice >= value.BidPrice && value.AskPrice > 0 && value.AskPrice < (double)decimal.MaxValue && value.BidSize >= 0 && value.AskSize >= 0);

    public static bool HasValidRisk(FuturesOptionTickDataV2ReadModel value) => value.GreeksAvailable == true && string.IsNullOrEmpty(value.PricingFailure)
        && double.IsFinite(value.ImpliedVolatility) && value.ImpliedVolatility > 0
        && double.IsFinite(value.UnderlyingPrice) && value.UnderlyingPrice > 0 && value.UnderlyingPrice < (double)decimal.MaxValue
        && double.IsFinite(value.Delta) 
        && double.IsFinite(value.Gamma) 
        && double.IsFinite(value.Vega) 
        && double.IsFinite(value.Theta) && double.IsFinite(value.Rho)
        && value.CalculatedAtUtc != default && value.CalculatedAtUtc.Offset == TimeSpan.Zero
        && value.TheoreticalPrice is { } price && double.IsFinite(price) && price >= 0 && price < (double)decimal.MaxValue;

    public static StrategyPositionLeg Apply(StrategyPositionLeg leg, FuturesOptionTickDataV2ReadModel value) =>
        value.IsTradeObservation ? leg : leg with
        {
            OptionTick = value,
            OptionRisk = HasValidRisk(value) ? value : leg.OptionRisk,
            CashMultiplier = value.PricingInputs is null ? leg.CashMultiplier
                : value.ContractId == leg.ContractId && value.PricingInputs.Multiplier is > 0 ? value.PricingInputs.Multiplier : null,
            Scenario = null
        };

    /// <summary>Read-only compatibility projection; quantities and marks always come from the owning position leg.</summary>
    public static IronCondorTradePlanLegObservation Observe(StrategyPositionLeg leg)
    {
        var quote = leg.OptionTick;
        var risk = leg.OptionRisk;
        var scenario = leg.Scenario;
        return new()
        {
            TradeLegId = leg.TradeLegId, ContractId = leg.ContractId, Strike = leg.Strike, Expiry = leg.Expiry,
            PutCall = leg.PutCall, SignedQuantity = leg.SignedQuantity,
            LegRole = $"{(leg.SignedQuantity < 0 ? "Short" : "Long")}{(leg.PutCall == 1 ? "Call" : "Put")}",
            MarkPrice = leg.CurrentPrice,
            BidPrice = quote is not null ? (decimal)quote.BidPrice : scenario?.BidPrice,
            AskPrice = quote is not null ? (decimal)quote.AskPrice : scenario?.AskPrice,
            TheoreticalPrice = risk?.TheoreticalPrice is { } price ? (decimal)price : scenario?.TheoreticalPrice,
            ImpliedVolatility = risk?.ImpliedVolatility ?? scenario?.ImpliedVolatility,
            Delta = risk?.Delta ?? scenario?.Delta, Gamma = risk?.Gamma ?? scenario?.Gamma,
            Vega = risk is not null ? risk.Vega / 100 : scenario?.Vega,
            Theta = risk?.PricingInputs?.DaysPerYear is > 0 ? risk.Theta / risk.PricingInputs.DaysPerYear : scenario?.Theta,
            ForwardPrice = scenario?.ForwardPrice, ForwardDelta = scenario?.ForwardDelta,
            QuoteAtUtc = quote?.ObservedAtUtc.UtcDateTime ?? scenario?.QuoteAtUtc,
            RiskCalculatedAtUtc = risk?.CalculatedAtUtc.UtcDateTime ?? scenario?.RiskCalculatedAtUtc
        };
    }
}
