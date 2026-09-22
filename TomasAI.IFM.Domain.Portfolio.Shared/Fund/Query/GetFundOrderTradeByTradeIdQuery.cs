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

/// <summary>Represents the GetFundOrderTradeByTradeIdQuery actor message.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record GetFundOrderTradeByTradeIdQuery : IQuery<FundOrderTradeProjectionReadModel>
{
    public const string Actor = "PortfolioQuery";
    public const string Verb = "GetFundOrderTradeByTradeId";
    public const int ErrorId = 34100;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public ActorEntityId QueryEntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public int TradeId { get; init; } = default!;
    [Key(3)] public Guid CorrelationId { get; init; }
    [Key(4)] public DateTime RequestedOnUtc { get; init; }
    [Key(5)] public PortfolioAccessContext Access { get; init; } = new();

    [IgnoreMember] public int ErrorCode => ErrorId;
    [IgnoreMember] public string? QueryParams => QueryEntityId.Format();
    [IgnoreMember] IActorEntityId IQuery.EntityId => QueryEntityId;

    /// <summary>Initializes an empty message for serialization.</summary>
    public GetFundOrderTradeByTradeIdQuery() { }

    /// <summary>Initializes the query from its application values.</summary>
    /// <param name="tradeId">The TradeId query value.</param>
    public GetFundOrderTradeByTradeIdQuery(int tradeId)
    {
        TradeId = tradeId;
    }
}
