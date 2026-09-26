using System.Globalization;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;

[MessagePackObject]
public sealed record ExitOrderCompositionCompletedEvent : ICompleteEvent<ExitPositionWorkflowId>
{
    public const string Verb = "ExitOrderCompositionCompleted";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public ExitPositionWorkflowId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public ExitOrderComposition Composition { get; init; } = new();
    [Key(9)] public string RequestFingerprint { get; init; } = string.Empty;
    [IgnoreMember] public string UserName => "TradePlan";
    [IgnoreMember] public string EventName => nameof(ExitOrderCompositionCompletedEvent);
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;
}
