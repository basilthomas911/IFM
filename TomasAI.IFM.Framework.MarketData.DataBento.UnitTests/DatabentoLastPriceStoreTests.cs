using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.MarketData.Contracts.LastPrice;
using TomasAI.IFM.Framework.MarketData.DataBento.LastPrice;

namespace TomasAI.IFM.Framework.MarketData.DataBento.UnitTests;

public sealed class DatabentoLastPriceStoreTests
{
    private static readonly DateOnly ValueDate = new(2026, 8, 10);
    private static readonly DateTimeOffset Timestamp =
        new(2026, 8, 10, 14, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Readers_are_stable_non_consuming_and_kind_checked()
    {
        using var store = new DatabentoLastPriceStore(ValueDate, 2);
        store.RegisterContract("ESU6", AssetTypeId.Futures);
        store.RegisterContract("ESU6 C5000", AssetTypeId.FuturesOption);

        var first = store.GetFuturesReader("ESU6", ValueDate);
        var second = store.GetFuturesReader("ESU6", ValueDate);
        Assert.Same(first, second);
        Assert.False(first.TryGetLastTrade(out _));
        Assert.Throws<InvalidOperationException>(() =>
            store.GetFuturesReader("ESU6 C5000", ValueDate));
        Assert.Throws<InvalidOperationException>(() =>
            store.GetFuturesOptionReader("ESU6", ValueDate));
    }

    [Fact]
    public void Newer_raw_quote_clears_an_older_enriched_pair()
    {
        using var store = CreateOptionStore();
        var reader = store.GetFuturesOptionReader("ESU6 C5000", ValueDate);
        var first = Quote(1, 10m, 12m);
        var enriched = new LastQuoteTickWithGreeksSnapshot(first, Greeks(1));

        Assert.True(store.TryUpdateQuoteWithGreeks(enriched));
        Assert.True(reader.TryGetLastQuoteWithGreeks(out var observed));
        Assert.Equal(1, observed.Tick.SourceSequence);

        Assert.True(store.TryUpdateQuote(Quote(2, 11m, 13m)));
        Assert.True(reader.TryGetLastQuote(out var raw));
        Assert.Equal(2, raw.SourceSequence);
        Assert.False(reader.TryGetLastQuoteWithGreeks(out _));
    }

    [Fact]
    public void Older_source_sequence_cannot_replace_a_newer_value()
    {
        using var store = CreateOptionStore();
        var reader = store.GetFuturesOptionReader("ESU6 C5000", ValueDate);

        Assert.True(store.TryUpdateQuote(Quote(20, 20m, 22m)));
        Assert.False(store.TryUpdateQuote(Quote(19, 1m, 2m)));
        Assert.True(reader.TryGetLastQuote(out var observed));
        Assert.Equal(20m, observed.BidPrice);
        Assert.Equal(20, observed.SourceSequence);
    }

    [Fact]
    public void Invalid_enriched_result_is_available_and_keeps_typed_reason()
    {
        using var store = CreateOptionStore();
        var tick = Quote(1, null, 12m);
        var invalid = Greeks(1) with
        {
            IsValid = false,
            FailureReason = OptionGreeksFailureReason.NoValidQuote,
            OptionMarkPrice = null,
            Delta = null
        };

        Assert.True(store.TryUpdateQuoteWithGreeks(
            new LastQuoteTickWithGreeksSnapshot(tick, invalid)));

        var reader = store.GetFuturesOptionReader("ESU6 C5000", ValueDate);
        Assert.True(reader.TryGetLastQuoteWithGreeks(out var observed));
        Assert.False(observed.Greeks.IsValid);
        Assert.Equal(OptionGreeksFailureReason.NoValidQuote, observed.Greeks.FailureReason);
        Assert.Null(observed.Greeks.Delta);
    }

    [Fact]
    public void Invalidation_makes_existing_handles_return_false()
    {
        using var store = CreateOptionStore();
        var reader = store.GetFuturesOptionReader("ESU6 C5000", ValueDate);
        Assert.True(store.TryUpdateQuote(Quote(1, 10m, 12m)));
        Assert.True(reader.TryGetLastQuote(out _));

        store.Invalidate();

        Assert.False(reader.TryGetLastQuote(out _));
        Assert.False(reader.TryGetLastTrade(out _));
        Assert.False(reader.TryGetLastQuoteWithGreeks(out _));
        Assert.False(reader.TryGetLastTradeWithGreeks(out _));
    }

    [Fact]
    public void Dataset_reset_clears_only_selected_values_and_preserves_reader_handles()
    {
        using var store = new DatabentoLastPriceStore(ValueDate, 2);
        store.RegisterContract("ESU6 C5000", AssetTypeId.FuturesOption);
        store.RegisterContract("VXU6 C20", AssetTypeId.FuturesOption);
        var es = store.GetFuturesOptionReader("ESU6 C5000", ValueDate);
        var vx = store.GetFuturesOptionReader("VXU6 C20", ValueDate);
        Assert.True(store.TryUpdateQuote(Quote("ESU6 C5000", 10, 10m, 12m)));
        Assert.True(store.TryUpdateQuote(Quote("VXU6 C20", 20, 20m, 22m)));

        store.ResetContracts(["ESU6 C5000"]);

        Assert.Same(es, store.GetFuturesOptionReader("ESU6 C5000", ValueDate));
        Assert.False(es.TryGetLastQuote(out _));
        Assert.True(vx.TryGetLastQuote(out var unaffected));
        Assert.Equal(20, unaffected.SourceSequence);
        Assert.True(store.TryUpdateQuote(Quote("ESU6 C5000", 1, 11m, 13m)));
        Assert.True(es.TryGetLastQuote(out var restarted));
        Assert.Equal(1, restarted.SourceSequence);
    }

    [Fact]
    public void Capacity_is_bounded_and_registration_is_idempotent()
    {
        using var store = new DatabentoLastPriceStore(ValueDate, 1);
        store.RegisterContract("ESU6", AssetTypeId.Futures);
        store.RegisterContract("ESU6", AssetTypeId.Futures);
        Assert.Equal(1, store.Count);

        Assert.Throws<InvalidOperationException>(() =>
            store.RegisterContract("NQU6", AssetTypeId.Futures));
    }

    [Fact]
    public async Task Concurrent_reads_never_observe_a_torn_quote()
    {
        using var store = CreateOptionStore();
        var reader = store.GetFuturesOptionReader("ESU6 C5000", ValueDate);
        const int observations = 20_000;
        var failure = 0;

        var writer = Task.Run(() =>
        {
            for (var sequence = 1; sequence <= observations; sequence++)
                store.TryUpdateQuote(Quote(sequence, sequence, sequence + 1m));
        });
        var consumer = Task.Run(() =>
        {
            while (!writer.IsCompleted)
            {
                if (reader.TryGetLastQuote(out var quote)
                    && quote.AskPrice != quote.BidPrice + 1m)
                    Interlocked.Exchange(ref failure, 1);
            }
        });

        await Task.WhenAll(writer, consumer);
        Assert.Equal(0, failure);
        Assert.True(reader.TryGetLastQuote(out var final));
        Assert.Equal(observations, final.SourceSequence);
    }

    private static DatabentoLastPriceStore CreateOptionStore()
    {
        var store = new DatabentoLastPriceStore(ValueDate, 1);
        store.RegisterContract("ESU6 C5000", AssetTypeId.FuturesOption);
        return store;
    }

    private static LastQuoteTickSnapshot Quote(long sequence, decimal? bid, decimal? ask) =>
        Quote("ESU6 C5000", sequence, bid, ask);

    private static LastQuoteTickSnapshot Quote(
        string contractId,
        long sequence,
        decimal? bid,
        decimal? ask) =>
        new(contractId, ValueDate, bid, 10, 1, ask, 11, 1,
            sequence, Timestamp.AddTicks(sequence), Timestamp.AddTicks(sequence + 1));

    private static OptionGreeksSnapshot Greeks(long optionSequence) => new(
        true, false, OptionGreeksFailureReason.None,
        OptionGreeksPriceSource.QuoteMidpoint, "ESU6", 5_100m, 11m,
        0.04, 30d / 365d, 0.2, 11d, 0.5, 0.01, 1.2, -0.3, 0.4,
        4, 100, optionSequence, Timestamp, Timestamp, Timestamp);
}
