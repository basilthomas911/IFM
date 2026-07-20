using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.MarketData.Events;

namespace TomasAI.IFM.Shared.EventSourcing;

// Register known derived event types for MessagePack polymorphic serialization.
[Union(0, typeof(FuturesContractAddedEvent))]
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
        => throw new NotImplementedException($"{EventName}.ToCompleteEvent is not implemented.");

    IErrorEvent<TId> ToFailEvent<TFail, TId>(Exception ex)
        where TFail : IErrorEvent<TId>
        where TId : IActorEntityId
        => throw new NotImplementedException($"{EventName}.ToFailEvent is not implemented.");
}

public interface ICompleteEvent : IEvent { }
public interface ICompleteEvent<TEntityId> : IEvent<TEntityId>, ICompleteEvent
    where TEntityId : IActorEntityId
{
}

public interface IErrorEvent : IEvent {
    DateTime ErrorDate { get; }
    int ErrorCode { get; init; }
    string ErrorMessage { get; init; }
    public ErrorType ErrorType { get; init; }
    public string ErrorData { get; init; }
    public string CommandName { get; init; }
    public string CommandData { get; init; }
}

public interface IErrorEvent<TEntityId> : IEvent<TEntityId>, IErrorEvent
    where TEntityId : IActorEntityId
{
}

public interface IExceptionEvent : IErrorEvent { }

public interface IExceptionEvent<TEntityId> : IEvent<TEntityId>
    where TEntityId : IActorEntityId
{
}