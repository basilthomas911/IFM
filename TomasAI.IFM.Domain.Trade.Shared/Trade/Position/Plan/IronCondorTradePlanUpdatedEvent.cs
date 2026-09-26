using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

[MessagePackObject]
public sealed record IronCondorTradePlanUpdatedEvent : ICompleteEvent<IronCondorTradePlanId>, IRequireDurableProjection
{
    public const string Verb = "IronCondorTradePlanUpdated";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public IronCondorTradePlanId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = UpdateIronCondorTradePlanCommand.Actor;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public StrategyTradePlanSnapshot Plan { get; init; } = new();
    [Key(9)] public string RequestFingerprint { get; init; } = string.Empty;
    [Key(10)] public Guid SourceEventId { get; init; }
    [IgnoreMember] public string UserName => "TradePlan";
    [IgnoreMember] public string EventName => nameof(IronCondorTradePlanUpdatedEvent);
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;
    [IgnoreMember] public bool RequiresDurableProjection => Plan.MaterialChange;
    [IgnoreMember] public DurableProjectionRequirement RequiredProjection => new(
        "FuturesIronCondorTradePositionCommandActor",
        "IronCondorPositionEventProjector",
        EventProjectorStageType.ApplyProjection);
}
