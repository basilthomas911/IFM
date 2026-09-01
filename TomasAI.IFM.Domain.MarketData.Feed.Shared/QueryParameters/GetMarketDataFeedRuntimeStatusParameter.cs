using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.QueryParameters;

[MessagePackObject(false)]
public sealed record GetMarketDataFeedRuntimeStatusParameter : IActorEntityId, IQueryParameter
{
    [IgnoreMember] public string? QueryParams { get; private set; } = string.Empty;

    public string Format() => "runtime-status";
}
