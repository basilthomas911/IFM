using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared;

[MessagePackObject(AllowPrivate = true)]
public sealed record MarketOutlookEntityId : IActorEntityId
{
    [Key(0)] public string ContractId { get; init; } = string.Empty;
    [Key(1)] public DateOnly ValueDate { get; init; }

    public MarketOutlookEntityId() { }

    [SerializationConstructor]
    public MarketOutlookEntityId(string contractId, DateOnly valueDate)
    {
        ContractId = contractId ?? string.Empty;
        ValueDate = valueDate;
    }

    public string Format() => $"{ContractId}.{ValueDate:yyyyMMdd}";
}
