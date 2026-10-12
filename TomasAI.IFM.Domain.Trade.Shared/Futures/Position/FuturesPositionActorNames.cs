using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Futures.Position;

public static class FuturesPositionActorNames
{
    public const string Command = "FuturesTradePositionCommand";
    public const string Query = "FuturesTradePositionQuery";
}
