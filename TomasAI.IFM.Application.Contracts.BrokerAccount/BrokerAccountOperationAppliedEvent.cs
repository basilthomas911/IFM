using MessagePack;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.BrokerAccount.Contracts;

/// <summary>Full account state with a named operation and a public projection outcome.</summary>
[MessagePackObject]
public record BrokerAccountOperationAppliedEvent : BrokerAccountChangedEvent, IRequireDurableProjection
{
    bool IRequireDurableProjection.RequiresDurableProjection => true;
    DurableProjectionRequirement IRequireDurableProjection.RequiredProjection =>
        new("BrokerAccountCommandActor", "BrokerAccountEventProjector", EventProjectorStageType.ApplyProjection);

    [Key(9)] public BrokerAccountOperation Operation { get; init; }
    public new const string Verb = "BrokerAccountOperationApplied";
    [IgnoreMember] public override string EventName => nameof(BrokerAccountOperationAppliedEvent);
}
