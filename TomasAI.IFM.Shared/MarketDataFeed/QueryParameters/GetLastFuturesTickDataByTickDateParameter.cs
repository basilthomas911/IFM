using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketDataFeed.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve the most recent futures tick data for a specific contract and tick date/time.
/// </summary>
[MessagePackObject(false)]
public record GetLastFuturesTickDataByTickDateParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public string ContractId { get; init; } = string.Empty;
    [Key(1)] public DateTime TickDate { get; init; }

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetLastFuturesTickDataByTickDateParameter() { }

    [SerializationConstructor]
    public GetLastFuturesTickDataByTickDateParameter(string contractId, DateTime tickDate)
    {
        ContractId = contractId ?? string.Empty;
        TickDate = tickDate;
        QueryParams = $"contractId={ContractId}&tickDate={TickDate:yyyy-MM-ddTHH:mm:ss.fff}";
    }

    public string Format()
        => $"{ContractId}.{TickDate:yyyy-MM-ddTHH:mm:ss.fff}";
}
