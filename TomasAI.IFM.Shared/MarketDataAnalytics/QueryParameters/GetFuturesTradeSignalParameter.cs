using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve a futures trade signal for a specific contract and value date.
/// </summary>
[MessagePackObject(false)]
public record GetFuturesTradeSignalParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public string ContractId { get; init; } = string.Empty;
    [Key(1)] public DateOnly ValueDate { get; init; }

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetFuturesTradeSignalParameter() { }

    [SerializationConstructor]
    public GetFuturesTradeSignalParameter(string contractId, DateOnly valueDate)
    {
        ContractId = contractId ?? string.Empty;
        ValueDate = valueDate;
        QueryParams = $"contractId={ContractId}&valueDate={ValueDate:yyyy-MM-dd}";
    }

    public string Format()
        => $"{ContractId}.{ValueDate:yyyy-MM-dd}";
}
