using System.Collections.Immutable;

namespace TomasAI.IFM.Shared.OptionPricing;

/// <summary>Bounded numerical policy, included in result provenance.</summary>
public sealed record PricingSettings
{
    public int Steps { get; init; } = 801;
    public int MaximumIterations { get; init; } = 100;
    public double MaximumVolatility { get; init; } = 4;
    public double PriceTolerance { get; init; } = 1e-8;
    public int SpatialSteps { get; init; } = 800;
    public int MaximumSorIterations { get; init; } = 2000;
    public double SorTolerance { get; init; } = 1e-9;
    public const string Version = "PricingPolicy/v2";
}
