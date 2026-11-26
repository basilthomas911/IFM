using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.TestSupport;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.RealtimeDelivery;

[Collection("MarketPriceDeliverySafety")]
public sealed class OptionTradeRetryTests
{
    [Theory]
    [InlineData(false, false)] [InlineData(true, false)] [InlineData(false, true)] [InlineData(true, true)]
    public async Task Failed_input_retries_before_next_input_with_or_without_metrics(bool metrics, bool publicationFailure)
    {
        await using var fixture = new OptionTradeDeliveryFixture(metrics) { FailLookup = !publicationFailure, FailSend = publicationFailure };
        await fixture.Start();
        Assert.False(await fixture.Deliver(OptionTradeDeliveryFixture.Input(1))); Assert.Empty(fixture.Accepted);
        fixture.FailLookup = fixture.FailSend = false;
        Assert.True(await fixture.Deliver(OptionTradeDeliveryFixture.Input(1)));
        Assert.True(await fixture.Deliver(OptionTradeDeliveryFixture.Input(2)));
        Assert.Equal(new long[] { 1, 2 }, fixture.Accepted.Select(e => e.OptionTickData.TickId));
        Assert.Equal(new[] { OptionTradeDeliveryFixture.Input(1).CommandId, OptionTradeDeliveryFixture.Input(2).CommandId }, fixture.Accepted.Select(e => e.CommandId));
        Assert.All(fixture.Accepted, e => Assert.Equal(13.125, e.OptionTickData.OptionPrice));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Pending_lookup_cannot_acknowledge_early(bool metrics)
    {
        await using var fixture = new OptionTradeDeliveryFixture(metrics) { PauseLookup = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        await fixture.Start(); var work = fixture.Deliver(OptionTradeDeliveryFixture.Input(1));
        await fixture.LookupStarted.Task.WaitAsync(TimeSpan.FromSeconds(5)); Assert.False(work.IsCompleted);
        fixture.PauseLookup.TrySetResult(); Assert.True(await work); Assert.Single(fixture.Accepted);
    }
    [Fact]
    public async Task Inactive_and_non_option_inputs_retain_their_intentional_skip_semantics()
    {
        await using var fixture = new OptionTradeDeliveryFixture(false) { Active = false }; await fixture.Start();
        Assert.True(await fixture.Deliver(OptionTradeDeliveryFixture.Input(1)));
        fixture.Active = true;
        Assert.True(await fixture.Deliver(OptionTradeDeliveryFixture.Input(2) with { AssetTypeId = AssetTypeId.Futures }));
        Assert.Empty(fixture.Attempts);
    }
}
