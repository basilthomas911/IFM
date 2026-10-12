using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;

public static class PositionActorNames
{
    public const string IronCondorCommand = "FuturesIronCondorTradePositionCommand";
    public const string VerticalSpreadCommand = "FuturesVerticalSpreadTradePositionCommand";
    public const string Query = "FuturesOptionTradePositionQuery";
}
