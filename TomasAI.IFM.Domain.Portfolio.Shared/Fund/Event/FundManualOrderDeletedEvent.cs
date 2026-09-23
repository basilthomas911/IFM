using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;

/// <summary>Represents the FundManualOrderDeletedEvent actor event.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record FundManualOrderDeletedEvent : IPortfolioFundDomainEvent
{
    public const string Actor = "PortfolioEvent";
    public const string Verb = nameof(FundManualOrderDeletedEvent);
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
    [Key(14)] public int OrderId { get; init; } = default!;
    /// <summary>Gets the economically inactive trades removed with the order.</summary>
    [Key(15)] public int[] RemovedTradeIds { get; init; } = [];

    [IgnoreMember] public string UserName => Principal;
    [IgnoreMember] public string EventName => nameof(FundManualOrderDeletedEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    /// <summary>Initializes an empty event for serialization.</summary>
    public FundManualOrderDeletedEvent() { }

    /// <summary>Initializes a new domain event.</summary>
    /// <param name="id">The id value.</param>
    /// <param name="commandId">The commandId value.</param>
    /// <param name="revision">The revision value.</param>
    /// <param name="occurredOnUtc">The occurredOnUtc value.</param>
    /// <param name="principal">The principal value.</param>
    /// <param name="orderId">The orderId value.</param>
    /// <param name="removedTradeIds">The economically inactive trade identifiers removed with the order.</param>
    public FundManualOrderDeletedEvent(Guid id, Guid commandId, long revision, DateTime occurredOnUtc, string principal, int orderId, int[]? removedTradeIds = null)
    {
        Id = id;
        CommandId = commandId;
        Revision = revision;
        OccurredOnUtc = occurredOnUtc;
        Principal = principal;
        ReceivedOn = occurredOnUtc;
        OriginatedOnUtc = occurredOnUtc;
        OrderId = orderId;
        RemovedTradeIds = removedTradeIds is null ? [] : [.. removedTradeIds];
    }
}
