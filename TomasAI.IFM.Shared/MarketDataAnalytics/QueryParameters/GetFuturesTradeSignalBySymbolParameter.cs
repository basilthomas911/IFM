using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve a futures trade signal by symbol and value date.
/// </summary>
/// <remarks>Use this type to specify the symbol and value date when requesting a futures trade signal.
/// This record is typically used as a data transfer object in service or actor-based APIs.</remarks>
[MessagePackObject(false)]
public record GetFuturesTradeSignalBySymbolParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public string Symbol { get; init; } = string.Empty;
    [Key(1)] public DateOnly ValueDate { get; init; }

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetFuturesTradeSignalBySymbolParameter() { }

    [SerializationConstructor]
    public GetFuturesTradeSignalBySymbolParameter(string symbol, DateOnly valueDate)
    {
        Symbol = symbol ?? string.Empty;
        ValueDate = valueDate;
        QueryParams = $"symbol={Symbol}&valueDate={ValueDate:yyyy-MM-dd}";
    }

    public string Format()
        => $"{Symbol}.{ValueDate:yyyy-MM-dd}";
}
