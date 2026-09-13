using TomasAI.IFM.Domain.Trade.Futures.Realtime.Model;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Realtime.Model;

internal static class FuturesOptionRoutePolicy
{
    internal static bool IsSupported(PortfolioFundTradeLeg route) =>
        route.TradeType is TradeStrategyKind.IronCondor or TradeStrategyKind.VerticalSpread;
}
