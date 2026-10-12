using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.Blackboard;

internal sealed class TradeBlackboard(
    IRedisCache redisCache,
    IJsonSerializer jsonSerializer) : ITradeBlackboard
{
    public OptionTradeCacheModel OptionTrade { get; } = new(redisCache, jsonSerializer);
    public TradePositionActionCacheModel TradePositionAction { get; } =
        new(redisCache, jsonSerializer);
    public TradePlanForwardLossLimitCacheModel TradePlanForwardLossLimit { get; } =
        new(redisCache, jsonSerializer);
    public HedgePositionTradeIdCacheModel HedgePositionTradeId { get; } =
        new(redisCache, jsonSerializer);
    public TradeOrderCacheModel TradeOrder { get; } = new(redisCache, jsonSerializer);
    public IronCondorMDILimitCacheModel IronCondorMDILimit { get; } =
        new(redisCache, jsonSerializer);
    public ForwardLossRatioMapCacheModel ForwardLossRatioMap { get; } =
        new(redisCache, jsonSerializer);
    public StopLossLimitCacheModel StopLossLimit { get; } = new(redisCache, jsonSerializer);
    public SignalProcessorCacheModel SignalProcessor { get; } =
        new(redisCache, jsonSerializer);
}
