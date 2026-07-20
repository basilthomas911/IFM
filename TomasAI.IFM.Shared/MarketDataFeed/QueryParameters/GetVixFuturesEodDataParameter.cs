using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketDataFeed.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve end-of-day VIX futures data for a specific contract and date.
/// </summary>
[MessagePackObject(false)]
public record GetVixFuturesEodDataParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public string ContractId { get; init; } = string.Empty;
    [Key(1)] public DateOnly ValueDate { get; init; }

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetVixFuturesEodDataParameter() { }

    [SerializationConstructor]
    public GetVixFuturesEodDataParameter(string contractId, DateOnly valueDate)
    {
        ContractId = contractId ?? string.Empty;
        ValueDate = valueDate;
        QueryParams = $"contractId={ContractId}&valueDate={ValueDate:yyyy-MM-dd}";
    }

    public string Format()
        => $"{ContractId}.{ValueDate:yyyy-MM-dd}";
}
