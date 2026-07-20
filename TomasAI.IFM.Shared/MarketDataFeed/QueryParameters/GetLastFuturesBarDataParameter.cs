using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketDataFeed.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve the last futures bar data available.
/// </summary>
[MessagePackObject(false)]
public record GetLastFuturesBarDataParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public string ContractId { get; init; } = string.Empty;
    [Key(1)] public string Symbol { get; init; } = string.Empty;
    [Key(2)] public DateOnly ValueDate { get; init; }

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetLastFuturesBarDataParameter()
    {
        QueryParams = string.Empty;
    }

    [SerializationConstructor]
    public GetLastFuturesBarDataParameter(string contractId, string symbol, DateOnly valueDate)
    {
        ContractId = contractId ?? string.Empty;
        Symbol = symbol ?? string.Empty;
        ValueDate = valueDate;
        QueryParams = $"contractId={ContractId}&symbol={Symbol}&valueDate={ValueDate:yyyy-MM-dd}";
    }

    public string Format()
        => $"{ContractId}.{Symbol}.{ValueDate:yyyy-MM-dd}";
}
