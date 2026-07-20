using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketData.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve the most recent rate of return for a specific symbol on a given date.
/// </summary>
[MessagePackObject(false)]
public record GetLastRateOfReturnParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public string Symbol { get; init; } = string.Empty;

    [Key(1)] public DateOnly ValueDate { get; init; }

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetLastRateOfReturnParameter() { }

    [SerializationConstructor]
    public GetLastRateOfReturnParameter(string symbol, DateOnly valueDate)
    {
        Symbol = symbol ?? string.Empty;
        ValueDate = valueDate;
        QueryParams = $"symbol={Symbol}&valueDate={ValueDate:yyyy-MM-dd}";
    }

    public string Format()
        => $"{Symbol}.{ValueDate:yyyy-MM-dd}";
}
