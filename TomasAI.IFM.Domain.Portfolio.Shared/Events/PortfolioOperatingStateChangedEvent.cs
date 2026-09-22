using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Events;

/// <summary>Represents the PortfolioOperatingStateChangedEvent actor event.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record PortfolioOperatingStateChangedEvent : IPortfolioDomainEvent
{
    public const string Actor = "PortfolioEvent";
    public const string Verb = nameof(PortfolioOperatingStateChangedEvent);
    public const int ErrorCode = 34000;

    [Key(0)] public ActorSubject Subject { get; init; } = ActorSubject.Unknown;
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public ActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = Actor;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public long Revision { get; init; }
    [Key(9)] public DateTime OccurredOnUtc { get; init; }
    [Key(10)] public string Principal { get; init; } = string.Empty;
    [Key(11)] public Guid CorrelationId { get; init; }
    [Key(12)] public Guid CausationId { get; init; }
    [Key(13)] public DateTime OriginatedOnUtc { get; init; }
    [Key(14)] public PortfolioOperatingState State { get; init; } = default!;
    [Key(15)] public string Reason { get; init; } = default!;

    [IgnoreMember] public string UserName => Principal;
    [IgnoreMember] public string EventName => nameof(PortfolioOperatingStateChangedEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    /// <summary>Initializes an empty event for serialization.</summary>
    public PortfolioOperatingStateChangedEvent() { }

    /// <summary>Initializes a new domain event.</summary>
    /// <param name="id">The id value.</param>
    /// <param name="commandId">The commandId value.</param>
    /// <param name="revision">The revision value.</param>
    /// <param name="occurredOnUtc">The occurredOnUtc value.</param>
    /// <param name="principal">The principal value.</param>
    /// <param name="state">The state value.</param>
    /// <param name="reason">The reason value.</param>
    public PortfolioOperatingStateChangedEvent(Guid id, Guid commandId, long revision, DateTime occurredOnUtc, string principal, PortfolioOperatingState state, string reason)
    {
        Id = id;
        CommandId = commandId;
        Revision = revision;
        OccurredOnUtc = occurredOnUtc;
        Principal = principal;
        ReceivedOn = occurredOnUtc;
        OriginatedOnUtc = occurredOnUtc;
        State = state;
        Reason = reason;
    }
}
