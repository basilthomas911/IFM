using MessagePack;
using System.Collections.Immutable;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using OrderComposition;
[MessagePackObject(AllowPrivate = true)]
public sealed record ExecuteOrderCompositionPipelineCommand : ICommand<OrderCompositionExecutionId>
{
    [Key(0)] public short SchemaVersion { get; init; }
    [Key(1)] public Guid CommandId { get; init; }
    [Key(2)] public ActorSubject Subject { get; init; }
    [Key(3)] public bool PostEvents { get; init; }
    [Key(4)] public OrderCompositionExecutionId EntityId { get; init; }
    [Key(5)] public int ErrorCode { get; init; } = ErrorId;
    [Key(6)] public BoundedContextName RouteTo { get; init; } = BoundedContextName.OrderCompositionPipelineBoundedContext;
    [Key(7)] public long InputWorkflowRevision { get; init; }
    [Key(8)] public IntrinsicTimeStrategyWorkflowView WorkflowView { get; init; } = new();
    [Key(9)] public FuturesItiSignalGeneratedEvent TriggerEvent { get; init; } = new();
    [Key(10)] public Guid CorrelationId { get; init; }
    [Key(11)] public Guid CausationId { get; init; }
    [Key(12)] public DateTime RequestedAtUtc { get; init; }
    [Key(13)] public DateTime ExpiresAtUtc { get; init; }
    [Key(14)] public DateTime EvaluatedAtUtc { get; init; }
    [Key(15)] public StrategyStageResultEnvelope AcceptedSelectionEnvelope { get; init; } = new();
    [Key(16)] public TradeSelectionBinding SelectionBinding { get; init; } = new();
    [IgnoreMember, Newtonsoft.Json.JsonIgnore, System.Text.Json.Serialization.JsonIgnore]
    FundCompositionReservationResult _reservation = default!;
    [Key(17)] public FundCompositionReservationResult Reservation
    {
        get => _reservation is null ? null! : _reservation with { Trades = _reservation.Trades is null ? null! : [.. _reservation.Trades] };
        init => _reservation = value is null ? null! : value with { Trades = value.Trades is null ? null! : [.. value.Trades] };
    }
    [Key(18)] public CompositionBinding CompositionBinding { get; init; } = new();
    [Key(19)] public MarketCompositionSnapshot MarketSnapshot { get; init; } = default!;
    [Key(20)] public string InputSha256 { get; init; } = "";
    public const string Actor = "OrderCompositionPipelineFunction";
    public const string Verb = "Execute";
    public const int ErrorId = 23024;
    [IgnoreMember] public IntrinsicTimeStrategyWorkflowEntityId WorkflowEntityId => EntityId.WorkflowEntityId;
    [IgnoreMember] public StrategyWorkflowId WorkflowId => EntityId.WorkflowId;
    [IgnoreMember] public string CommandName => nameof(ExecuteOrderCompositionPipelineCommand);
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => RequestedAtUtc;
    [IgnoreMember] public string OriginatedBy => EventSource;
    public string Fingerprint() => CompositionHash.Compute(this with { InputSha256 = "" });
}
