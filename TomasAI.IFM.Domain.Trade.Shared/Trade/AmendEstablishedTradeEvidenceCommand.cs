using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Trade;

[MessagePackObject]
[Union(0, typeof(Futures.AmendFuturesTradeEvidenceCommand))]
[Union(1, typeof(Futures.Option.AmendOptionTradeEvidenceCommand))]
public abstract record AmendEstablishedTradeEvidenceCommand : EstablishedTradeCommand
{
    [Key(4)] public Guid AmendmentId { get; init; }
    [Key(5)] public decimal CommissionDelta { get; init; }
}
