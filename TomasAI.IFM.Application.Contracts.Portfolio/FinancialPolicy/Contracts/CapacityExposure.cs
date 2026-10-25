using MessagePack;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Versioned financial contract: CapacityExposure; numeric keys are append-only.</summary>
[MessagePackObject]
public sealed record CapacityExposure
{
    [Key(0)] public CapacityScopeKind ScopeKind { get; init; }
    [Key(1)] public string ScopeKey { get; init; } = string.Empty;
    [Key(2)] public CapacityMeasure Measure { get; init; }
    [Key(3)] public decimal Amount { get; init; }
    [Key(4)] public CapacityUnit Unit { get; init; }
    [Key(5)] public int MethodVersion { get; init; }
}
