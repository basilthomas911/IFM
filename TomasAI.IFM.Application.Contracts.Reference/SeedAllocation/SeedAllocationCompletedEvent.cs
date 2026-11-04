using MessagePack;
using TomasAI.IFM.Domain.Reference.Shared.ProjectionOutcomes;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Reference.Shared.SeedAllocation;

[MessagePackObject]
public sealed partial record SeedAllocationCompletedEvent : ReferenceProjectionComplete<ActorEntityId>
{
    [Key(8)] public string SeedType { get; init; } = string.Empty;
    [Key(9)] public int Value { get; init; }
}

[MessagePackObject]
public sealed partial record SeedAllocationFailedEvent : ReferenceProjectionFail<ActorEntityId>;
