using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.Caching
{
    public enum DataCacheName
    {
        Undefined,
        boundedContextState,
        StopLossLimit,
        ForwardLossRatioMap,
        FuturesEodData,
        FuturesEodDataRange,
        SignalProcessor,
        ReferenceLookup,
        // Values 8 and 9 were the retired per-tick Redis cache names.
        TradeOrder = 10,
        TradeFillCount,
        TradeDiaryEntry,
        TradePositionId,
        TradePositionAction,
        HedgePositionTradeId,
        FuturesContract,
        FuturesOptionContract,
        NormalCurveTable,
        VixFuturesEodData,
        VixFuturesContractId,
        FuturesTickDataStreamingParameter,
        FuturesOptionTickDataStreamingParameter,
        FundBalanceByOrderId,
        OptionTrade,
        TradePlansMap,
        FundBalance,
        TradePlanForwardLossLimit,
        DomainEvents,
        IronCondorMDILimit,
        FuturesContractSymbol,
        FuturesItiSignalAveragePredictedTrendDelta,
        FuturesItiSignalAveragePredictedTrendDeltaRange,
        FuturesItiSignalMDI,
        TradePlanForwardLossRatio,
        EventStreamId,
        EventNameId,
        // Numeric value 37 was the retired FuturesOpenPrice cache.
        VixFuturesOpenPrice = 38,
        StreamingRequestId,
        RiskFreeRate,
        FuturesRsiSignal,
        FuturesRsiDailySignal,
        EventProjectorState,
    }
}
