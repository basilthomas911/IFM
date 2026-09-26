using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Futures.Option;

[MessagePackObject(AllowPrivate = true)]
public sealed record GetIronCondorOptionTradesQuery : IQuery<EstablishedTradeDefinition[]>
{

    /// <summary>Creates an empty query for serialization and existing callers.</summary>
    public GetIronCondorOptionTradesQuery() { }

    /// <summary>Rehydrates every published query field in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="portfolioId">The PortfolioId field.</param>
    /// <param name="fundId">The FundId field.</param>
    /// <param name="fromUtc">The FromUtc field.</param>
    /// <param name="toUtc">The ToUtc field.</param>
    /// <param name="pageSize">The PageSize field.</param>
    /// <param name="pagingState">The PagingState field.</param>
    [SerializationConstructor]
    public GetIronCondorOptionTradesQuery(ActorSubject subject, IActorEntityId entityId, int portfolioId, int fundId, DateTime fromUtc, DateTime toUtc, int pageSize, byte[]? pagingState)
    {
        Subject = subject;
        EntityId = entityId;
        PortfolioId = portfolioId;
        FundId = fundId;
        FromUtc = fromUtc;
        ToUtc = toUtc;
        PageSize = pageSize;
        PagingState = pagingState;
    }
    public const string Actor = FuturesOptionTradeActorNames.Query;
    public const string Verb = "GetIronCondorOptionTrades";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public int PortfolioId { get; init; }
    [Key(3)] public int FundId { get; init; }
    [Key(4)] public DateTime FromUtc { get; init; }
    [Key(5)] public DateTime ToUtc { get; init; }
    [Key(6)] public int PageSize { get; init; } = 100;
    [Key(7)] public byte[]? PagingState { get; init; }
    [IgnoreMember] public int ErrorCode => 25205;
    [IgnoreMember] public string? QueryParams => null;
}
