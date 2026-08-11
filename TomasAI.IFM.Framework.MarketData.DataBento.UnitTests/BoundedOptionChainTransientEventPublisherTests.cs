using TomasAI.IFM.Framework.MarketData.DataBento.OptionChain;

namespace TomasAI.IFM.Framework.MarketData.DataBento.UnitTests;

public sealed class BoundedOptionChainTransientEventPublisherTests
{
    [Fact]
    public async Task Dispose_drains_quote_and_trade_without_persistence()
    {
        var sink = new CapturingSink();
        var publisher = new BoundedOptionChainTransientEventPublisher(sink, 2);
        var date = new DateOnly(2026, 8, 10);
        var maturity = new DateOnly(2026, 9, 18);
        await publisher.PublishAsync(new FuturesOptionChainQuoteChangedServiceEvent(
            Guid.NewGuid(), "ESU6", "ESU6 C6500", date, maturity, default, default));
        await publisher.PublishAsync(new FuturesOptionChainTradeChangedServiceEvent(
            Guid.NewGuid(), "ESU6", "ESU6 C6500", date, maturity, default, default));

        await publisher.DisposeAsync();

        Assert.Equal(["quote", "trade"], sink.Observed);
    }

    private sealed class CapturingSink : IOptionChainTransientEventSink
    {
        internal List<string> Observed { get; } = [];
        public ValueTask OnQuoteAsync(FuturesOptionChainQuoteChangedServiceEvent @event)
        {
            Observed.Add("quote");
            return ValueTask.CompletedTask;
        }
        public ValueTask OnTradeAsync(FuturesOptionChainTradeChangedServiceEvent @event)
        {
            Observed.Add("trade");
            return ValueTask.CompletedTask;
        }
    }
}
