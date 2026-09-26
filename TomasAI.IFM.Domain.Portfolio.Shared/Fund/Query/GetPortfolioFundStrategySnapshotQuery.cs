using TomasAI.IFM.Domain.Portfolio.Shared.Common;
using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Queries;

/// <summary>Represents the GetPortfolioFundStrategySnapshotQuery actor message.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record GetPortfolioFundStrategySnapshotQuery : IQuery<PortfolioFundStrategySnapshot>
{
    public const string Actor = PortfolioQueryRoutes.Fund;
    public const string Verb = "GetPortfolioFundStrategySnapshot";
    public const int ErrorId = 34100;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public ActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public int PortfolioId { get; init; } = default!;
    [Key(3)] public int TradingYear { get; init; } = default!;
    [Key(4)] public string DecisionHorizon { get; init; } = default!;
    [Key(5)] public string UnderlyingRoot { get; init; } = default!;
    [Key(6)] public string AssetType { get; init; } = default!;
    [Key(7)] public DateTime AsOfUtc { get; init; } = default!;
    [Key(8)] public Guid WorkflowId { get; init; } = default!;
    [Key(9)] public long WorkflowRevision { get; init; } = default!;
    [Key(10)] public Guid RequestCorrelationId { get; init; } = default!;
    [Key(11)] public Guid CorrelationId { get; init; }
    [Key(12)] public DateTime RequestedOnUtc { get; init; }
    [Key(13)] public PortfolioAccessContext Access { get; init; } = new();

    [IgnoreMember] public int ErrorCode => ErrorId;
    [IgnoreMember] public string? QueryParams => EntityId.Format();
    [IgnoreMember] IActorEntityId IQuery.EntityId => EntityId;

    /// <summary>Initializes an empty message for serialization.</summary>
    public GetPortfolioFundStrategySnapshotQuery() { }

    /// <summary>Initializes the query from its application values.</summary>
    /// <param name="portfolioId">The PortfolioId query value.</param>
    /// <param name="tradingYear">The TradingYear query value.</param>
    /// <param name="decisionHorizon">The DecisionHorizon query value.</param>
    /// <param name="underlyingRoot">The UnderlyingRoot query value.</param>
    /// <param name="assetType">The AssetType query value.</param>
    /// <param name="asOfUtc">The AsOfUtc query value.</param>
    /// <param name="workflowId">The WorkflowId query value.</param>
    /// <param name="workflowRevision">The WorkflowRevision query value.</param>
    /// <param name="correlationId">The CorrelationId query value.</param>
    public GetPortfolioFundStrategySnapshotQuery(int portfolioId, int tradingYear, string decisionHorizon, string underlyingRoot, string assetType, DateTime asOfUtc, Guid workflowId, long workflowRevision, Guid correlationId)
    {
        PortfolioId = portfolioId;
        TradingYear = tradingYear;
        DecisionHorizon = decisionHorizon;
        UnderlyingRoot = underlyingRoot;
        AssetType = assetType;
        AsOfUtc = asOfUtc;
        WorkflowId = workflowId;
        WorkflowRevision = workflowRevision;
        CorrelationId = correlationId;
    }

    /// <summary>Rehydrates every serialized field in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject wire value.</param>
    /// <param name="entityId">The EntityId wire value.</param>
    /// <param name="portfolioId">The PortfolioId wire value.</param>
    /// <param name="tradingYear">The TradingYear wire value.</param>
    /// <param name="decisionHorizon">The DecisionHorizon wire value.</param>
    /// <param name="underlyingRoot">The UnderlyingRoot wire value.</param>
    /// <param name="assetType">The AssetType wire value.</param>
    /// <param name="asOfUtc">The AsOfUtc wire value.</param>
    /// <param name="workflowId">The WorkflowId wire value.</param>
    /// <param name="workflowRevision">The WorkflowRevision wire value.</param>
    /// <param name="requestCorrelationId">The RequestCorrelationId wire value.</param>
    /// <param name="correlationId">The CorrelationId wire value.</param>
    /// <param name="requestedOnUtc">The RequestedOnUtc wire value.</param>
    /// <param name="access">The Access wire value.</param>
    [SerializationConstructor]
    public GetPortfolioFundStrategySnapshotQuery(ActorSubject subject, ActorEntityId entityId, int portfolioId, int tradingYear, string decisionHorizon, string underlyingRoot, string assetType, DateTime asOfUtc, Guid workflowId, long workflowRevision, Guid requestCorrelationId, Guid correlationId, DateTime requestedOnUtc, PortfolioAccessContext access)
    {
        Subject = subject;
        EntityId = entityId;
        PortfolioId = portfolioId;
        TradingYear = tradingYear;
        DecisionHorizon = decisionHorizon;
        UnderlyingRoot = underlyingRoot;
        AssetType = assetType;
        AsOfUtc = asOfUtc;
        WorkflowId = workflowId;
        WorkflowRevision = workflowRevision;
        RequestCorrelationId = requestCorrelationId;
        CorrelationId = correlationId;
        RequestedOnUtc = requestedOnUtc;
        Access = access;
    }
}
