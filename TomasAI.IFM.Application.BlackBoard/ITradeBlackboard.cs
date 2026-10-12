using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.Blackboard;

public interface ITradeBlackboard
{
    OptionTradeCacheModel OptionTrade { get; }
    TradePositionActionCacheModel TradePositionAction { get; }
    TradePlanForwardLossLimitCacheModel TradePlanForwardLossLimit { get; }
    HedgePositionTradeIdCacheModel HedgePositionTradeId { get; }
    TradeOrderCacheModel TradeOrder { get; }
    IronCondorMDILimitCacheModel IronCondorMDILimit { get; }
    ForwardLossRatioMapCacheModel ForwardLossRatioMap { get; }
    StopLossLimitCacheModel StopLossLimit { get; }
    SignalProcessorCacheModel SignalProcessor { get; }
}
