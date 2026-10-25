namespace TomasAI.IFM.Shared.Identity;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1707", Justification = "These names are persisted PostgreSQL sequence identifiers and must remain stable.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1700", Justification = "Reserved numeric members retain their historical sequence identities and prevent reassignment.")]
public enum SequenceName
{
    FuturesTickData_TickId,
    FuturesOptionTickData_TickId,
    FuturesItiSignal_SequenceId,
    // Retired predictive-data slots preserve the numeric identities of later sequence names.
    ReservedPredictiveClassData_SequenceId,
    ReservedPredictiveDeltaData_SequenceId,
    FuturesTradeSignal_SequenceId,
    SpreadDistribution_Id,
    TradePlan_SequenceId,
    OptionTradeSpreadData_SequenceId,
    StreamingRequest_RequestId,
    MarketDataFeed_RequestId,
    TradePlacementSignal_SequenceId,
    FundTransaction_TransactionId,
    FuturesIntraDay_SequenceId,
    FuturesOptionTickPriceData_TickId,
    Fund_FundId,
    Trade_OrderId,
    Trade_TradeId,
    ScheduledJob_JobId,
    Portfolio_PortfolioId,
    PortfolioPolicy_PolicyId,
    Reference_TradeStrategyFamilyId,
    MarketDataService_WatchdogStatusLogId,
    Reference_TradeStrategySymbolId,
    PortfolioLedger_BookId,
    PortfolioLedger_AccountId,
    PortfolioLedger_JournalId,
    PortfolioLedger_TransactionId,
}
