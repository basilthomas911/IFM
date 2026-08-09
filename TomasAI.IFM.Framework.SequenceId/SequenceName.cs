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
    Fund_FundId,
    Trade_OrderId,
    Trade_TradeId,
    ScheduledJob_JobId,
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
        SequenceName.FuturesOptionTickPriceData_TickId => nameof(SequenceName.FuturesOptionTickPriceData_TickId),
        SequenceName.Fund_FundId => nameof(SequenceName.Fund_FundId),
        SequenceName.Trade_OrderId => nameof(SequenceName.Trade_OrderId),
        SequenceName.Trade_TradeId => nameof(SequenceName.Trade_TradeId),
        SequenceName.ScheduledJob_JobId => nameof(SequenceName.ScheduledJob_JobId),
        _ => value.ToString()
    };

    /// <summary>
    /// Maps the legacy Reference API seed names to their strongly typed PostgreSQL sequences.
    /// Exact <see cref="SequenceName"/> values are also accepted for forward compatibility.
    /// </summary>
    public static SequenceName ParseSequenceName(string seedType)
    {
        if (string.IsNullOrWhiteSpace(seedType))
            throw new ArgumentException("A sequence name is required.", nameof(seedType));

        return seedType switch
        {
            "FundId" => SequenceName.Fund_FundId,
            "OrderId" => SequenceName.Trade_OrderId,
            "TradeId" => SequenceName.Trade_TradeId,
            "ScheduledJobId" => SequenceName.ScheduledJob_JobId,
            _ when Enum.TryParse<SequenceName>(seedType, ignoreCase: false, out var sequenceName)
                => sequenceName,
            _ => throw new ArgumentOutOfRangeException(
                nameof(seedType),
                seedType,
                "The seed type is not a registered PostgreSQL sequence.")
        };
    }
}
