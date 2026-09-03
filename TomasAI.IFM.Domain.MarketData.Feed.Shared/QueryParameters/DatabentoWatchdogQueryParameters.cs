using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.QueryParameters;

[MessagePackObject(false)]
public sealed record GetDatabentoReadinessParameter : IActorEntityId, IQueryParameter
{
    [IgnoreMember] public string? QueryParams { get; private set; } = string.Empty;
    public string Format() => "databento-readiness";
}

[MessagePackObject(false)]
public sealed record GetDatabentoCurrentContractsParameter : IActorEntityId, IQueryParameter
{
    [IgnoreMember] public string? QueryParams { get; private set; } = string.Empty;
    public string Format() => "databento-current-contracts";
}

[MessagePackObject(false)]
public sealed record GetDatabentoWatchdogHistoryParameter(
    [property: Key(0)] DateOnly? ValueDate = null,
    [property: Key(1)] string? MajorStatus = null,
    [property: Key(2)] int PageSize = 100) : IActorEntityId, IQueryParameter
{
    [IgnoreMember] public string? QueryParams { get; private set; } = string.Empty;
    public string Format() => $"databento-watchdog-history-{ValueDate:yyyyMMdd}-{MajorStatus ?? "all"}-{PageSize}";
}
