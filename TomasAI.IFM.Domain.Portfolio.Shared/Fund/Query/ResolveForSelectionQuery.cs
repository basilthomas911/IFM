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

/// <summary>Represents the ResolveForSelectionQuery actor message.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record ResolveForSelectionQuery : IQuery<PortfolioFundStrategySnapshot>
{
    public const string Actor = "PortfolioQuery";
    public const string Verb = "ResolveForSelection";
    public const int ErrorId = 34100;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public ActorEntityId QueryEntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public int PortfolioId { get; init; } = default!;
    [Key(3)] public int? FundId { get; init; } = default!;
    [Key(4)] public int TradingYear { get; init; } = default!;
    [Key(5)] public string DecisionHorizon { get; init; } = default!;
    [Key(6)] public string UnderlyingRoot { get; init; } = default!;
    [Key(7)] public DateTime AsOfUtc { get; init; } = default!;
    [Key(8)] public Guid WorkflowId { get; init; } = default!;
    [Key(9)] public long WorkflowRevision { get; init; } = default!;
    [Key(10)] public Guid RequestCorrelationId { get; init; } = default!;
    [Key(11)] public Guid CorrelationId { get; init; }
    [Key(12)] public DateTime RequestedOnUtc { get; init; }
    [Key(13)] public PortfolioAccessContext Access { get; init; } = new();

    [IgnoreMember] public int ErrorCode => ErrorId;
    [IgnoreMember] public string? QueryParams => QueryEntityId.Format();
    [IgnoreMember] IActorEntityId IQuery.EntityId => QueryEntityId;

    /// <summary>Initializes an empty message for serialization.</summary>
    public ResolveForSelectionQuery() { }

    /// <summary>Initializes the query from its application values.</summary>
    /// <param name="portfolioId">The PortfolioId query value.</param>
    /// <param name="fundId">The FundId query value.</param>
    /// <param name="tradingYear">The TradingYear query value.</param>
    /// <param name="decisionHorizon">The DecisionHorizon query value.</param>
    /// <param name="underlyingRoot">The UnderlyingRoot query value.</param>
    /// <param name="asOfUtc">The AsOfUtc query value.</param>
    /// <param name="workflowId">The WorkflowId query value.</param>
    /// <param name="workflowRevision">The WorkflowRevision query value.</param>
    /// <param name="correlationId">The CorrelationId query value.</param>
    public ResolveForSelectionQuery(int portfolioId, int? fundId, int tradingYear, string decisionHorizon, string underlyingRoot, DateTime asOfUtc, Guid workflowId, long workflowRevision, Guid correlationId)
    {
        PortfolioId = portfolioId;
        FundId = fundId;
        TradingYear = tradingYear;
        DecisionHorizon = decisionHorizon;
        UnderlyingRoot = underlyingRoot;
        AsOfUtc = asOfUtc;
        WorkflowId = workflowId;
        WorkflowRevision = workflowRevision;
        CorrelationId = correlationId;
    }
}
