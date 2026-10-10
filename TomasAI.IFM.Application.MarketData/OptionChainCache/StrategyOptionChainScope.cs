using TomasAI.IFM.Application.MarketData.Pricing;
namespace TomasAI.IFM.Application.MarketData.OptionChainCache;
/// <summary>Stable policy/bias/date lookup identity, separate from the provider pricing plan identity.</summary>
public static class StrategyOptionChainScope
{
    /// <summary>Identifies one pinned global policy, calendar value date, normalized bias and workflow horizon.</summary>
    public static string Key(Guid parameterSetId, int version, DateOnly valueDate, string bias, string horizon)
    {
        if (parameterSetId == Guid.Empty || version < 1 || valueDate == default
            || bias is not ("Neutral" or "Bullish" or "Bearish") || horizon is not ("Daily" or "Weekly" or "Monthly"))
            throw new ArgumentException("Invalid strategy option chain scope.");
        return PricingSemanticHash.Compute(new { ParameterSetId = parameterSetId, Version = version, ValueDate = valueDate, Bias = bias, Horizon = horizon });
    }
}
