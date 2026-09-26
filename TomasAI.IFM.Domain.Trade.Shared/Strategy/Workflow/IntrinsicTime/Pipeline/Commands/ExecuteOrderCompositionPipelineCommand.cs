using TomasAI.IFM.Domain.Trade.Shared;
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

    /// <summary>Creates an empty command for serialization and existing callers.</summary>
    public ExecuteOrderCompositionPipelineCommand() { }

    /// <summary>Rehydrates every published command field in permanent numeric-key order.</summary>
    /// <param name="schemaVersion">The SchemaVersion field.</param>
    /// <param name="commandId">The CommandId field.</param>
    /// <param name="subject">The Subject field.</param>
    /// <param name="postEvents">The PostEvents field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="errorCode">The ErrorCode field.</param>
    /// <param name="routeTo">The RouteTo field.</param>
    /// <param name="inputWorkflowRevision">The InputWorkflowRevision field.</param>
    /// <param name="workflowView">The WorkflowView field.</param>
    /// <param name="triggerEvent">The TriggerEvent field.</param>
    /// <param name="correlationId">The CorrelationId field.</param>
    /// <param name="causationId">The CausationId field.</param>
    /// <param name="requestedAtUtc">The RequestedAtUtc field.</param>
    /// <param name="expiresAtUtc">The ExpiresAtUtc field.</param>
    /// <param name="evaluatedAtUtc">The EvaluatedAtUtc field.</param>
    /// <param name="acceptedSelectionEnvelope">The AcceptedSelectionEnvelope field.</param>
    /// <param name="selectionBinding">The SelectionBinding field.</param>
    /// <param name="reservation">The Reservation field.</param>
    /// <param name="compositionBinding">The CompositionBinding field.</param>
    /// <param name="marketSnapshot">The MarketSnapshot field.</param>
    /// <param name="inputSha256">The InputSha256 field.</param>
    [SerializationConstructor]
    public ExecuteOrderCompositionPipelineCommand(short schemaVersion, Guid commandId, ActorSubject subject, bool postEvents, OrderCompositionExecutionId entityId, int errorCode, BoundedContextName routeTo, long inputWorkflowRevision, IntrinsicTimeStrategyWorkflowView workflowView, FuturesItiSignalGeneratedEvent triggerEvent, Guid correlationId, Guid causationId, DateTime requestedAtUtc, DateTime expiresAtUtc, DateTime evaluatedAtUtc, StrategyStageResultEnvelope acceptedSelectionEnvelope, TradeSelectionBinding selectionBinding, FundCompositionReservationResult? reservation, CompositionBinding compositionBinding, MarketCompositionSnapshot marketSnapshot, string inputSha256)
    {
        SchemaVersion = schemaVersion;
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        InputWorkflowRevision = inputWorkflowRevision;
        WorkflowView = workflowView;
        TriggerEvent = triggerEvent;
        CorrelationId = correlationId;
        CausationId = causationId;
        RequestedAtUtc = requestedAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        EvaluatedAtUtc = evaluatedAtUtc;
        AcceptedSelectionEnvelope = acceptedSelectionEnvelope;
        SelectionBinding = selectionBinding;
        Reservation = reservation;
        CompositionBinding = compositionBinding;
        MarketSnapshot = marketSnapshot;
        InputSha256 = inputSha256;
    }
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
    [Key(17)] public FundCompositionReservationResult? Reservation
    {
        get => _reservation is null ? null : _reservation with { Trades = _reservation.Trades is null ? null! : [.. _reservation.Trades] };
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
