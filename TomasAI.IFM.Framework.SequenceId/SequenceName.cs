namespace TomasAI.IFM.Framework.SequenceId;

public enum SequenceName
{
    FuturesTickData_TickId,
    FuturesOptionTickData_TickId,
    FuturesItiSignal_SequenceId,
    FuturesItiTrendClassData_SequenceId,
    FuturesItiTrendDeltaData_SequenceId,
    FuturesTradeSignal_SequenceId,
    TelemetryLog_SequenceId,
    SpreadDistribution_Id,
    TradePlan_SequenceId,
    OptionTradeSpreadData_SequenceId,
    OptionQuote_QuoteId,
    StreamingRequest_RequestId,
    OptionQuoteData_SequenceId,
    MarketDataFeed_RequestId,
    TradePlacementSignal_SequenceId,
    FundTransaction_TransactionId,
    FuturesIntraDay_SequenceId,
    FuturesOptionTickPriceData_TickId,

}

public static class SequenceNameExtensions
{
    public static string ToStringFast(this SequenceName value) => value switch
    {
        SequenceName.FuturesTickData_TickId => nameof(SequenceName.FuturesTickData_TickId),
        SequenceName.FuturesOptionTickData_TickId => nameof(SequenceName.FuturesOptionTickData_TickId),
        SequenceName.FuturesItiSignal_SequenceId => nameof(SequenceName.FuturesItiSignal_SequenceId),
        SequenceName.FuturesItiTrendClassData_SequenceId => nameof(SequenceName.FuturesItiTrendClassData_SequenceId),
        SequenceName.FuturesItiTrendDeltaData_SequenceId => nameof(SequenceName.FuturesItiTrendDeltaData_SequenceId),
        SequenceName.FuturesTradeSignal_SequenceId => nameof(SequenceName.FuturesTradeSignal_SequenceId),
        SequenceName.TelemetryLog_SequenceId => nameof(SequenceName.TelemetryLog_SequenceId),
        SequenceName.SpreadDistribution_Id => nameof(SequenceName.SpreadDistribution_Id),
        SequenceName.TradePlan_SequenceId => nameof(SequenceName.TradePlan_SequenceId),
        SequenceName.OptionTradeSpreadData_SequenceId => nameof(SequenceName.OptionTradeSpreadData_SequenceId),
        SequenceName.OptionQuote_QuoteId => nameof(SequenceName.OptionQuote_QuoteId),
        SequenceName.StreamingRequest_RequestId => nameof(SequenceName.StreamingRequest_RequestId),
        SequenceName.OptionQuoteData_SequenceId => nameof(SequenceName.OptionQuoteData_SequenceId),
        SequenceName.MarketDataFeed_RequestId => nameof(SequenceName.MarketDataFeed_RequestId),
        SequenceName.TradePlacementSignal_SequenceId => nameof(SequenceName.TradePlacementSignal_SequenceId),
        SequenceName.FundTransaction_TransactionId => nameof(SequenceName.FundTransaction_TransactionId),
        SequenceName.FuturesIntraDay_SequenceId => nameof(SequenceName.FuturesIntraDay_SequenceId),
        _ => value.ToString()
    };
}
