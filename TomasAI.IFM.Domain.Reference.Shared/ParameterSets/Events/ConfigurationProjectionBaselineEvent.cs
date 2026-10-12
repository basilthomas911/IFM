using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

/// <summary>Verified configuration facts captured once during an offline Development migration.</summary>
[MessagePackObject]
public sealed record ConfigurationProjectionBaselineEvent : IEvent
{
    [Key(0)] public Guid Id { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; } = ActorSubject.Unknown;
    [Key(2)] public long EventId { get; init; }
    [Key(3)] public Guid CommandId { get; init; }
    [Key(4)] public string AggregateId { get; init; } = "ConfigurationProjectionBaseline";
    [Key(5)] public string EventSource { get; init; } = "ConfigurationProjectionBaseline";
    [Key(6)] public DateTime ReceivedOn { get; init; }
    [Key(7)] public long SourceCut { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819", Justification = "The complete baseline array is an immutable MessagePack wire payload, matching other accepted snapshot events.")]
    [Key(8)] public ConfigurationBaselineTable[] Tables { get; init; } = [];
    [Key(9)] public int SchemaVersion { get; init; } = 1;
    [Key(10)] public long LookupSequenceLastValue { get; init; } = 1;
    [Key(11)] public bool LookupSequenceIsCalled { get; init; }
    [IgnoreMember] public string UserName => "ConfigurationMigration";
    [IgnoreMember] public string EventName => nameof(ConfigurationProjectionBaselineEvent);
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;
}
