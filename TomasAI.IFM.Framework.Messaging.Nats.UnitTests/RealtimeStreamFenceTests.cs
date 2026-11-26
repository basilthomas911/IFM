using TomasAI.IFM.Framework.Messaging.NatsJetStream.RealtimeMailboxes;

namespace TomasAI.IFM.Framework.Messaging.Nats.UnitTests;

public sealed class RealtimeStreamFenceTests
{
    [Fact]
    public async Task Concurrent_readers_share_resource_and_writer_waits_for_every_reader()
    {
        var fence = new RealtimeStreamFence();
        var first = await fence.EnterReadAsync();
        var second = await fence.EnterReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
        var writer = fence.EnterWriteAsync().AsTask(); Assert.False(writer.IsCompleted);
        await first.DisposeAsync(); Assert.False(writer.IsCompleted);
        await second.DisposeAsync(); await using var exclusive = await writer.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Late_reader_cannot_pass_a_waiting_writer()
    {
        var fence = new RealtimeStreamFence(); var original = await fence.EnterReadAsync();
        var writer = fence.EnterWriteAsync().AsTask();
        var late = fence.EnterReadAsync().AsTask(); Assert.False(writer.IsCompleted); Assert.False(late.IsCompleted);
        await original.DisposeAsync(); var exclusive = await writer.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(late.IsCompleted); await exclusive.DisposeAsync();
        await using var last = await late.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Cancelled_waiting_writer_releases_entry_for_readers_and_subsequent_writer()
    {
        var fence = new RealtimeStreamFence(); var original = await fence.EnterReadAsync();
        using var cancellation = new CancellationTokenSource();
        var writer = fence.EnterWriteAsync(cancellation.Token).AsTask(); var late = fence.EnterReadAsync().AsTask();
        cancellation.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => writer);
        var reader = await late.WaitAsync(TimeSpan.FromSeconds(2));
        await original.DisposeAsync(); await reader.DisposeAsync();
        await using var recovered = await fence.EnterWriteAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Cancelled_reader_and_writer_queued_at_entry_leave_fence_usable()
    {
        var fence = new RealtimeStreamFence(); var owner = await fence.EnterWriteAsync();
        using var readCancellation = new CancellationTokenSource(); using var writeCancellation = new CancellationTokenSource();
        var reader = fence.EnterReadAsync(readCancellation.Token).AsTask();
        var writer = fence.EnterWriteAsync(writeCancellation.Token).AsTask();
        readCancellation.Cancel(); writeCancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => writer);
        await owner.DisposeAsync();
        await using var recovered = await fence.EnterReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Fault_inside_operation_releases_lease_and_double_disposal_is_safe(bool write)
    {
        var fence = new RealtimeStreamFence(); IAsyncDisposable? lease = null;
        await Assert.ThrowsAsync<IOException>(async () =>
        {
            await using var acquired = write ? await fence.EnterWriteAsync() : await fence.EnterReadAsync();
            lease = acquired; throw new IOException("Bounded operation failed.");
        });
        Assert.NotNull(lease);
        await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => lease.DisposeAsync().AsTask()));
        await using var recovered = await fence.EnterWriteAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Already_cancelled_acquisition_does_not_take_an_available_permit()
    {
        var fence = new RealtimeStreamFence(); using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fence.EnterReadAsync(cancellation.Token).AsTask());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fence.EnterWriteAsync(cancellation.Token).AsTask());
        await using var acquired = await fence.EnterWriteAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
    }
}
