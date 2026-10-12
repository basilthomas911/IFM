using System;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Shared.Events;

/// <summary>
/// Event emitted when one or more futures option contracts are added to the system.
/// and emit completed/failed events derived from this event.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FuturesOptionContractsAddedEvent : IEvent<FuturesOptionContractsEntityId>
{
    [IgnoreMember] public const string Actor = "FuturesOptionContractEvent";
    [IgnoreMember] public const string Verb = "AddedContracts";
    [IgnoreMember] public const int ErrorCode = 2004;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public FuturesOptionContractsEntityId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    // payload keys (8..)
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve array-valued serialized message and query contracts, including their MessagePack slots.")]
    [Key(8)] public FuturesOptionContractReadModel[] Contracts { get; init; }
    [Key(9)] public DateTime CreatedOn { get; init; }
    [Key(10)] public string CreatedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    /// <summary>
    /// Parameterless constructor required for serializers.
    /// </summary>
    public FuturesOptionContractsAddedEvent() { }

    /// <summary>
    /// MessagePack constructor used for deserialization.
    /// </summary>
    [SerializationConstructor]
    public FuturesOptionContractsAddedEvent(
        ActorSubject subject,
        Guid id,
        FuturesOptionContractsEntityId entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        FuturesOptionContractReadModel[] contracts,
        DateTime createdOn,
        string createdBy)
    {
        Subject = subject;
        EntityId = entityId;
        Id = id;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        Contracts = contracts ?? Array.Empty<FuturesOptionContractReadModel>();
        CreatedOn = createdOn;
        CreatedBy = createdBy ?? string.Empty;
    }

    /// <summary>
    /// Convert this denormalize event into a completed event which indicates successful handling.
    /// </summary>
    /// <typeparam name="TComplete">The complete event type to produce.</typeparam>
    /// <typeparam name="TEntityId">The entity id type (must be <see cref="FuturesOptionContractsEntityId"/>).</typeparam>
    /// <returns>A completed event instance.</returns>
    /// <exception cref="InvalidOperationException">If <typeparamref name="TEntityId"/> is not <see cref="FuturesOptionContractsEntityId"/>.</exception>
    public ICompleteEvent<TEntityId> ToCompleteEvent<TComplete, TEntityId>()
        where TComplete : ICompleteEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        if (typeof(TEntityId) != typeof(FuturesOptionContractsEntityId))
            throw new InvalidOperationException($"ToCompletedEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(FuturesOptionContractsEntityId).FullName}.");

        var completed = new FuturesOptionContractsAddedCompleteEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesOptionContractsAddedCompleteEvent.Actor, FuturesOptionContractsAddedCompleteEvent.Verb, this.Subject.EntityId),
            EntityId = this.EntityId,
            Id = this.Id,
            EventId = this.EventId,
            CommandId = this.CommandId,
            AggregateId = this.AggregateId,
            EventSource = this.EventSource,
            ReceivedOn = this.ReceivedOn,
            Contracts = this.Contracts,
            CreatedOn = this.CreatedOn,
            CreatedBy = this.CreatedBy
        };

        return (ICompleteEvent<TEntityId>)(object)completed;
    }

    /// <summary>
    /// Convert this denormalize event into a failed error event describing the provided exception.
    /// </summary>
    /// <typeparam name="TFail">The error event type to produce.</typeparam>
    /// <typeparam name="TEntityId">The entity id type.</typeparam>
    /// <param name="ex">The exception encountered while denormalizing.</param>
    /// <returns>An error event describing the failure.</returns>
    public IErrorEvent<TEntityId> ToFailEvent<TFail, TEntityId>(Exception ex)
        where TFail : IErrorEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        ArgumentNullException.ThrowIfNull(ex);
        var failed = new FuturesOptionContractsAddedFailEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesOptionContractsAddedFailEvent.Actor, FuturesOptionContractsAddedFailEvent.Verb, this.Subject.EntityId),
            EntityId = this.EntityId,
            Id = this.Id,
            EventId = this.EventId,
            CommandId = this.CommandId,
            AggregateId = this.AggregateId,
            EventSource = this.EventSource,
            ReceivedOn = this.ReceivedOn,
            ErrorMessage = ex.Message,
            ErrorType = ErrorType.Command,
            ErrorCode = ErrorCode
        };
        return (IErrorEvent<TEntityId>)(object)failed;
    }
}
