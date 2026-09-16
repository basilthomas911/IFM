using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.MarketData.TickAggregation;

namespace TomasAI.IFM.Framework.MarketData.UnitTests;

public sealed class TickQuoteBufferPoolTests
{
    [Fact]
    public async Task Fixed_slot_is_exact_size_and_waits_until_its_previous_owner_returns_it()
    {
        using var pool = new TickQuoteBufferPool([(512, 1)]);
        var first = await pool.RentAsync(512);
        var buffer = first.Buffer;
        Assert.Equal(512, buffer.Length);
        buffer[0] = new FuturesTickQuoteData(1, 2, 3, 4, 5, 5m, 6, 7, 8, 6m, 9, 10);
        first.SetCount(1);

        var waiting = pool.RentAsync(512).AsTask();
        Assert.False(waiting.IsCompleted);
        first.Dispose();

        var second = await waiting.WaitAsync(TimeSpan.FromSeconds(2));
        try
        {
            Assert.Same(buffer, second.Buffer);
            Assert.Equal(default(FuturesTickQuoteData), second.Buffer[0]);
        }
        finally { second.Dispose(); }
    }

    [Fact]
    public async Task Retired_generation_does_not_rent_its_slots_again()
    {
        var pool = new TickQuoteBufferPool([(512, 1)]);
        var active = await pool.RentAsync(512);
        pool.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await pool.RentAsync(512));
        active.Dispose();
    }

    [Fact]
    public async Task Cancelled_waiter_does_not_consume_the_slot_when_it_returns()
    {
        using var pool = new TickQuoteBufferPool([(512, 1)]);
        var active = await pool.RentAsync(512);
        using var cancellation = new CancellationTokenSource();
        var waiting = pool.RentAsync(512, cancellation.Token).AsTask();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        active.Dispose();
        using var next = await pool.RentAsync(512);
        Assert.Equal(512, next.Buffer.Length);
    }

    [Fact]
    public void Excessive_reservation_is_rejected_before_allocating_quote_arrays()
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TickQuoteBufferPool([(4096, 2048)]));
        Assert.Contains("512 MiB", error.Message);
    }
}
