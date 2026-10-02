using TomasAI.IFM.Application.MarketData.Databento.Resiliency;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class DatabentoRecoveryHoldingBufferTests
{
    [Fact]
    public void Capacity_drops_excess_without_growing_memory()
    {
        var buffer = new DatabentoRecoveryHoldingBuffer<int>(2);
        Assert.True(buffer.TryHold(1));
        Assert.True(buffer.TryHold(2));
        Assert.False(buffer.TryHold(3));
        Assert.Equal(new(2, 2, 2, 1), buffer.Capture());
        Assert.True(buffer.TryTake(out var first));
        Assert.Equal(1, first);
        Assert.True(buffer.TryHold(4));
        Assert.Equal(new(2, 2, 3, 1), buffer.Capture());
    }

    [Fact]
    public void Concurrent_producers_never_exceed_fixed_capacity()
    {
        const int capacity = 64;
        const int attempts = 10_000;
        var buffer = new DatabentoRecoveryHoldingBuffer<int>(capacity);

        Parallel.For(0, attempts, item => buffer.TryHold(item));

        var snapshot = buffer.Capture();
        Assert.Equal(capacity, snapshot.Count);
        Assert.Equal(capacity, snapshot.Accepted);
        Assert.Equal(attempts - capacity, snapshot.Dropped);
        for (var i = 0; i < capacity; i++) Assert.True(buffer.TryTake(out _));
        Assert.False(buffer.TryTake(out _));
    }
}
