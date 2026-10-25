using MessagePack;
using TomasAI.IFM.Application.TradeBroker.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.BrokerAccount.Contracts;

[MessagePackObject]
public sealed record BrokerAccountPositionEvidence
{
    [Key(0)] public string ContractId { get; init; } = string.Empty;
    [Key(1)] public int SignedQuantity { get; init; }
    [Key(2)] public decimal AveragePrice { get; init; }
}
