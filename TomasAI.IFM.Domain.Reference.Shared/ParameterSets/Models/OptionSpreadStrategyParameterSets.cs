using MessagePack;

namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

/// <summary>Versioned Market Selection defaults for iron-condor construction.</summary>
[MessagePackObject]
public sealed record IronCondorMarketSelectionParameterSet
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid ParameterSetId { get; init; }
    [Key(2)] public int Version { get; init; }
    [Key(3)] public string DefaultSymbol { get; init; } = "ES";
    [Key(4)] public IronCondorSymbolDefaults[] Symbols { get; init; } = [];
}

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

/// <summary>Versioned Market Selection defaults for vertical-spread construction.</summary>
[MessagePackObject]
public sealed record VerticalSpreadMarketSelectionParameterSet
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid ParameterSetId { get; init; }
    [Key(2)] public int Version { get; init; }
    [Key(3)] public string DefaultSymbol { get; init; } = "ES";
    [Key(4)] public VerticalSpreadSymbolDefaults[] Symbols { get; init; } = [];
}

/// <summary>Per-symbol short-leg delta and width defaults for a vertical spread.</summary>
[MessagePackObject]
public sealed record VerticalSpreadSymbolDefaults
{
    [Key(0)] public string Symbol { get; init; } = string.Empty;
    [Key(1)] public int ShortLegDelta { get; init; }
    [Key(2)] public decimal SpreadWidth { get; init; }
}
