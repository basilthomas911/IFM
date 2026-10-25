using MessagePack;

namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

/// <summary>Per-symbol short-leg delta and wing-width defaults for an iron condor.</summary>
[MessagePackObject]
public sealed record IronCondorSymbolDefaults
{
    [Key(0)] public string Symbol { get; init; } = string.Empty;
    [Key(1)] public int ShortCallDelta { get; init; }
    [Key(2)] public decimal CallSpreadWidth { get; init; }
    [Key(3)] public int ShortPutDelta { get; init; }
    [Key(4)] public decimal PutSpreadWidth { get; init; }
}
