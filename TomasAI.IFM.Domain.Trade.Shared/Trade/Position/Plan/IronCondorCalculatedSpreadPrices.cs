using MessagePack;
namespace TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

/// <summary>Exact calculator inputs and output for one leg; prices are per option before quantity or multiplier scaling.</summary>
[MessagePackObject]
public sealed record IronCondorCalculatedOptionPrice
{
    [Key(0)] public string ContractId { get; init; } = string.Empty;
    [Key(1)] public int SignedRatio { get; init; }
    [Key(2)] public bool IsCall { get; init; }
    [Key(3)] public decimal Strike { get; init; }
    [Key(4)] public double UnderlyingPrice { get; init; }
    [Key(5)] public double ImpliedVolatility { get; init; }
    [Key(6)] public double TimeToExpiry { get; init; }
    [Key(7)] public double AnnualContinuousRate { get; init; }
    [Key(8)] public string ExerciseStyle { get; init; } = string.Empty;
    [Key(9)] public string PremiumStyle { get; init; } = string.Empty;
    [Key(10)] public double TheoreticalPrice { get; init; }
    [Key(11)] public string EngineVersion { get; init; } = string.Empty;
    [Key(12)] public string ContextDigest { get; init; } = string.Empty;
    [Key(13)] public DateTime QuoteAsOfUtc { get; init; }
    [Key(14)] public DateTime UnderlyingAsOfUtc { get; init; }
    [Key(15)] public string MappingVersion { get; init; } = string.Empty;
    [Key(16)] public Guid GenerationId { get; init; }
}

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
    [Key(6)] public IronCondorCalculatedOptionPrice[] OptionLegPrices { get; init; } = [];
    [Key(7)] public string CalculatorVersion { get; init; } = string.Empty;
    [Key(8)] public string NumericalPolicyVersion { get; init; } = string.Empty;
}
