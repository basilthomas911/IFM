using TomasAI.IFM.Shared.MarketData.Contracts.Historical;

namespace TomasAI.IFM.Application.MarketData.Databento.Historical;

/// <summary>Configures cost, staging, polling, and bounded decoding for historical acquisition.</summary>
public sealed record DatabentoHistoricalOptions
{
    /// <summary>Gets the configured domain-to-provider profiles.</summary>
    public required IReadOnlyList<DatabentoHistoricalSeriesProfile> SeriesProfiles { get; init; }
    /// <summary>Gets the absolute staging root for verified provider files.</summary>
    public required string StagingRoot { get; init; }
    /// <summary>Gets the maximum decoded records delivered in one sink batch.</summary>
    public int MaximumBatchRecords { get; init; } = 4096;
    /// <summary>Gets the provider job polling interval.</summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(2);
    /// <summary>Gets the maximum provider job wait duration.</summary>
    public TimeSpan JobTimeout { get; init; } = TimeSpan.FromMinutes(20);
}
