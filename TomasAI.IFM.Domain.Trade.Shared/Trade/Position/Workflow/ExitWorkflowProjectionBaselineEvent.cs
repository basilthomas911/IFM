using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;

/// <summary>Verified offline baseline of older exit-stage read rows whose source events did not contain complete facts.</summary>
[MessagePackObject]
public sealed record ExitWorkflowProjectionBaselineEvent : IEvent
{
    [Key(0)] public Guid Id { get; init; }
    [Key(1)] public long EventId { get; init; }
    [Key(2)] public Guid CommandId { get; init; }
    [Key(3)] public string AggregateId { get; init; } = "ExitWorkflowProjectionBaseline";
    [Key(4)] public string EventSource { get; init; } = "ExitWorkflowProjectionBaseline";
    [Key(5)] public DateTime ReceivedOn { get; init; }
    [Key(6)] public long SourceCut { get; init; }
    [Key(7)] public int SchemaVersion { get; init; } = 1;
    /// <summary>Complete immutable stage snapshots in captured key order.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819", Justification = "An array is the permanent MessagePack snapshot payload; callers must preserve the captured facts.")]
    [Key(8)] public ExitPositionWorkflowProjection[] Snapshots { get; init; } = [];
    [Key(9)] public string PayloadSha256 { get; init; } = string.Empty;
    [Key(10)] public ActorSubject Subject { get; init; } = ActorSubject.Unknown;
    [IgnoreMember] public string UserName => "StorageMigration";
    [IgnoreMember] public string EventName => nameof(ExitWorkflowProjectionBaselineEvent);
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;
}
