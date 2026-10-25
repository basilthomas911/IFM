using MessagePack;
namespace TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

/// <summary>Current theoretical spread prices from four qualified OptionCalculator calls at one frozen instant.</summary>
/// <remarks>SignedSpreadPrice = sum(signedRatio * legPrice). CombinedSpreadPrice = abs(putSpreadPrice) + abs(callSpreadPrice).
/// All four prices are points per strategy unit, distinct from observed execution or quote prices and from forward scenarios.</remarks>
[MessagePackObject]
public sealed record IronCondorCalculatedSpreadPrices
{
    [Key(0)] public double PutSpreadPrice { get; init; }
    [Key(1)] public double CallSpreadPrice { get; init; }
    [Key(2)] public double SignedSpreadPrice { get; init; }
    [Key(3)] public double CombinedSpreadPrice { get; init; }
    [Key(4)] public DateTime CalculatedAtUtc { get; init; }
    [Key(5)] public DateTime ValidUntilUtc { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(6)] public IronCondorCalculatedOptionPrice[] OptionLegPrices { get; init; } = [];
    [Key(7)] public string CalculatorVersion { get; init; } = string.Empty;
    [Key(8)] public string NumericalPolicyVersion { get; init; } = string.Empty;
}
