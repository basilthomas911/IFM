using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;
using TomasAI.IFM.Framework.MarketData.TickAggregation;

namespace TomasAI.IFM.Framework.MarketData.UnitTests;

public sealed class BoundedTickLiveEventPublisherTests
{
    [Fact]
    public async Task Dispose_drains_accepted_transient_events_in_order()
    {
        var sink = new CapturingSink();
        var publisher = new BoundedTickLiveEventPublisher(sink, 2);
        var date = new DateOnly(2026, 8, 10);
        await publisher.PublishAsync(new LiveTickQuoteServiceEvent(
            Guid.NewGuid(), "ESU6", date, AssetTypeId.Futures,
            "GLBX.MDP3", date, 7, 42,
            new FuturesTickQuoteData(1, 1, 1, 0, 10, 10m, 1, 1, 11, 11m, 1, 1)));
        await publisher.PublishAsync(new LiveTickTradeServiceEvent(
            Guid.NewGuid(), "ESU6", date, AssetTypeId.Futures,
            "GLBX.MDP3", date, 7, 42,
            new FuturesTickTradeData(2, 2, 2, 0, 11, 11m, 1, 1, 1, 0)));

        await publisher.DisposeAsync();

        Assert.Equal(["quote:1", "trade:2"], sink.Observed);
    }

    private sealed class CapturingSink : ITickLiveEventSink
    {
        internal List<string> Observed { get; } = [];
        public ValueTask OnQuoteAsync(LiveTickQuoteServiceEvent @event)
        {
            Observed.Add($"quote:{@event.Quote.SourceSequence}");
            return ValueTask.CompletedTask;
        }
        public ValueTask OnTradeAsync(LiveTickTradeServiceEvent @event)
        {
            Observed.Add($"trade:{@event.Trade.SourceSequence}");
            return ValueTask.CompletedTask;
        }
    }
}
