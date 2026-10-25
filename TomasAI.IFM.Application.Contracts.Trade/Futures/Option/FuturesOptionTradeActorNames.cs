using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Futures.Option;

public static class FuturesOptionTradeActorNames
{
    public const string Command = "FuturesOptionTradeCommand";
    public const string Event = "FuturesOptionTradeEvent";
    public const string Query = "FuturesOptionTradeQuery";
}
