using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve futures ITI signal MDI data by trend for a specific contract, value date, and group.
/// </summary>
[MessagePackObject(false)]
public record GetFuturesItiSignalMDIByTrendParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public string ContractId { get; init; } = string.Empty;
    [Key(1)] public DateOnly ValueDate { get; init; }
    [Key(2)] public int GroupId { get; init; }

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetFuturesItiSignalMDIByTrendParameter() { }

    [SerializationConstructor]
    public GetFuturesItiSignalMDIByTrendParameter(string contractId, DateOnly valueDate, int groupId)
    {
        ContractId = contractId ?? string.Empty;
        ValueDate = valueDate;
        GroupId = groupId;
        QueryParams = $"contractId={ContractId}&valueDate={ValueDate:yyyy-MM-dd}&groupId={GroupId}";
    }

    public string Format()
        => $"{ContractId}.{ValueDate:yyyy-MM-dd}.{GroupId}";
}
