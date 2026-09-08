using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;

namespace TomasAI.IFM.Framework.MarketData.Pricing;

/// <summary>Resolves the pinned trading increment without rounding a theoretical Black-76 value.</summary>
public static class OptionPremiumTicks
{
    public const string CmeEsGlobexVersion = "CME-358A01.C-2026-09-08/v1";

    /// <summary>
    /// CME 358A01.C: outright and combination net premiums use four inclusive upper bands;
    /// allocated combination legs use 0.05. Signed combination premiums use their magnitude.
    /// This rule does not cover ClearPort, derived blocks or the separate box-spread exception.
    /// </summary>
    public static decimal GetIncrement(OptionPricingConvention contract, decimal premium, bool combinationLeg = false)
    {
        if (contract.SchemaVersion == 1 && contract.PremiumTickRule == OptionPremiumTickRule.Unspecified)
            return contract.TickSize > 0 ? contract.TickSize : throw new ArgumentException("TickRuleUnavailable");
        if (!IsValid(contract)) throw new ArgumentException("TickRuleUnavailable", nameof(contract));
        if (contract.PremiumTickRule == OptionPremiumTickRule.Fixed) return contract.TickSize;
        if (combinationLeg) return .05m;
        // Avoid decimal.Abs(decimal.MinValue) overflow; negative net premiums obey symmetric bands.
        if (premium is >= -5m and <= 5m) return .05m;
        if (premium is >= -20m and <= 20m) return .10m;
        if (premium is >= -100m and <= 100m) return .25m;
        return .50m;
    }

    /// <summary>Schema 1 preserves legacy fixed mappings. Schema 2 requires an explicit supported rule.</summary>
    public static bool IsValid(OptionPricingConvention contract) => contract.SchemaVersion switch
    {
        1 => contract.PremiumTickRule == OptionPremiumTickRule.Unspecified && contract.TickSize > 0,
        2 => contract.PremiumTickRule switch
        {
            OptionPremiumTickRule.Fixed => contract.TickSize > 0 && !string.IsNullOrWhiteSpace(contract.TickRuleVersion),
            OptionPremiumTickRule.CmeEsGlobex358A => contract.Root == "ES" && contract.Dataset == "GLBX.MDP3"
                && contract.Exchange == "XCME" && contract.Currency == "USD" && contract.Multiplier == 50m && contract.TickSize == .05m
                && contract.TickRuleVersion == CmeEsGlobexVersion,
            _ => false
        },
        _ => false
    };
}
