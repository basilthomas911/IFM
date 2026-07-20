using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketDataFeed.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve end-of-day futures data for a specific contract within a date range.
/// </summary>
[MessagePackObject(false)]
public record GetFuturesEodDataByDateRangeParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public string ContractId { get; init; } = string.Empty;
    [Key(1)] public DateOnly StartDate { get; init; }
    [Key(2)] public DateOnly EndDate { get; init; }

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetFuturesEodDataByDateRangeParameter() { }

    [SerializationConstructor]
    public GetFuturesEodDataByDateRangeParameter(string contractId, DateOnly startDate, DateOnly endDate)
    {
        ContractId = contractId ?? string.Empty;
        StartDate = startDate;
        EndDate = endDate;
        QueryParams = $"contractId={ContractId}&startDate={StartDate:yyyy-MM-dd}&endDate={EndDate:yyyy-MM-dd}";
    }

    public string Format()
        => $"{ContractId}.{StartDate:yyyy-MM-dd}.{EndDate:yyyy-MM-dd}";
}
