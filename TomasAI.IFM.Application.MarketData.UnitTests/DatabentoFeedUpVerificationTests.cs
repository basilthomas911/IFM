using TomasAI.IFM.Application.MarketData.UnitTests.Harness;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class DatabentoFeedUpVerificationTests
{
    [Fact]
    public void No_active_epoch_is_down()
    {
        var context = new MarketDataApiTestContext();

        Assert.False(context.Api.IsDatabentoFeedUp());
    }

    [Fact]
    public async Task Running_epoch_and_aggregation_are_up_even_without_recent_records()
    {
        var context = new MarketDataApiTestContext();
        await context.StartAsync();

        Assert.True(context.Api.IsDatabentoFeedUp());
    }

    [Fact]
    public async Task Any_stopped_managed_aggregation_makes_the_single_probe_down()
    {
        var context = new MarketDataApiTestContext();
        await context.StartAsync();
        context.Epoch.TickAggregation.ServiceRunning = false;

        Assert.False(context.Api.IsDatabentoFeedUp());
    }

    [Fact]
    public async Task Stopped_epoch_and_invalid_timeouts_are_down_without_exceptions()
    {
        var context = new MarketDataApiTestContext();
        await context.StartAsync();

        Assert.False(context.Api.IsDatabentoFeedUp(TimeSpan.Zero));
        Assert.False(context.Api.IsDatabentoFeedUp(TimeSpan.FromTicks(-1)));

        await context.Api.StopAsync(MarketDataApiTestContext.ValueDate);

        Assert.False(context.Api.IsDatabentoFeedUp(TimeSpan.FromSeconds(1)));
    }
}
