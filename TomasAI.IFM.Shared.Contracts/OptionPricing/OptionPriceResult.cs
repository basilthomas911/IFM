using System.Collections.Immutable;

namespace TomasAI.IFM.Shared.OptionPricing;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]

public readonly record struct OptionPriceResult(double? Price, PricingFailure Failure, string EngineVersion)
{
    public bool Success => Failure == PricingFailure.None && Price.HasValue;
}
