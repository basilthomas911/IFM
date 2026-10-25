using MessagePack;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

[MessagePackObject]
public sealed record CapacityLimit([property: Key(0)] CapacityScopeKind ScopeKind, [property: Key(1)] string ScopeKey,
    [property: Key(2)] CapacityMeasure Measure, [property: Key(3)] CapacityUnit Unit,
    [property: Key(4)] decimal Maximum, [property: Key(5)] bool Enabled = true);
