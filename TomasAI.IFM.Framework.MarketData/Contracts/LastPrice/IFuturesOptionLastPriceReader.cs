namespace TomasAI.IFM.Framework.MarketData.Contracts.LastPrice;

/// <summary>
/// Provides lock-free, non-consuming access to the latest futures-option trade
/// and quote snapshots held by the active market-data provider.
/// </summary>
public interface IFuturesOptionLastPriceReader
{
    string FuturesOptionContractId { get; }
    DateOnly ValueDate { get; }

    bool TryGetLastTrade(out LastTradeTickSnapshot snapshot);
    bool TryGetLastQuote(out LastQuoteTickSnapshot snapshot);

    /// <summary>
    /// Gets the latest option trade and the quote-derived Greeks state that was
    /// current when the trade was processed.
    /// </summary>
    /// <remarks>
    /// A <see langword="true"/> result means the atomic enriched snapshot is
    /// available; callers must inspect <see cref="OptionGreeksSnapshot.IsValid"/>
    /// and <see cref="OptionGreeksSnapshot.FailureReason"/> separately.
    /// </remarks>
    bool TryGetLastTradeWithGreeks(
        out LastTradeTickWithGreeksSnapshot snapshot);

    /// <summary>
    /// Gets the latest option quote and the Greeks calculation produced for
    /// that exact quote observation.
    /// </summary>
    /// <remarks>
    /// A <see langword="true"/> result means the atomic enriched snapshot is
    /// available; callers must inspect <see cref="OptionGreeksSnapshot.IsValid"/>
    /// and <see cref="OptionGreeksSnapshot.FailureReason"/> separately.
    /// </remarks>
    bool TryGetLastQuoteWithGreeks(
        out LastQuoteTickWithGreeksSnapshot snapshot);
}
