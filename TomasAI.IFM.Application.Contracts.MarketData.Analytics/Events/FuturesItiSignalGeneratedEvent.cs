using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;

/// <summary>
/// Represents a domain event indicating that a Futures ITI signal has been generated.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FuturesItiSignalGeneratedEvent : IEvent<FuturesItiSignalEntityId>, IRequireDurableProjection, TomasAI.IFM.Shared.EventModelActor.IMessageOriginOwner
{
    [MessagePack.IgnoreMember]
    TomasAI.IFM.Shared.EventModelActor.MessageOriginSlot originSlot;
    [MessagePack.IgnoreMember]
    ref TomasAI.IFM.Shared.EventModelActor.MessageOriginSlot TomasAI.IFM.Shared.EventModelActor.IMessageOriginOwner.MessageOriginSlot => ref originSlot;
    /// <summary>Copies business fields while leaving nonserialized creation metadata unassociated.</summary>
    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    protected FuturesItiSignalGeneratedEvent(FuturesItiSignalGeneratedEvent original)
    {
        Subject = original.Subject;
        Id = original.Id;
        EntityId = original.EntityId;
        EventId = original.EventId;
        CommandId = original.CommandId;
        AggregateId = original.AggregateId;
        EventSource = original.EventSource;
        ReceivedOn = original.ReceivedOn;
        FuturesItiSignal = original.FuturesItiSignal;
        CreatedOn = original.CreatedOn;
        CreatedBy = original.CreatedBy;
        VixFuturesPrice = original.VixFuturesPrice;
        DeriveLongerPeriods = original.DeriveLongerPeriods;
        SourceCursor = original.SourceCursor;
        JournalSequence = original.JournalSequence;
    }


    [IgnoreMember] public const string Actor = "FuturesItiSignalEvent";
    [IgnoreMember] public const string Verb = "Generated";
    [IgnoreMember] public const int ErrorCode = 19002;

    // base metadata (keys 0..7)
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public FuturesItiSignalEntityId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    // payload (keys 8..)
    /// <summary>The generated Futures ITI signal view model.</summary>
    [Key(8)] public FuturesItiSignalV2ReadModel? FuturesItiSignal { get; init; }
    /// <summary>The date and time when the signal was created.</summary>
    [Key(9)] public DateTime CreatedOn { get; init; }
    /// <summary>The user who created the signal.</summary>
    [Key(10)] public string CreatedBy { get; init; }
    /// <summary>The source VIX futures price used by deterministic period derivation.</summary>
    [Key(11)] public double VixFuturesPrice { get; init; }
    /// <summary>Deprecated compatibility field. New generation events always set this to false.</summary>
    [Key(12)] public bool DeriveLongerPeriods { get; init; }
    [Key(13)] public FuturesItiSourceCursor? SourceCursor { get; init; }
    [Key(14)] public long JournalSequence { get; init; }
    [IgnoreMember] public DurableProjectionRequirement RequiredProjection => new(
        "FuturesItiSignalCommandActor", "FuturesItiSignalEventProjector",
        TomasAI.IFM.Shared.EventProjector.EventProjectorStageType.ApplyProjection);

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    /// <summary>
    /// Parameterless constructor for MessagePack deserialization.
    /// </summary>
    public FuturesItiSignalGeneratedEvent() { }

    /// <summary>
    /// MessagePack constructor used for deserialization.
    /// </summary>
    [SerializationConstructor]
    public FuturesItiSignalGeneratedEvent(
        ActorSubject subject,
        Guid id,
        FuturesItiSignalEntityId entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        FuturesItiSignalV2ReadModel? futuresItiSignal,
        DateTime createdOn,
        string createdBy,
        double vixFuturesPrice,
        bool deriveLongerPeriods)
    {
        Subject = subject;
        Id = id;
        EntityId = entityId;
        EventId = eventId;
        CommandId = commandId;
        // Preserve the serialized metadata, including null, when nested in event-log snapshots.
        AggregateId = aggregateId;
        EventSource = eventSource;
        ReceivedOn = receivedOn;
        FuturesItiSignal = futuresItiSignal;
        CreatedOn = createdOn;
        CreatedBy = createdBy;
        VixFuturesPrice = vixFuturesPrice;
        DeriveLongerPeriods = deriveLongerPeriods;
    }

    /// <summary>
    /// Convert this denormalize event into a completed event which indicates successful handling.
    /// </summary>
    public ICompleteEvent<TEntityId> ToCompleteEvent<TComplete, TEntityId>()
        where TComplete : ICompleteEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        if (typeof(TEntityId) != typeof(FuturesItiSignalEntityId))
            throw new InvalidOperationException($"{EventName}.ToCompleteEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(FuturesItiSignalEntityId).FullName}.");

        ICompleteEvent<FuturesItiSignalEntityId> completed = new FuturesItiSignalGeneratedCompleteEvent
        {
            Subject = new ActorSubject(this.Subject.ActorType, this.Subject.Name, FuturesItiSignalGeneratedCompleteEvent.Verb, this.Subject.EntityId),
            EntityId = this.EntityId,
            Id = this.Id,
            EventId = this.EventId,
            CommandId = this.CommandId,
            AggregateId = this.AggregateId,
            EventSource = this.EventSource,
            ReceivedOn = this.ReceivedOn,
            FuturesItiSignal = this.FuturesItiSignal,
            VixFuturesPrice = this.VixFuturesPrice,
            DeriveLongerPeriods = this.DeriveLongerPeriods,
            CreatedOn = this.CreatedOn,
            CreatedBy = this.CreatedBy
        };

        return (ICompleteEvent<TEntityId>)completed;
    }

    /// <summary>
    /// Convert this denormalize event into a failed error event describing the provided exception.
    /// </summary>
    public IErrorEvent<TEntityId> ToFailEvent<TFail, TEntityId>(Exception ex)
        where TFail : IErrorEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        ArgumentNullException.ThrowIfNull(ex);
        if (typeof(TEntityId) != typeof(FuturesItiSignalEntityId))
            throw new InvalidOperationException($"{EventName}.ToFailEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(FuturesItiSignalEntityId).FullName}.");

        IErrorEvent<FuturesItiSignalEntityId> failed = new FuturesItiSignalGeneratedFailEvent
        {
            Subject = new ActorSubject(this.Subject.ActorType, this.Subject.Name, FuturesItiSignalGeneratedFailEvent.Verb, this.Subject.EntityId),
            EntityId = this.EntityId,
            Id = this.Id,
            ErrorDate = default,
            EventId = this.EventId,
            CommandId = this.CommandId == Guid.Empty ? Guid.NewGuid() : this.CommandId,
            EventSource = this.EventSource,
            ErrorMessage = ex.Message,
            ErrorType = ErrorType.Command,
            ErrorCode = ErrorCode,
            ErrorData = ex.ToString(),
            ReceivedOn = this.ReceivedOn,
            AggregateId = this.AggregateId
        };

        return (IErrorEvent<TEntityId>)failed;
    }
}
