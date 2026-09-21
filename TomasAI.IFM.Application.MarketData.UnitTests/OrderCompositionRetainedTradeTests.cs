using System.Collections.Concurrent;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Framework.MarketData.DataBento;
using static TomasAI.IFM.Application.MarketData.UnitTests.OrderCompositionPricingPrerequisiteTests;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed partial class OrderCompositionWorkerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_chain_trades_when_out_of_order_or_pricing_fails_then_every_source_is_retained(bool invalidPrice)
    {
        using var prices = Prices();
        using var feed = new ChainFeed();
        var writer = new RetainedTrades();
        await using var runtime = Runtime(Factory(feed), prices, tradeEvidence: writer);
        Assert.True((await runtime.AcquireAsync(Request(), default)).Active);
        var ns = checked((At.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks) * 100) + 23;
        // No option quote is needed: IV and full Greeks use the source trade, never cached quote Greeks.
        foreach (uint sequence in new uint[] { 3, 2, 1 })
            feed.Push(new MarketRecord64(new TradeRecord64(
                new(10, 1, MarketRecordKind.Trade, 0, ns, ns + 17, sequence),
                invalidPrice ? -1_000_000_000 : 110_000_000_000, 2, 0, 0, 0)));
        await Until(() => writer.Rows.Count == 3);
        var rows = writer.Rows.ToArray();
        Assert.Equal(new long[] { 3, 2, 1 }, rows.Select(x => x.Source.Sequence));
        Assert.All(rows, row =>
        {
            row.Validate();
            Assert.Equal(ns, row.Source.EventNanoseconds);
            Assert.Equal(ns + 17, row.Source.ReceiveNanoseconds);
            Assert.Equal(invalidPrice ? -1m : 110m, row.Source.Price);
            if (invalidPrice) { Assert.Null(row.Greeks); Assert.NotNull(row.Failure); }
            else { Assert.NotNull(row.Greeks); Assert.Null(row.Failure); Assert.True(row.Greeks.ImpliedVolatility > 0); }
        });
    }

    sealed class RetainedTrades : IOptionTradeEvidenceWriter
    {
        public readonly ConcurrentQueue<OptionTradeEvidence> Rows = new();
        public ValueTask WriteAsync(OptionTradeEvidence evidence, CancellationToken token)
        { token.ThrowIfCancellationRequested(); evidence.Validate(); Rows.Enqueue(evidence); return ValueTask.CompletedTask; }
    }
}
