namespace TomasAI.IFM.Framework.MarketData.Contracts.LastPrice;

/// <summary>
/// A non-consuming, provider-neutral snapshot of the most recently observed
/// trade for one market-data contract.
/// </summary>
public readonly record struct LastTradeTickSnapshot(
    string ContractId,
    DateOnly ValueDate,
    decimal Price,
    uint Size,
    long SourceSequence,
    DateTimeOffset EventTimestamp,
    DateTimeOffset ReceiveTimestamp);

/// <summary>
/// A non-consuming, provider-neutral snapshot of the most recently observed
/// quote for one market-data contract. A missing side remains null.
/// </summary>
public readonly record struct LastQuoteTickSnapshot(
    string ContractId,
    DateOnly ValueDate,
    decimal? BidPrice,
    uint BidSize,
    uint BidCount,
    decimal? AskPrice,
    uint AskSize,
    uint AskCount,
    long SourceSequence,
    DateTimeOffset EventTimestamp,
    DateTimeOffset ReceiveTimestamp)
{
    /// <summary>
    /// Returns a midpoint only for a positive, non-crossed two-sided quote.
    /// </summary>
    public bool TryGetMidpoint(out decimal midpoint)
    {
        if (BidPrice is > 0m
            && AskPrice is > 0m
            && BidPrice <= AskPrice)
        {
            midpoint = BidPrice.Value + ((AskPrice.Value - BidPrice.Value) / 2m);
            return true;
        }

        midpoint = default;
        return false;
    }
}

/// <summary>
/// Provides lock-free, non-consuming access to the latest futures trade and
/// quote snapshots held by the active market-data provider.
/// </summary>
public interface IFuturesLastPriceReader
{
    string FuturesContractId { get; }
    DateOnly ValueDate { get; }

    bool TryGetLastTrade(out LastTradeTickSnapshot snapshot);
    bool TryGetLastQuote(out LastQuoteTickSnapshot snapshot);
}

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
