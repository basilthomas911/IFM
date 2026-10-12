using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

namespace TomasAI.IFM.Application.MarketData.Contracts.Historical;

/// <summary>Provides the durable command/query state for one data load attempt.</summary>
public sealed record HistoricalDataLoaderState
{
    /// <summary>Gets the attempt identity.</summary>
    public required Guid DataLoadAttemptId { get; init; }
    /// <summary>Gets the stable request hash used for repeat-run ownership.</summary>
    public required string RequestSha256 { get; init; }
    /// <summary>Gets the lifecycle status.</summary>
    public HistoricalDataLoaderStatus Status { get; init; }
    /// <summary>Gets the last durable acquisition checkpoint.</summary>
    public required HistoricalAcquisitionCheckpoint Checkpoint { get; init; }
    /// <summary>Gets the immutable manifest after completion.</summary>
    public MarketDataHistoricalManifest? Manifest { get; init; }
    /// <summary>Gets the coverage audit after completion.</summary>
    public HistoricalDataLoaderAudit? Audit { get; init; }
    /// <summary>Gets the terminal failure text without provider data.</summary>
    public string ErrorMessage { get; init; } = string.Empty;
    /// <summary>Gets the last durable update time.</summary>
    public DateTimeOffset UpdatedAtUtc { get; init; }
}
