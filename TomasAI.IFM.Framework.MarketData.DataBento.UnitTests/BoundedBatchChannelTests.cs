namespace TomasAI.IFM.Framework.MarketData.DataBento.UnitTests;

public sealed class BoundedBatchChannelTests
{
    [Fact]
    public void ReaderRequiresCurrentLeaseToBeReturned()
    {
        var channel = new BoundedBatchChannel(2, 2);
        PublishOne(channel);
        PublishOne(channel);

        Assert.True(channel.TryRead(out var first));
        Assert.Throws<InvalidOperationException>(() => channel.TryRead(out _));
        first!.Dispose();
        Assert.True(channel.TryRead(out var second));
        second!.Dispose();
    }

    [Fact]
    public async Task FullChannelBlocksWriterAndResumesWithoutDropping()
    {
        var channel = new BoundedBatchChannel(2, 1);
        PublishOne(channel);
        PublishOne(channel);
        var third = channel.RentBatch(static () => false);

        var publish = Task.Run(() => channel.Publish(third, static () => false));
        await Task.Delay(50);
        Assert.False(publish.IsCompleted);

        using (channel.Read(TimeSpan.FromSeconds(1)))
        {
        }
        Assert.True(await publish.WaitAsync(TimeSpan.FromSeconds(2)));

        using var second = channel.Read(TimeSpan.FromSeconds(1));
        second.Dispose();
        using var final = channel.Read(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void CompletionDeliversQueuedBatchesBeforeEndOfStream()
    {
        var channel = new BoundedBatchChannel(1, 1);
        PublishOne(channel);
        channel.Complete();

        using (channel.Read(TimeSpan.FromSeconds(1)))
        {
        }
        Assert.Throws<EndOfStreamException>(() =>
            channel.Read(TimeSpan.FromMilliseconds(10)));
    }

    [Fact]
    public void EmptyReadTimesOutWithoutConsumingFutureBatch()
    {
        var channel = new BoundedBatchChannel(1, 1);
        Assert.Throws<TimeoutException>(() =>
            channel.Read(TimeSpan.FromMilliseconds(10)));

        PublishOne(channel);
        using var batch = channel.Read(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Multiplexed_reader_waits_for_channel_signal_and_wakes_on_publish()
    {
        using var ready = new SemaphoreSlim(0);
        var channel = new BoundedBatchChannel(1, 1, () => ready.Release());
        using var reader = new MultiplexedTickerBatchReader(
            [(new InstrumentKey(7, 42), (ISynchronousBatchReader<MarketDataBatch64>)channel)],
            static () => { },
            ready);

        var read = Task.Run(() => reader.Read(TimeSpan.FromSeconds(2)));
        PublishOne(channel);

        using var batch = await read.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(new InstrumentKey(7, 42), batch.Instrument);
    }

    private static void PublishOne(BoundedBatchChannel channel)
    {
        var batch = channel.RentBatch(static () => false);
        Assert.True(channel.Publish(batch, static () => false));
    }
}
