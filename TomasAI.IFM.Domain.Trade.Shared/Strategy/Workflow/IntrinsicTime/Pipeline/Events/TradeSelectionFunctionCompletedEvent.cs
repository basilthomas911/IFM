using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;

/// <summary>Reports that the Trade Selection pipeline completed successfully.</summary>
/// <remarks>
/// The Function projects and stores completed-only state before returning this event directly. It is never published.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public sealed record TradeSelectionFunctionCompletedEvent : ICompleteEvent<IntrinsicTimeStrategyWorkflowEntityId>
{
    /// <summary>Function actor name.</summary>
    [IgnoreMember] public const string Actor = "TradeSelectionPipelineFunction";
    /// <summary>Stable pipeline lifecycle verb.</summary>
    [IgnoreMember] public const string Verb = "TradeSelectionFunctionCompleted";
    /// <summary>Stable event error identifier.</summary>
    [IgnoreMember] public const int ErrorId = 24027;

    /// <summary>Gets the persisted pipeline event subject.</summary>
    [Key(0)] public ActorSubject Subject { get; init; }
    /// <summary>Gets the stable logical pipeline event identity.</summary>
    [Key(1)] public Guid Id { get; init; }
    /// <summary>Gets the workflow routing identity.</summary>
    [Key(2)] public IntrinsicTimeStrategyWorkflowEntityId EntityId { get; init; }
    /// <summary>Gets the pipeline event-stream sequence identity.</summary>
    [Key(3)] public long EventId { get; init; }
    /// <summary>Gets the Start pipeline command identity.</summary>
    [Key(4)] public Guid CommandId { get; init; }
    /// <summary>Gets the pipeline aggregate identity.</summary>
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    /// <summary>Gets the pipeline Command event source.</summary>
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    /// <summary>Gets the UTC event receipt timestamp.</summary>
    [Key(7)] public DateTime ReceivedOn { get; init; }
    /// <summary>Gets the workflow execution identity.</summary>
    [Key(8)] public StrategyWorkflowId WorkflowId { get; init; }
    /// <summary>Gets the immutable workflow revision supplied to the pipeline.</summary>
    [Key(9)] public long InputWorkflowRevision { get; init; }
    /// <summary>Gets the workflow correlation identity.</summary>
    [Key(10)] public Guid CorrelationId { get; init; }
    /// <summary>Gets the causative pipeline command or processing-event identity.</summary>
    [Key(11)] public Guid CausationId { get; init; }
    /// <summary>Gets the pipeline workflow stage.</summary>
    [Key(12)] public StrategyWorkflowStage PipelineStage { get; init; }
    /// <summary>Gets the complete opaque pipeline result.</summary>
    [Key(13)] public StrategyStageResultEnvelope Result { get; init; } = new();
    /// <summary>Gets the UTC pipeline completion timestamp.</summary>
    [Key(14)] public DateTime CompletedAtUtc { get; init; }
    [Key(15)] public DateTime ExpiresAtUtc { get; init; }
    [Key(16)] public string ParameterPayloadSha256 { get; init; } = string.Empty;
    [Key(17)] public DateTime EvaluatedAtUtc { get; init; }
    [Key(18)] public DateTime ValidUntilUtc { get; init; }
    [Key(19)] public string RequestFingerprint { get; init; } = string.Empty;

    /// <summary>Gets the local pipeline event-source user for diagnostics.</summary>
    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    /// <summary>Gets the concrete pipeline event contract name.</summary>
    [IgnoreMember] public string EventName => nameof(TradeSelectionFunctionCompletedEvent);
    /// <summary>Gets the pipeline event classification.</summary>
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;

}
