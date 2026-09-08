using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;

/// <summary>Reports terminal failure of the Order Composition pipeline calculation.</summary>
/// <remarks>
/// This non-durable typed failure is returned directly by the Function to the Strategy Workflow caller.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public sealed record OrderCompositionFunctionFailedEvent : IErrorEvent<IntrinsicTimeStrategyWorkflowEntityId>
{
    /// <summary>Function actor name.</summary>
    [IgnoreMember] public const string Actor = "OrderCompositionPipelineFunction";
    /// <summary>Stable pipeline failure verb.</summary>
    [IgnoreMember] public const string Verb = "OrderCompositionFunctionFailed";
    /// <summary>Stable event error identifier.</summary>
    [IgnoreMember] public const int ErrorId = 24030;

    /// <summary>Gets the persisted pipeline event subject.</summary>
    [Key(0)] public ActorSubject Subject { get; init; }
    /// <summary>Gets the workflow routing identity.</summary>
    [Key(1)] public IntrinsicTimeStrategyWorkflowEntityId EntityId { get; init; }
    /// <summary>Gets the stable logical pipeline failure identity.</summary>
    [Key(2)] public Guid Id { get; init; }
    /// <summary>Gets the UTC failure timestamp.</summary>
    [Key(3)] public DateTime ErrorDate { get; init; }
    /// <summary>Gets the pipeline event-stream sequence identity.</summary>
    [Key(4)] public long EventId { get; init; }
    /// <summary>Gets the Start pipeline command identity.</summary>
    [Key(5)] public Guid CommandId { get; init; }
    /// <summary>Gets the pipeline Command event source.</summary>
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    /// <summary>Gets the safe pipeline failure message.</summary>
    [Key(7)] public string ErrorMessage { get; init; } = string.Empty;
    /// <summary>Gets the stable pipeline failure code.</summary>
    [Key(8)] public int ErrorCode { get; init; }
    /// <summary>Gets the standard failure classification.</summary>
    [Key(9)] public ErrorType ErrorType { get; init; }
    /// <summary>Gets optional failure diagnostic data.</summary>
    [Key(10)] public string ErrorData { get; init; } = string.Empty;
    /// <summary>Gets the UTC event receipt timestamp.</summary>
    [Key(11)] public DateTime ReceivedOn { get; init; }
    /// <summary>Gets the pipeline aggregate identity.</summary>
    [Key(12)] public string AggregateId { get; init; } = string.Empty;
    /// <summary>Gets the originating command contract name.</summary>
    [Key(13)] public string CommandName { get; init; } = string.Empty;
    /// <summary>Gets safe originating command diagnostic data.</summary>
    [Key(14)] public string CommandData { get; init; } = string.Empty;
    /// <summary>Gets the originating bounded-context route text.</summary>
    [Key(15)] public string RouteTo { get; init; } = string.Empty;
    /// <summary>Gets the workflow execution identity.</summary>
    [Key(16)] public StrategyWorkflowId WorkflowId { get; init; }
    /// <summary>Gets the immutable workflow revision supplied to the pipeline.</summary>
    [Key(17)] public long InputWorkflowRevision { get; init; }
    /// <summary>Gets the workflow correlation identity.</summary>
    [Key(18)] public Guid CorrelationId { get; init; }
    /// <summary>Gets the causative pipeline command or processing-event identity.</summary>
    [Key(19)] public Guid CausationId { get; init; }
    /// <summary>Gets the pipeline workflow stage.</summary>
    [Key(20)] public StrategyWorkflowStage PipelineStage { get; init; }
    [Key(21)] public DateTime ExpiresAtUtc { get; init; }
    [Key(22)] public string ReasonCode { get; init; } = string.Empty;
    [Key(23)] public string InputPayloadSha256 { get; init; } = string.Empty;

    /// <summary>Gets the concrete pipeline event contract name.</summary>
    [IgnoreMember] public string EventName => nameof(OrderCompositionFunctionFailedEvent);
    /// <summary>Gets the local pipeline event-source user for diagnostics.</summary>
    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    /// <summary>Gets the standard error-event classification.</summary>
    [IgnoreMember] public EventType EventType => EventType.ErrorEvent;

}
