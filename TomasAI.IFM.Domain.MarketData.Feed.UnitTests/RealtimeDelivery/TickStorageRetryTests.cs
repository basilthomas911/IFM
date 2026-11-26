using MessagePack;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.TestSupport;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.RealtimeDelivery;

[Collection("MarketPriceDeliverySafety")]
public sealed class TickStorageRetryTests
{
    public static IEnumerable<object[]> Cases => new[] { "Trade", "Quote" }.SelectMany(kind => new[] { false, true }.SelectMany(metrics => new[] { "refused", "throw", "lost-reply" }.Select(failure => new object[] { kind, metrics, failure })));
    [Theory, MemberData(nameof(Cases))]
    public async Task Fresh_decoded_retries_preserve_tick_identity_values_and_order(string kind, bool metrics, string failure)
    {
        await using var fixture = new TickStorageDeliveryFixture(metrics) { Failure = failure }; await fixture.Start();
        var bytes = TickStorageDeliveryFixture.Encode(TickStorageDeliveryFixture.Input(kind, 1));
        Assert.False(await fixture.Deliver(TickStorageDeliveryFixture.Decode(kind, bytes)));
        fixture.Failure = "none";
        Assert.True(await fixture.Deliver(TickStorageDeliveryFixture.Decode(kind, bytes)));
        Assert.True(await fixture.Deliver(TickStorageDeliveryFixture.Decode(kind, TickStorageDeliveryFixture.Encode(TickStorageDeliveryFixture.Input(kind, 2)))));
        Assert.Equal(new long[] { 1, 1, 2 }, fixture.Attempts);
        Assert.All(fixture.SourceGenerations, value => Assert.Equal(TickStorageDeliveryFixture.Generation, value));
        foreach (var payload in fixture.Payloads)
        {
            if (kind == "Trade")
            {
                var stored = MessagePackSerializer.Deserialize<FuturesTickTradeDataInsertedEvent>(payload);
                Assert.Equal(5450.25m, stored.TradeData.Price);
            }
            else
            {
                var stored = MessagePackSerializer.Deserialize<FuturesTickQuoteDataInsertedEvent>(payload);
                try { Assert.Equal(2, stored.QuoteData.Count); Assert.Equal(5450, stored.QuoteData.Items[0].BidPrice); Assert.Equal(5450.5m, stored.QuoteData.Items[1].AskPrice);  }
                finally { stored.QuoteData.Dispose(); }
            }
        }
    }
    [Fact]
    public async Task Failed_quote_returns_lease_once_and_retained_bytes_rebuild_an_uncorrupted_retry()
    {
        await using var fixture = new TickStorageDeliveryFixture(false) { Failure = "refused" }; await fixture.Start();
        var bytes = TickStorageDeliveryFixture.Encode(TickStorageDeliveryFixture.Input("Quote", 1));
        var original = (FuturesTickQuoteDataChangedEvent)TickStorageDeliveryFixture.Decode("Quote", bytes);
        Assert.False(await fixture.Deliver(original)); Assert.Equal(default, original.QuoteData.Buffer[0]);
        var retry = (FuturesTickQuoteDataChangedEvent)TickStorageDeliveryFixture.Decode("Quote", bytes);
        Assert.Same(original.QuoteData.Buffer, retry.QuoteData.Buffer);
        original.QuoteData.Dispose(); // A stale second Dispose must not clear a newly leased retry.
        Assert.Equal(5450, retry.QuoteData.Items[0].BidPrice);
        fixture.Failure = "none"; Assert.True(await fixture.Deliver(retry)); Assert.Equal(default, retry.QuoteData.Buffer[0]);
    }
    [Theory]
    [InlineData("Trade")] [InlineData("Quote")]
    public async Task Processing_remains_unacknowledged_and_quote_memory_remains_valid_until_completion(string kind)
    {
        await using var fixture = new TickStorageDeliveryFixture(false) { Pause = new(TaskCreationOptions.RunContinuationsAsynchronously) }; await fixture.Start();
        var source = TickStorageDeliveryFixture.Decode(kind, TickStorageDeliveryFixture.Encode(TickStorageDeliveryFixture.Input(kind, 1)));
        var work = fixture.Deliver(source); await fixture.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); Assert.False(work.IsCompleted);
        if (source is FuturesTickQuoteDataChangedEvent quote) Assert.Equal(5450, quote.QuoteData.Items[0].BidPrice);
        fixture.Pause.TrySetResult(); Assert.True(await work);
    }
}
