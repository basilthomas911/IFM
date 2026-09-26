using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Futures;

public static class FuturesTradeActorNames
{
    public const string Command = "FuturesTradeCommand";
    public const string Query = "FuturesTradeQuery";
}
