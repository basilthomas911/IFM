using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using System.Collections.Immutable;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;

/// <summary>Frozen fifth-stage input. Calculation can propose capacity; only Portfolio can reserve it.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record ExecuteRiskManagementPipelineCommand : ICommand<RiskManagementExecutionId>
{

    /// <summary>Creates an empty command for serialization and existing callers.</summary>
    public ExecuteRiskManagementPipelineCommand() { }

    /// <summary>Rehydrates every published command field in permanent numeric-key order.</summary>
    /// <param name="schemaVersion">The SchemaVersion field.</param>
    /// <param name="commandId">The CommandId field.</param>
    /// <param name="subject">The Subject field.</param>
    /// <param name="postEvents">The PostEvents field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="errorCode">The ErrorCode field.</param>
    /// <param name="routeTo">The RouteTo field.</param>
    /// <param name="inputWorkflowRevision">The InputWorkflowRevision field.</param>
    /// <param name="correlationId">The CorrelationId field.</param>
    /// <param name="causationId">The CausationId field.</param>
    /// <param name="requestedAtUtc">The RequestedAtUtc field.</param>
    /// <param name="expiresAtUtc">The ExpiresAtUtc field.</param>
    /// <param name="evaluatedAtUtc">The EvaluatedAtUtc field.</param>
    /// <param name="regimeResult">The RegimeResult field.</param>
    /// <param name="marketConditionResult">The MarketConditionResult field.</param>
    /// <param name="selectionResult">The SelectionResult field.</param>
    /// <param name="compositionResult">The CompositionResult field.</param>
    /// <param name="marketSnapshot">The MarketSnapshot field.</param>
    /// <param name="policy">The Policy field.</param>
    /// <param name="policyId">The PolicyId field.</param>
    /// <param name="policyVersion">The PolicyVersion field.</param>
    /// <param name="policyHash">The PolicyHash field.</param>
    /// <param name="sizingAuthority">The SizingAuthority field.</param>
    /// <param name="authority">The Authority field.</param>
    /// <param name="funding">The Funding field.</param>
    /// <param name="incrementalLossReserve">The IncrementalLossReserve field.</param>
    /// <param name="inputSha256">The InputSha256 field.</param>
    /// <param name="configurationPayloadSha256">The ConfigurationPayloadSha256 field.</param>
    [SerializationConstructor]
    public ExecuteRiskManagementPipelineCommand(short schemaVersion, Guid commandId, ActorSubject subject, bool postEvents, RiskManagementExecutionId entityId, int errorCode, BoundedContextName routeTo, long inputWorkflowRevision, Guid correlationId, Guid causationId, DateTime requestedAtUtc, DateTime expiresAtUtc, DateTime evaluatedAtUtc, StrategyStageResultEnvelope regimeResult, StrategyStageResultEnvelope marketConditionResult, StrategyStageResultEnvelope selectionResult, StrategyStageResultEnvelope compositionResult, MarketCompositionSnapshot marketSnapshot, RiskSizingPolicy policy, Guid policyId, long policyVersion, string policyHash, RiskSizingAuthority sizingAuthority, FinancialAuthorityReference authority, ImmutableArray<RiskQuantityFunding> funding, decimal incrementalLossReserve, string inputSha256, string configurationPayloadSha256)
    {
        SchemaVersion = schemaVersion;
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        InputWorkflowRevision = inputWorkflowRevision;
        CorrelationId = correlationId;
        CausationId = causationId;
        RequestedAtUtc = requestedAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        EvaluatedAtUtc = evaluatedAtUtc;
        RegimeResult = regimeResult;
        MarketConditionResult = marketConditionResult;
        SelectionResult = selectionResult;
        CompositionResult = compositionResult;
        MarketSnapshot = marketSnapshot;
        Policy = policy;
        PolicyId = policyId;
        PolicyVersion = policyVersion;
        PolicyHash = policyHash;
        SizingAuthority = sizingAuthority;
        Authority = authority;
        Funding = funding;
        IncrementalLossReserve = incrementalLossReserve;
        InputSha256 = inputSha256;
        ConfigurationPayloadSha256 = configurationPayloadSha256;
    }
    public const string Actor = "RiskManagementPipelineFunction";
    public const string Verb = "Execute";
    public const int ErrorId = 23025;
    [Key(0)] public short SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid CommandId { get; init; }
    [Key(2)] public ActorSubject Subject { get; init; }
    [Key(3)] public bool PostEvents { get; init; }
    [Key(4)] public RiskManagementExecutionId EntityId { get; init; }
    [Key(5)] public int ErrorCode { get; init; } = ErrorId;
    [Key(6)] public BoundedContextName RouteTo { get; init; } = BoundedContextName.RiskManagementPipelineBoundedContext;
    [Key(7)] public long InputWorkflowRevision { get; init; }
    [Key(8)] public Guid CorrelationId { get; init; }
    [Key(9)] public Guid CausationId { get; init; }
    [Key(10)] public DateTime RequestedAtUtc { get; init; }
    [Key(11)] public DateTime ExpiresAtUtc { get; init; }
    [Key(12)] public DateTime EvaluatedAtUtc { get; init; }
    [Key(13)] public StrategyStageResultEnvelope RegimeResult { get; init; } = new();
    [Key(14)] public StrategyStageResultEnvelope MarketConditionResult { get; init; } = new();
    [Key(15)] public StrategyStageResultEnvelope SelectionResult { get; init; } = new();
    [Key(16)] public StrategyStageResultEnvelope CompositionResult { get; init; } = new();
    [Key(17)] public MarketCompositionSnapshot MarketSnapshot { get; init; } = default!;
    [Key(18)] public RiskSizingPolicy Policy { get; init; } = default!;
    [Key(19)] public Guid PolicyId { get; init; }
    [Key(20)] public long PolicyVersion { get; init; }
    [Key(21)] public string PolicyHash { get; init; } = "";
    [Key(22)] public RiskSizingAuthority SizingAuthority { get; init; } = default!;
    [Key(23)] public FinancialAuthorityReference Authority { get; init; } = new();
    [Key(24)] public ImmutableArray<RiskQuantityFunding> Funding { get; init; } = [];
    [Key(25)] public decimal IncrementalLossReserve { get; init; }
    [Key(26)] public string InputSha256 { get; init; } = "";
    /// <summary>Exact published configuration payload, including emulator method and funding parameters.</summary>
    [Key(27)] public string ConfigurationPayloadSha256 { get; init; } = "";
    [IgnoreMember] public IntrinsicTimeStrategyWorkflowEntityId WorkflowEntityId => EntityId.WorkflowEntityId;
    [IgnoreMember] public StrategyWorkflowId WorkflowId => EntityId.WorkflowId;
    [IgnoreMember] public string CommandName => nameof(ExecuteRiskManagementPipelineCommand);
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => RequestedAtUtc;
    [IgnoreMember] public string OriginatedBy => EventSource;
    public string Fingerprint() => RiskContracts.Hash(this with { InputSha256 = "" });
}
