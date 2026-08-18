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

    public static class DataCacheNameExtensions
    {
        public static string ToStringFast(this DataCacheName value) => value switch
        {
            DataCacheName.Undefined => nameof(DataCacheName.Undefined),
            DataCacheName.boundedContextState => nameof(DataCacheName.boundedContextState),
            DataCacheName.StopLossLimit => nameof(DataCacheName.StopLossLimit),
            DataCacheName.ForwardLossRatioMap => nameof(DataCacheName.ForwardLossRatioMap),
            DataCacheName.FuturesEodData => nameof(DataCacheName.FuturesEodData),
            DataCacheName.FuturesEodDataRange => nameof(DataCacheName.FuturesEodDataRange),
            DataCacheName.SignalProcessor => nameof(DataCacheName.SignalProcessor),
            DataCacheName.ReferenceLookup => nameof(DataCacheName.ReferenceLookup),
            DataCacheName.TradeOrder => nameof(DataCacheName.TradeOrder),
            DataCacheName.TradeFillCount => nameof(DataCacheName.TradeFillCount),
            DataCacheName.TradeDiaryEntry => nameof(DataCacheName.TradeDiaryEntry),
            DataCacheName.TradePositionId => nameof(DataCacheName.TradePositionId),
            DataCacheName.TradePositionAction => nameof(DataCacheName.TradePositionAction),
            DataCacheName.HedgePositionTradeId => nameof(DataCacheName.HedgePositionTradeId),
            DataCacheName.FuturesContract => nameof(DataCacheName.FuturesContract),
            DataCacheName.FuturesOptionContract => nameof(DataCacheName.FuturesOptionContract),
            DataCacheName.NormalCurveTable => nameof(DataCacheName.NormalCurveTable),
            DataCacheName.VixFuturesEodData => nameof(DataCacheName.VixFuturesEodData),
            DataCacheName.VixFuturesContractId => nameof(DataCacheName.VixFuturesContractId),
            DataCacheName.FuturesTickDataStreamingParameter => nameof(DataCacheName.FuturesTickDataStreamingParameter),
            DataCacheName.FuturesOptionTickDataStreamingParameter => nameof(DataCacheName.FuturesOptionTickDataStreamingParameter),
            DataCacheName.FundBalanceByOrderId => nameof(DataCacheName.FundBalanceByOrderId),
            DataCacheName.OptionTrade => nameof(DataCacheName.OptionTrade),
            DataCacheName.TradePlansMap => nameof(DataCacheName.TradePlansMap),
            DataCacheName.FundBalance => nameof(DataCacheName.FundBalance),
            DataCacheName.TradePlanForwardLossLimit => nameof(DataCacheName.TradePlanForwardLossLimit),
            DataCacheName.DomainEvents => nameof(DataCacheName.DomainEvents),
            DataCacheName.IronCondorMDILimit => nameof(DataCacheName.IronCondorMDILimit),
            DataCacheName.FuturesContractSymbol => nameof(DataCacheName.FuturesContractSymbol),
            DataCacheName.FuturesItiSignalAveragePredictedTrendDelta => nameof(DataCacheName.FuturesItiSignalAveragePredictedTrendDelta),
            DataCacheName.FuturesItiSignalAveragePredictedTrendDeltaRange => nameof(DataCacheName.FuturesItiSignalAveragePredictedTrendDeltaRange),
            DataCacheName.FuturesItiSignalMDI => nameof(DataCacheName.FuturesItiSignalMDI),
            DataCacheName.TradePlanForwardLossRatio => nameof(DataCacheName.TradePlanForwardLossRatio),
            DataCacheName.EventStreamId => nameof(DataCacheName.EventStreamId),
            DataCacheName.EventNameId => nameof(DataCacheName.EventNameId),
            DataCacheName.VixFuturesOpenPrice => nameof(DataCacheName.VixFuturesOpenPrice),
            DataCacheName.StreamingRequestId => nameof(DataCacheName.StreamingRequestId),
            DataCacheName.RiskFreeRate => nameof(DataCacheName.RiskFreeRate),
            _ => value.ToString()
        };
    }
}
