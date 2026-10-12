using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;

namespace TomasAI.IFM.Application.MarketData.Contracts;

/// <summary>One ticker or one exact chain; callers cannot supply both or neither.</summary>
public sealed record SubscriptionTarget
{
    public SubscriptionTickerKey? Ticker { get; }
    public SubscriptionTickerKey? Underlying { get; }
    public SubscriptionChainKey? Chain { get; }
    public string Dataset => Ticker?.Dataset ?? Chain!.Underlying.Dataset;
    public SubscriptionTarget(SubscriptionTickerKey ticker, SubscriptionTickerKey? underlying = null)
    {
        ArgumentNullException.ThrowIfNull(ticker);
        if (ticker.AssetKind == SubscriptionAssetKind.FuturesOption
            ? underlying is null || underlying.AssetKind != SubscriptionAssetKind.Futures
                || !string.Equals(underlying.ProviderScope,ticker.ProviderScope,StringComparison.Ordinal) || !string.Equals(underlying.Dataset,ticker.Dataset,StringComparison.Ordinal)
                || !string.Equals(underlying.Schema,ticker.Schema,StringComparison.Ordinal)
            : underlying is not null)
            throw new ArgumentException("An option requires its resolved futures dependency; an outright has none.",nameof(underlying));
        Ticker = ticker;
        Underlying = underlying;
    }
    public SubscriptionTarget(SubscriptionChainKey chain) => Chain = chain ?? throw new ArgumentNullException(nameof(chain));
}
