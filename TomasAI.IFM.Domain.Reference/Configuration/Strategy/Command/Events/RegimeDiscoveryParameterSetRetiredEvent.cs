using MessagePack;
using TomasAI.IFM.Domain.Reference.Shared.Configuration.Strategy;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.Configuration.Strategy.Command.Events;

/// <summary>Records retirement of one published Regime Discovery parameter-set version.</summary>
[MessagePackObject]
public sealed record RegimeDiscoveryParameterSetRetiredEvent : IEvent<RegimeDiscoveryParameterSetEntityId>
{
    /// <summary>Gets the private event actor family.</summary>
    public const string Actor = RegimeDiscoveryParameterSetCreatedEvent.Actor;
    /// <summary>Gets the event verb.</summary>
    public const string Verb = "Retired";
    /// <inheritdoc />
    [Key(0)] public ActorSubject Subject { get; init; }
    /// <inheritdoc />
    [Key(1)] public Guid Id { get; init; }
    /// <inheritdoc />
    [Key(2)] public RegimeDiscoveryParameterSetEntityId EntityId { get; init; }
    /// <inheritdoc />
    [Key(3)] public long EventId { get; init; }
    /// <inheritdoc />
    [Key(4)] public Guid CommandId { get; init; }
    /// <inheritdoc />
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    /// <inheritdoc />
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    /// <inheritdoc />
    [Key(7)] public DateTime ReceivedOn { get; init; }
    /// <summary>Gets the UTC retirement timestamp.</summary>
    [Key(8)] public DateTime RetiredAtUtc { get; init; }
    /// <inheritdoc />
    [IgnoreMember] public string UserName => string.Empty;
    /// <inheritdoc />
    [IgnoreMember] public string EventName => nameof(RegimeDiscoveryParameterSetRetiredEvent);
    /// <inheritdoc />
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
}
