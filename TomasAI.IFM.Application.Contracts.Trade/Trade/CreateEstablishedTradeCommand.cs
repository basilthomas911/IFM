using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Trade;

[MessagePackObject]
[Union(0, typeof(Futures.CreateFuturesTradeCommand))]
[Union(1, typeof(Futures.Option.CreateOptionTradeCommand))]
public abstract record CreateEstablishedTradeCommand : EstablishedTradeCommand
{
    [Key(4)] public EstablishedTradeDefinition Trade { get; init; } = new();
}
