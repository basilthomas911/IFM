using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Futures.Option;

[MessagePackObject]
public sealed record GetIronCondorOptionTradeQuery : IQuery<EstablishedTradeDefinition>
{
    public const string Actor = FuturesOptionTradeActorNames.Query;
    public const string Verb = "GetIronCondorOptionTrade";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public TradeEntityId TradeId { get; init; }
    [IgnoreMember] public int ErrorCode => 25201;
    [IgnoreMember] public string? QueryParams => null;
}

[MessagePackObject]
public sealed record GetVerticalSpreadOptionTradeQuery : IQuery<EstablishedTradeDefinition>
{
    public const string Actor = FuturesOptionTradeActorNames.Query;
    public const string Verb = "GetVerticalSpreadOptionTrade";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public TradeEntityId TradeId { get; init; }
    [IgnoreMember] public int ErrorCode => 25202;
    [IgnoreMember] public string? QueryParams => null;
}

[MessagePackObject]
public sealed record GetIronCondorOptionTradesQuery : IQuery<EstablishedTradeDefinition[]>
{
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

[MessagePackObject]
public sealed record GetVerticalSpreadOptionTradesQuery : IQuery<EstablishedTradeDefinition[]>
{
    public const string Actor = FuturesOptionTradeActorNames.Query;
    public const string Verb = "GetVerticalSpreadOptionTrades";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public int PortfolioId { get; init; }
    [Key(3)] public int FundId { get; init; }
    [Key(4)] public DateTime FromUtc { get; init; }
    [Key(5)] public DateTime ToUtc { get; init; }
    [Key(6)] public int PageSize { get; init; } = 100;
    [Key(7)] public byte[]? PagingState { get; init; }
    [IgnoreMember] public int ErrorCode => 25206;
    [IgnoreMember] public string? QueryParams => null;
}
