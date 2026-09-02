using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.QueryParameters;

[MessagePackObject(AllowPrivate = true)]
public sealed record GetMarketOutlookSnapshotParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public string ContractId { get; init; } = string.Empty;
    [Key(1)] public DateOnly ValueDate { get; init; }
    [Key(2)] public bool LoadPersistedBaseline { get; init; }

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetMarketOutlookSnapshotParameter() { }

    [SerializationConstructor]
    public GetMarketOutlookSnapshotParameter(
        string contractId,
        DateOnly valueDate,
        bool loadPersistedBaseline = false)
    {
        ContractId = contractId ?? string.Empty;
        ValueDate = valueDate;
        LoadPersistedBaseline = loadPersistedBaseline;
        QueryParams = $"contractId={ContractId}&valueDate={ValueDate:yyyy-MM-dd}" +
            $"&loadPersistedBaseline={LoadPersistedBaseline.ToString().ToLowerInvariant()}";
    }

    public string Format() => $"{ContractId}.{ValueDate:yyyyMMdd}";
}
