using MessagePack;

namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

/// <summary>Versioned Market Selection defaults for vertical-spread construction.</summary>
[MessagePackObject]
public sealed record VerticalSpreadMarketSelectionParameterSet
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid ParameterSetId { get; init; }
    [Key(2)] public int Version { get; init; }
    [Key(3)] public string DefaultSymbol { get; init; } = "ES";
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "The published serialization contract uses arrays; preserve its wire shape.")]
    [Key(4)] public VerticalSpreadSymbolDefaults[] Symbols { get; init; } = [];
}
