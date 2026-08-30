using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Queries;

public static class PortfolioQuerySubjects
{
    public const string Actor = "PortfolioQuery";
    public const int ErrorCode = 34100;
}

[MessagePackObject(AllowPrivate = true)]
public sealed record PortfolioQuery<TParameters, TResult> : IQuery<TResult> where TResult : class
{
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public ActorEntityId QueryEntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public TParameters Parameters { get; init; } = default!;
    [Key(3)] public Guid CorrelationId { get; init; }
    [Key(4)] public DateTime RequestedOnUtc { get; init; }
    [IgnoreMember] public int ErrorCode => PortfolioQuerySubjects.ErrorCode;
    [IgnoreMember] public string? QueryParams => QueryEntityId.Format();
    [IgnoreMember] IActorEntityId IQuery.EntityId => QueryEntityId;
}

[MessagePackObject]
public sealed record AllocatePortfolioBusinessIdRequest(
    [property: Key(0)] PortfolioBusinessIdentityKind Kind);

[MessagePackObject] public sealed record GetPortfolioRequest([property: Key(0)] int PortfolioId, [property: Key(1)] long? Version);
[MessagePackObject] public sealed record GetPortfolioRevisionRequest([property: Key(0)] int PortfolioId);
[MessagePackObject] public sealed record GetPortfoliosRequest([property: Key(0)] int? State, [property: Key(1)] int PageSize, [property: Key(2)] string? PageToken);
[MessagePackObject] public sealed record GetFundRequest([property: Key(0)] int PortfolioId, [property: Key(1)] int FundId, [property: Key(2)] long? Version);
[MessagePackObject] public sealed record GetFundRevisionRequest([property: Key(0)] int PortfolioId, [property: Key(1)] int FundId);
[MessagePackObject] public sealed record GetFundsRequest([property: Key(0)] int PortfolioId, [property: Key(1)] int? State, [property: Key(2)] int PageSize, [property: Key(3)] string? PageToken);
[MessagePackObject] public sealed record GetAllocationRequest([property: Key(0)] int PortfolioId, [property: Key(1)] int FundId);
[MessagePackObject] public sealed record GetEnvelopeRequest([property: Key(0)] int PortfolioId, [property: Key(1)] int FundId, [property: Key(2)] DateTime AsOfUtc);
[MessagePackObject] public sealed record GetAssignmentsRequest([property: Key(0)] int PortfolioId, [property: Key(1)] int FundId, [property: Key(2)] long MandateVersion);
[MessagePackObject] public sealed record GetStrategySnapshotRequest([property: Key(0)] int PortfolioId, [property: Key(1)] int TradingYear, [property: Key(2)] string DecisionHorizon, [property: Key(3)] string UnderlyingRoot, [property: Key(4)] string AssetType, [property: Key(5)] DateTime AsOfUtc, [property: Key(6)] Guid WorkflowId, [property: Key(7)] long WorkflowRevision, [property: Key(8)] Guid CorrelationId);
[MessagePackObject] public sealed record GetOrderRequest([property: Key(0)] int OrderId);
[MessagePackObject] public sealed record GetTradeRequest([property: Key(0)] int TradeId);
[MessagePackObject] public sealed record GetCompositionRequest([property: Key(0)] Guid WorkflowId);
[MessagePackObject] public sealed record GetOrdersRequest([property: Key(0)] int PortfolioId, [property: Key(1)] int FundId, [property: Key(2)] DateOnly OrderMonth, [property: Key(3)] int PageSize, [property: Key(4)] string? PageToken);
[MessagePackObject] public sealed record GetOrderTradesRequest([property: Key(0)] int OrderId, [property: Key(1)] int PageSize, [property: Key(2)] string? PageToken);
[MessagePackObject] public sealed record GetStrategyReferenceCombinationsRequest([property: Key(0)] int PortfolioId, [property: Key(1)] DateTime AsOfUtc);
