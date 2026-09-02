using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.MarketOutlook;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Processing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.MarketOutlookSnapshot;

internal sealed class MarketOutlookProcessorTestRuntime : IAsyncDisposable
{
    MarketOutlookProcessorTestRuntime()
    {
        Cache = MarketOutlookHotCache.Shared;
        Cache.Clear();
        Metrics = new MarketOutlookProcessorMetrics();
        Channel = new MarketOutlookUpdateChannel(Metrics);
        Publisher = Substitute.For<IMarketOutlookSnapshotPublisher>();
        Processor = new MarketOutlookUpdateProcessor(
            Channel,
            Channel,
            Cache,
            Cache,
            Publisher,
            Metrics,
            Substitute.For<ILogger<MarketOutlookUpdateProcessor>>());
    }

    internal MarketOutlookHotCache Cache { get; }
    internal MarketOutlookProcessorMetrics Metrics { get; }
    internal MarketOutlookUpdateChannel Channel { get; }
    internal IMarketOutlookSnapshotPublisher Publisher { get; }
    internal MarketOutlookUpdateProcessor Processor { get; }

    internal static async ValueTask<MarketOutlookProcessorTestRuntime> StartAsync()
    {
        var runtime = new MarketOutlookProcessorTestRuntime();
        await runtime.Processor.StartAsync(CancellationToken.None);
        return runtime;
    }

    internal async ValueTask DrainAsync()
    {
        if (!await Processor.WaitForIdleAsync(TimeSpan.FromSeconds(5)))
            throw new TimeoutException("Market Outlook test processor did not become idle.");
    }

    public async ValueTask DisposeAsync()
    {
        await Processor.StopAsync(CancellationToken.None);
        Processor.Dispose();
        Cache.Clear();
    }
}
