using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve the last futures trade signal.
/// </summary>
[MessagePackObject(false)]
public record GetLastFuturesTradeSignalParameter : IActorEntityId, IQueryParameter
{
    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetLastFuturesTradeSignalParameter()
    {
        QueryParams = string.Empty;
    }

    public string Format()
        => "last";
}
