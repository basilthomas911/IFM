using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.EventSourcing;

public interface IEvent
{
    ActorSubject Subject { get; init; }
    Guid Id { get; init; }
    long EventId { get; init; }
    Guid CommandId { get; init; }
    string AggregateId { get; init; }
    string EventSource { get; init; }
    DateTime ReceivedOn { get; init; }
    string UserName { get; }
    string EventName { get; }
    EventType EventType { get; }
}

public interface IEvent<TEntityId> : IEvent
     where TEntityId : IActorEntityId
{
    TEntityId EntityId { get; init; }

    ICompleteEvent<TId> ToCompleteEvent<TComplete, TId>()
        where TComplete : ICompleteEvent<TId>
        where TId : IActorEntityId
        => throw new NotSupportedException($"{EventName}.ToCompleteEvent is not implemented.");

    IErrorEvent<TId> ToFailEvent<TFail, TId>(Exception ex)
        where TFail : IErrorEvent<TId>
        where TId : IActorEntityId
        => throw new NotSupportedException($"{EventName}.ToFailEvent is not implemented.");
}
