using TomasAI.IFM.Application.MarketData.Rust.Delivery;
using TomasAI.IFM.Shared.MarketData.Contracts.LastPrice;
namespace TomasAI.IFM.Application.MarketData.Rust.Api;

/// <summary>Stable query-only reader for the complete native owner. Reads resolve
/// the current Rust data view; no provider lease, preparation callback, renewal
/// timer or local time source survives in this managed object.</summary>
public sealed class NativeOwnedFuturesPriceReader : IFuturesLastPriceReader
{
    readonly NativeMarketDataRuntime runtime;
    readonly MarketDataLane lane;
    /// <inheritdoc/>
    public string FuturesContractId { get; }
    /// <inheritdoc/>
    public DateOnly ValueDate { get; }
    /// <summary>Binds explicit API identity only. This starts no subscription.
    /// Session disposal retires the native handle even if a caller retains this reader.</summary>
    public NativeOwnedFuturesPriceReader(NativeMarketDataRuntime runtime, MarketDataLane lane, string contractId, DateOnly valueDate)
    {
        ArgumentNullException.ThrowIfNull(runtime); ArgumentException.ThrowIfNullOrWhiteSpace(contractId);
        if (lane is not (MarketDataLane.FuturesGlbx or MarketDataLane.FuturesXcbf)) throw new ArgumentOutOfRangeException(nameof(lane));
        this.runtime = runtime; this.lane = lane; FuturesContractId = contractId; ValueDate = valueDate;
    }
    /// <inheritdoc/>
    public bool TryGetLastTrade(out LastTradeTickSnapshot snapshot)
    {
        try
        {
            if (runtime.ReadDatasetOwnerPrices(lane,FuturesContractId,ValueDate)?.Trade is {} trade) { snapshot=trade.ToSnapshot(); return true; }
        }
        catch (ObjectDisposedException) { }
        snapshot=default; return false;
    }
    /// <inheritdoc/>
    public bool TryGetLastQuote(out LastQuoteTickSnapshot snapshot)
    {
        try
        {
            if (runtime.ReadDatasetOwnerPrices(lane,FuturesContractId,ValueDate)?.Quote is {} quote) { snapshot=quote.ToSnapshot(); return true; }
        }
        catch (ObjectDisposedException) { }
        snapshot=default; return false;
    }
}
