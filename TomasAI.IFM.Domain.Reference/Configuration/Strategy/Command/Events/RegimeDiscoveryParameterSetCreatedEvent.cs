using MessagePack;
using TomasAI.IFM.Domain.Reference.Shared.Configuration.Strategy;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.Configuration.Strategy.Command.Events;

/// <summary>Records creation of one immutable Draft Regime Discovery parameter-set version.</summary>
[MessagePackObject]
public sealed record RegimeDiscoveryParameterSetCreatedEvent : IEvent<RegimeDiscoveryParameterSetEntityId>
{
    /// <summary>Gets the private event actor family.</summary>
    public const string Actor = "RegimeDiscoveryConfiguration";
    /// <summary>Gets the event verb.</summary>
    public const string Verb = "Created";
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
    /// <summary>Gets the immutable typed payload.</summary>
    [Key(8)] public RegimeDiscoveryParameterSet ParameterSet { get; init; } = new();
    /// <summary>Gets its description.</summary>
    [Key(9)] public string Description { get; init; } = string.Empty;
    /// <summary>Gets its author.</summary>
    [Key(10)] public string CreatedBy { get; init; } = string.Empty;
    /// <summary>Gets the accepted UTC creation timestamp, preserved during materialization.</summary>
    [Key(11)] public DateTime CreatedAtUtc { get; init; }
    /// <inheritdoc />
    [IgnoreMember] public string UserName => CreatedBy;
    /// <inheritdoc />
    [IgnoreMember] public string EventName => nameof(RegimeDiscoveryParameterSetCreatedEvent);
    /// <inheritdoc />
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
}
