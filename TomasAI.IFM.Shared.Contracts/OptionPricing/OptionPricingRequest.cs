using System.Collections.Immutable;

namespace TomasAI.IFM.Shared.OptionPricing;

/// <summary>Annual decimal units. Prices and Greeks are per option, before quantities/multipliers.
/// TimeToExpiry is the caller's qualified year fraction; no machine clock is consulted.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
public readonly record struct OptionPricingRequest(
    UnderlyingKind Underlying, ExerciseKind Exercise, PremiumKind Premium,
    OptionSide Side, double UnderlyingPrice, double Strike, double TimeToExpiry,
    double Rate, DividendKind Dividends = DividendKind.None, double DividendYield = 0)
{
    public ImmutableArray<CashDividend> CashDividends { get; init; } = [];
}
