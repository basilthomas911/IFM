using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve futures trade signal IDs for a specific value date.
/// </summary>
[MessagePackObject(false)]
public record GetFuturesTradeSignalIdsParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public DateOnly ValueDate { get; init; }

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetFuturesTradeSignalIdsParameter() { }

    [SerializationConstructor]
    public GetFuturesTradeSignalIdsParameter(DateOnly valueDate)
    {
        ValueDate = valueDate;
        QueryParams = $"valueDate={ValueDate:yyyy-MM-dd}";
    }

    public string Format()
        => $"{ValueDate:yyyy-MM-dd}";
}
