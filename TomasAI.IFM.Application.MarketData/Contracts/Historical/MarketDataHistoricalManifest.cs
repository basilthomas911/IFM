using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Shared.MarketData.Contracts.Historical;

namespace TomasAI.IFM.Application.MarketData.Contracts.Historical;

/// <summary>Reports the immutable acquisition result without embedding provider records.</summary>
public sealed record MarketDataHistoricalManifest
{
    /// <summary>Gets the manifest identity.</summary>
    public required Guid ManifestId { get; init; }
    /// <summary>Gets the data load attempt identity.</summary>
    public required Guid DataLoadAttemptId { get; init; }
    /// <summary>Gets the provider job identity.</summary>
    public required string ProviderJobId { get; init; }
    /// <summary>Gets the request hash.</summary>
    public required string RequestSha256 { get; init; }
    /// <summary>Gets the normalized manifest hash.</summary>
    public required string NormalizedSha256 { get; init; }
    /// <summary>Gets the normalized observation count.</summary>
    public long ObservationCount { get; init; }
    /// <summary>Gets the normalized exact-trade count.</summary>
    public long TradeCount { get; init; }
    /// <summary>Gets the first covered value date.</summary>
    public DateOnly FirstValueDate { get; init; }
    /// <summary>Gets the last covered value date.</summary>
    public DateOnly LastValueDate { get; init; }
    /// <summary>Gets the UTC completion time.</summary>
    public DateTimeOffset CompletedAtUtc { get; init; }
}
