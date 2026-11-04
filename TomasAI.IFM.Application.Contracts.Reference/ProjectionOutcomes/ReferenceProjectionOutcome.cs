using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.Shared.ProjectionOutcomes;

/// <summary>Correlates a public projection outcome with its committed source event.</summary>
public abstract record ReferenceProjectionOutcome<TEntityId> : IEvent<TEntityId> where TEntityId : IActorEntityId
{
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public TEntityId EntityId { get; init; } = default!;
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public abstract EventType EventType { get; }
}

public abstract record ReferenceProjectionComplete<TEntityId> : ReferenceProjectionOutcome<TEntityId>, ICompleteEvent<TEntityId>
    where TEntityId : IActorEntityId
{
    [IgnoreMember] public override EventType EventType => EventType.CompletedEvent;
}

public abstract record ReferenceProjectionFail<TEntityId> : ReferenceProjectionOutcome<TEntityId>, IErrorEvent<TEntityId>
    where TEntityId : IActorEntityId
{
    [Key(8)] public int ErrorCode { get; init; }
    [Key(9)] public string ErrorMessage { get; init; } = string.Empty;
    [Key(10)] public ErrorType ErrorType { get; init; }
    [Key(11)] public string ErrorData { get; init; } = string.Empty;
    [Key(12)] public string CommandName { get; init; } = string.Empty;
    [Key(13)] public string CommandData { get; init; } = string.Empty;
    [IgnoreMember] public DateTime ErrorDate => ReceivedOn;
    [IgnoreMember] public override EventType EventType => EventType.ErrorEvent;
}
