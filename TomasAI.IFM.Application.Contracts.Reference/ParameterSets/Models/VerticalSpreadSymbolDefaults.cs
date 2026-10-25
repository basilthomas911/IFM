using MessagePack;

namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

/// <summary>Per-symbol short-leg delta and width defaults for a vertical spread.</summary>
[MessagePackObject]
public sealed record VerticalSpreadSymbolDefaults
{
    [Key(0)] public string Symbol { get; init; } = string.Empty;
    [Key(1)] public int ShortLegDelta { get; init; }
    [Key(2)] public decimal SpreadWidth { get; init; }
}
