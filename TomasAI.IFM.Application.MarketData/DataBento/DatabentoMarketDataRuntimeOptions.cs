using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.MarketData.DataBento;

namespace TomasAI.IFM.Application.MarketData.Databento;

public sealed record DatabentoContractRegistration
{
    public required string DomainContractId { get; init; }
    public required string ProviderContractName { get; init; }
    public required AssetTypeId AssetTypeId { get; init; }
}
public sealed record DatabentoMarketDataRuntimeOptions
{
    public required DatabentoFeedOptions FeedOptions { get; init; }
    public required IReadOnlyList<DatabentoContractRegistration> Contracts { get; init; }
    public int QueryConcurrency { get; init; } = 2;
    public int QueryQueueCapacity { get; init; } = 128;
    public int LastPriceCapacity { get; init; } = 4096;
    public int MaximumConcurrentOptionChains { get; init; } = 8;
    public TimeSpan ProviderQueryTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan FeedStartTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan FeedStopTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan ReaderPollTimeout { get; init; } = TimeSpan.FromMilliseconds(50);
}
