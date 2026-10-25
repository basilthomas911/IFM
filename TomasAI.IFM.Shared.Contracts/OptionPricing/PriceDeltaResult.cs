using System.Collections.Immutable;

namespace TomasAI.IFM.Shared.OptionPricing;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]

public readonly record struct PriceDeltaResult(
    OptionPriceDeltaValues? Value, PricingFailure Failure, string EngineVersion, int Steps)
{
    public bool Success => Failure == PricingFailure.None && Value.HasValue;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Preserves the existing per-result pricing metadata and calculator instance API.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Preserves the existing instance API used by dependency-injected pricing implementations.")]
    public string PolicyVersion => PricingSettings.Version;
    public OptionPricingRequest Request { get; init; }
    public PricingSettings? NumericalPolicy { get; init; }
}
