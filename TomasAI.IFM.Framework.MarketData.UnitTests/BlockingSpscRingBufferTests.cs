using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Framework.MarketData.UnitTests;

public class BlockingSpscRingBufferTests
{
    // ──────────────────────────────────────────────
    // Constructor
    // ──────────────────────────────────────────────

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(16)]
    [InlineData(1024)]
    public void Constructor_PowerOfTwoCapacity_SetsCapacity(int capacity)
    {
        var buffer = new BlockingSpscRingBuffer<TestMessage>(capacity, SampleData.SpinCountEnqueue, SampleData.SpinCountDequeue);

        Assert.Equal(capacity, buffer.Capacity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(9)]
    [InlineData(15)]
    public void Constructor_NonPowerOfTwoCapacity_ThrowsArgumentOutOfRangeException(int capacity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new BlockingSpscRingBuffer<TestMessage>(capacity, SampleData.SpinCountEnqueue, SampleData.SpinCountDequeue));
    }

    [Fact]
    public void Constructor_CountIsZero()
    {
        var buffer = new BlockingSpscRingBuffer<TestMessage>(SampleData.DefaultCapacity, SampleData.SpinCountEnqueue, SampleData.SpinCountDequeue);

        Assert.Equal(0, buffer.Count);
    }

    // ──────────────────────────────────────────────
    // Capacity
    // ──────────────────────────────────────────────

    [Fact]
    public void Capacity_ReturnsConstructorValue()
    {
        var buffer = new BlockingSpscRingBuffer<TestMessage>(8, SampleData.SpinCountEnqueue, SampleData.SpinCountDequeue);

        Assert.Equal(8, buffer.Capacity);
    }

    // ──────────────────────────────────────────────
    // Start
    // ──────────────────────────────────────────────

    [Fact]
    public void Start_InitializesBuffer_AllowsEnqueue()
    {
        using var buffer = SampleData.CreateBuffer();

        buffer.Enqueue(SampleData.Message1);

        Assert.Equal(1, buffer.Count);
    }

    [Fact]
    public void Start_AfterStop_ThrowsObjectDisposedException()
    {
        var buffer = SampleData.CreateBuffer();
        buffer.Stop();

        Assert.Throws<ObjectDisposedException>(() => buffer.Start());
    }

    // ──────────────────────────────────────────────
    // Stop
    // ──────────────────────────────────────────────

    [Fact]
    public void Stop_ReleasesResources_SubsequentEnqueueThrows()
    {
        var buffer = SampleData.CreateBuffer();
        buffer.Stop();

        Assert.Throws<ObjectDisposedException>(() => buffer.Enqueue(SampleData.Message1));
    }

    [Fact]
    public void Stop_CalledTwice_DoesNotThrow()
    {
        var buffer = SampleData.CreateBuffer();
        buffer.Stop();

        var ex = Record.Exception(() => buffer.Stop());

        Assert.Null(ex);
    }

    [Fact]
    public void Stop_BeforeStart_DoesNotThrow()
    {
        var buffer = new BlockingSpscRingBuffer<TestMessage>(SampleData.DefaultCapacity, SampleData.SpinCountEnqueue, SampleData.SpinCountDequeue);

        var ex = Record.Exception(() => buffer.Stop());

        Assert.Null(ex);
    }

    // ──────────────────────────────────────────────
    // Dispose
    // ──────────────────────────────────────────────

    [Fact]
    public void Dispose_DelegatesToStop_SubsequentEnqueueThrows()
    {
        var buffer = SampleData.CreateBuffer();
        buffer.Dispose();

        Assert.Throws<ObjectDisposedException>(() => buffer.Enqueue(SampleData.Message1));
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var buffer = SampleData.CreateBuffer();
        buffer.Dispose();

        var ex = Record.Exception(() => buffer.Dispose());

        Assert.Null(ex);
    }

    // ──────────────────────────────────────────────
    // Count
    // ──────────────────────────────────────────────

    [Fact]
    public void Count_AfterEnqueue_Increments()
    {
        using var buffer = SampleData.CreateBuffer();

        buffer.Enqueue(SampleData.Message1);
        Assert.Equal(1, buffer.Count);

        buffer.Enqueue(SampleData.Message2);
        Assert.Equal(2, buffer.Count);
    }

    [Fact]
    public void Count_AfterDequeue_Decrements()
    {
        using var buffer = SampleData.CreateBuffer();
        buffer.Enqueue(SampleData.Message1);
        buffer.Enqueue(SampleData.Message2);

        buffer.Dequeue();
        Assert.Equal(1, buffer.Count);

        buffer.Dequeue();
        Assert.Equal(0, buffer.Count);
    }

    // ──────────────────────────────────────────────
    // Enqueue / Dequeue — happy path
    // ──────────────────────────────────────────────

    [Fact]
    public void EnqueueDequeue_SingleItem_ReturnsSameItem()
    {
        using var buffer = SampleData.CreateBuffer();

        buffer.Enqueue(SampleData.Message1);
        var result = buffer.Dequeue();

        Assert.Equal(SampleData.Message1, result);
    }

    [Fact]
    public void EnqueueDequeue_MultipleItems_ReturnsFIFOOrder()
    {
        using var buffer = SampleData.CreateBuffer();

        buffer.Enqueue(SampleData.Message1);
        buffer.Enqueue(SampleData.Message2);
        buffer.Enqueue(SampleData.Message3);

        Assert.Equal(SampleData.Message1, buffer.Dequeue());
        Assert.Equal(SampleData.Message2, buffer.Dequeue());
        Assert.Equal(SampleData.Message3, buffer.Dequeue());
    }

    [Fact]
    public void EnqueueDequeue_FillToCapacity_AllItemsDequeued()
    {
        using var buffer = SampleData.CreateBuffer(); // capacity 4

        buffer.Enqueue(SampleData.Message1);
        buffer.Enqueue(SampleData.Message2);
        buffer.Enqueue(SampleData.Message3);
        buffer.Enqueue(SampleData.Message4);

        Assert.Equal(4, buffer.Count);

        Assert.Equal(SampleData.Message1, buffer.Dequeue());
        Assert.Equal(SampleData.Message2, buffer.Dequeue());
        Assert.Equal(SampleData.Message3, buffer.Dequeue());
        Assert.Equal(SampleData.Message4, buffer.Dequeue());
        Assert.Equal(0, buffer.Count);
    }

    [Fact]
    public void EnqueueDequeue_InterleavedOperations_MaintainsFIFO()
    {
        using var buffer = SampleData.CreateBuffer();

        buffer.Enqueue(SampleData.Message1);
        buffer.Enqueue(SampleData.Message2);
        Assert.Equal(SampleData.Message1, buffer.Dequeue());

        buffer.Enqueue(SampleData.Message3);
        Assert.Equal(SampleData.Message2, buffer.Dequeue());
        Assert.Equal(SampleData.Message3, buffer.Dequeue());

        Assert.Equal(0, buffer.Count);
    }

    [Fact]
    public void EnqueueDequeue_WrapsAroundBuffer_RetainsCorrectOrder()
    {
        using var buffer = SampleData.CreateBuffer(2); // capacity 2

        for (var round = 0; round < 5; round++)
        {
            var msg = new TestMessage(round, round * 10m);
            buffer.Enqueue(msg);
            var result = buffer.Dequeue();
            Assert.Equal(msg, result);
        }

        Assert.Equal(0, buffer.Count);
    }

    [Fact]
    public void EnqueueDequeue_CapacityOne_WorksCorrectly()
    {
        using var buffer = SampleData.CreateBuffer(1);

        buffer.Enqueue(SampleData.Message1);
        Assert.Equal(1, buffer.Count);

        var result = buffer.Dequeue();
        Assert.Equal(SampleData.Message1, result);
        Assert.Equal(0, buffer.Count);
    }

    // ──────────────────────────────────────────────
    // Enqueue — blocking when full
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Enqueue_WhenFull_BlocksUntilSlotAvailable()
    {
        using var buffer = SampleData.CreateBuffer(2);
        buffer.Enqueue(SampleData.Message1);
        buffer.Enqueue(SampleData.Message2);

        var enqueued = false;

        var producerTask = Task.Run(() =>
        {
            buffer.Enqueue(SampleData.Message3);
            enqueued = true;
        });

        await Task.Delay(50);
        Assert.False(enqueued);

        buffer.Dequeue();
        await producerTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(enqueued);
    }

    [Fact]
    public void Enqueue_WhenFullAndCancelled_ThrowsOperationCanceledException()
    {
        using var buffer = SampleData.CreateBuffer(2);
        buffer.Enqueue(SampleData.Message1);
        buffer.Enqueue(SampleData.Message2);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        Assert.Throws<OperationCanceledException>(() =>
            buffer.Enqueue(SampleData.Message3, cts.Token));
    }

    // ──────────────────────────────────────────────
    // Dequeue — blocking when empty
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Dequeue_WhenEmpty_BlocksUntilItemAvailable()
    {
        using var buffer = SampleData.CreateBuffer();
        TestMessage? received = null;

        var consumerTask = Task.Run(() =>
        {
            received = buffer.Dequeue();
        });

        await Task.Delay(50);
        Assert.Null(received);

        buffer.Enqueue(SampleData.Message1);
        await consumerTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(SampleData.Message1, received);
    }

    [Fact]
    public void Dequeue_WhenEmptyAndCancelled_ThrowsOperationCanceledException()
    {
        using var buffer = SampleData.CreateBuffer();

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        Assert.Throws<OperationCanceledException>(() =>
            buffer.Dequeue(cts.Token));
    }

    // ──────────────────────────────────────────────
    // Disposed state
    // ──────────────────────────────────────────────

    [Fact]
    public void Enqueue_AfterStop_ThrowsObjectDisposedException()
    {
        var buffer = SampleData.CreateBuffer();
        buffer.Stop();

        Assert.Throws<ObjectDisposedException>(() => buffer.Enqueue(SampleData.Message1));
    }

    [Fact]
    public void Dequeue_AfterStop_ThrowsObjectDisposedException()
    {
        var buffer = SampleData.CreateBuffer();
        buffer.Stop();

        Assert.Throws<ObjectDisposedException>(() => buffer.Dequeue());
    }

    // ──────────────────────────────────────────────
    // Concurrent producer/consumer
    // ──────────────────────────────────────────────

    [Fact]
    public async Task ConcurrentProducerConsumer_AllMessagesDeliveredInOrder()
    {
        const int messageCount = 1000;
        using var buffer = SampleData.CreateBuffer(16);
        var results = new List<TestMessage>(messageCount);

        var producerTask = Task.Run(() =>
        {
            for (var i = 0; i < messageCount; i++)
                buffer.Enqueue(new TestMessage(i, i * 1.5m));
        });

        var consumerTask = Task.Run(() =>
        {
            for (var i = 0; i < messageCount; i++)
                results.Add(buffer.Dequeue());
        });

        await Task.WhenAll(producerTask, consumerTask).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(messageCount, results.Count);
        for (var i = 0; i < messageCount; i++)
        {
            Assert.Equal(i, results[i].Id);
            Assert.Equal(i * 1.5m, results[i].Price);
        }
    }

    [Fact]
    public async Task ConcurrentProducerConsumer_CancellationStopsBoth()
    {
        using var buffer = SampleData.CreateBuffer(4);
        using var cts = new CancellationTokenSource();
        var producerStopped = false;
        var consumerStopped = false;

        var producerTask = Task.Run(() =>
        {
            try
            {
                for (var i = 0; ; i++)
                    buffer.Enqueue(new TestMessage(i, i), cts.Token);
            }
            catch (OperationCanceledException)
            {
                producerStopped = true;
            }
        });

        var consumerTask = Task.Run(() =>
        {
            try
            {
                while (true)
                    buffer.Dequeue(cts.Token);
            }
            catch (OperationCanceledException)
            {
                consumerStopped = true;
            }
        });

        await Task.Delay(100);
        cts.Cancel();

        await Task.WhenAll(producerTask, consumerTask).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(producerStopped);
        Assert.True(consumerStopped);
    }

    // ──────────────────────────────────────────────
    // Large capacity
    // ──────────────────────────────────────────────

    [Fact]
    public void EnqueueDequeue_LargeCapacity_HandlesCorrectly()
    {
        const int capacity = 1024;
        using var buffer = SampleData.CreateBuffer(capacity);

        for (var i = 0; i < capacity; i++)
            buffer.Enqueue(new TestMessage(i, i));

        Assert.Equal(capacity, buffer.Count);

        for (var i = 0; i < capacity; i++)
        {
            var result = buffer.Dequeue();
            Assert.Equal(i, result.Id);
        }

        Assert.Equal(0, buffer.Count);
    }
}
