using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketDataFeed.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve futures bar data for a contract/symbol and date/time range.
/// </summary>
[MessagePackObject(false)]
public record GetFuturesBarDataParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public string ContractId { get; init; } = string.Empty;
    [Key(1)] public string Symbol { get; init; } = string.Empty;
    [Key(2)] public DateOnly ValueDate { get; init; }
    [Key(3)] public DateTime StartDate { get; init; }
    [Key(4)] public DateTime EndDate { get; init; }

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetFuturesBarDataParameter() { }

    [SerializationConstructor]
    public GetFuturesBarDataParameter(string contractId, string symbol, DateOnly valueDate, DateTime startDate, DateTime endDate)
    {
        ContractId = contractId ?? string.Empty;
        Symbol = symbol ?? string.Empty;
        ValueDate = valueDate;
        StartDate = startDate;
        EndDate = endDate;
        QueryParams = $"contractId={ContractId}&symbol={Symbol}&valueDate={ValueDate:yyyy-MM-dd}&startDate={StartDate:yyyy-MM-ddTHH:mm:ss.fff}&endDate={EndDate:yyyy-MM-ddTHH:mm:ss.fff}";
    }

    public string Format()
        => $"{ContractId}.{Symbol}.{ValueDate:yyyy-MM-dd}.{StartDate:yyyy-MM-ddTHH:mm:ss.fff}.{EndDate:yyyy-MM-ddTHH:mm:ss.fff}";
}
