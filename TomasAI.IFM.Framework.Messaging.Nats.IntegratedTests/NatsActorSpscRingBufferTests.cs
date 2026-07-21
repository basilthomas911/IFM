using FluentAssertions;
using NATS.Client.Core;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;

namespace TomasAI.IFM.Framework.Messaging.Nats.IntegratedTests;

/// <summary>
/// Integration tests for <see cref="NatsActorSpscRingBuffer"/> covering lifecycle, SPSC behavior,
/// blocking and cancellation, unblocking semantics, concurrency, and disposal edge cases.
/// </summary>
public class NatsActorSpscRingBufferTests
{
 /// <summary>
 /// Helper to create a ring buffer. Caller must call Start() before use and Stop() after use.
 /// </summary>
 private static NatsActorSpscRingBuffer CreateBuffer(int capacity, int spinEnq =1, int spinDeq =1)
 => new(capacity, spinEnq, spinDeq);

 /// <summary>
 /// Helper to create and start a buffer ready for use.
 /// </summary>
 private static NatsActorSpscRingBuffer CreateAndStart(int capacity, int spinEnq =1, int spinDeq =1)
 {
 var buf = CreateBuffer(capacity, spinEnq, spinDeq);
 buf.Start();
 return buf;
 }

 /// <summary>
 /// Verifies constructor rejects invalid capacities (non power-of-two or less than one).
 /// </summary>
 [Theory]
 [InlineData(0)]
 [InlineData(3)] // not a power of two
 public void Ctor_InvalidCapacity_Throws(int invalidCapacity)
 {
 Action act = () => CreateBuffer(invalidCapacity);
 act.Should().Throw<ArgumentOutOfRangeException>();
 }

 /// <summary>
 /// Ensures a valid constructor sets expected initial state (capacity, empty count) before Start().
 /// </summary>
 [Fact]
 public void Ctor_ValidCapacity_InitialState()
 {
 var buf = CreateBuffer(8);
 try
 {
 buf.Capacity.Should().Be(8);
 buf.Count.Should().Be(0);
 }
 finally
 {
 // not started, so Stop should be a no-op if called once
 buf.Stop();
 }
 }

 /// <summary>
 /// Validates basic single-thread enqueue and dequeue transitions and count tracking.
 /// </summary>
 [Fact]
 public void Enqueue_Dequeue_SingleThread_BasicFlow()
 {
 var buf = CreateAndStart(8);
 try
 {
 var msg = default(NatsMsg<byte[]>);

 buf.Enqueue(msg);
 buf.Count.Should().Be(1);

 var _ = buf.Dequeue();
 buf.Count.Should().Be(0);
 }
 finally { buf.Stop(); }
 }

 /// <summary>
 /// Asserts enqueue blocks and can be canceled when the buffer is full.
 /// </summary>
 [Fact]
 public void Enqueue_WhenFull_Cancels()
 {
 const int cap =8; // logical capacity
 var buf = CreateAndStart(cap, spinEnq:1, spinDeq:1);
 try
 {
 var msg = default(NatsMsg<byte[]>);

 // Fill up to capacity -1; with SPSC full detection (tail+1==head), usable slots = cap -1
 for (int i =0; i < cap -1; i++) buf.Enqueue(msg);

 buf.Count.Should().Be(cap -1);

 using var cts = new CancellationTokenSource();
 cts.Cancel();
 Action act = () => buf.Enqueue(msg, cts.Token);
 act.Should().Throw<OperationCanceledException>();

 // Count unchanged after canceled enqueue
 buf.Count.Should().Be(cap -1);
 }
 finally { buf.Stop(); }
 }

 /// <summary>
 /// Ensures a producer blocked on full buffer unblocks after a consumer dequeues an item.
 /// </summary>
 [Fact]
 public async Task Enqueue_WhenFull_UnblocksAfterDequeue()
 {
 const int cap =8;
 var buf = CreateAndStart(cap, spinEnq:1, spinDeq:1);
 try
 {
 var msg = default(NatsMsg<byte[]>);

 for (int i =0; i < cap -1; i++) buf.Enqueue(msg);

 // Start a task that will block trying to enqueue
 var enqTask = Task.Run(() => buf.Enqueue(msg, CancellationToken.None));

 // Give it a moment to attempt and block
 await Task.Delay(2000);

 // Dequeue one to make space; this should unblock the producer
 var _ = buf.Dequeue();

 // Now the enqueue should complete
 await enqTask.WaitAsync(TimeSpan.FromSeconds(2));
 buf.Count.Should().Be(cap -1);
 }
 finally { buf.Stop(); }
 }

 /// <summary>
 /// Asserts dequeue blocks and can be canceled when the buffer is empty.
 /// </summary>
 [Fact]
 public void Dequeue_WhenEmpty_Cancels()
 {
 var buf = CreateAndStart(8, spinEnq:1, spinDeq:1);
 try
 {
 using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
 Action act = () => buf.Dequeue(cts.Token);
 act.Should().Throw<OperationCanceledException>();
 buf.Count.Should().Be(0);
 }
 finally { buf.Stop(); }
 }

 /// <summary>
 /// Ensures a consumer blocked on empty buffer unblocks after a producer enqueues an item.
 /// </summary>
 [Fact]
 public async Task Dequeue_WhenEmpty_UnblocksAfterEnqueue()
 {
 var buf = CreateAndStart(8, spinEnq:1, spinDeq:1);
 try
 {
 var msg = default(NatsMsg<byte[]>);

 // Start a task that will block trying to dequeue
 var deqTask = Task.Run(() => buf.Dequeue(CancellationToken.None));

 // Give it a moment to attempt and block
 await Task.Delay(200);

 // Enqueue one to make an item available; this should unblock the consumer
 buf.Enqueue(msg);

 var _ = await deqTask.WaitAsync(TimeSpan.FromSeconds(2));
 buf.Count.Should().Be(0);
 }
 finally { buf.Stop(); }
 }

 /// <summary>
 /// Validates concurrent producer/consumer process the full item count without loss or duplication.
 /// </summary>
 [Fact]
 public async Task ProducerConsumer_Concurrent_ProcessesAll()
 {
 const int cap =1024;
 const int total =10_000;

 var buf = CreateAndStart(cap, spinEnq:4, spinDeq:4);
 try
 {
 var msg = default(NatsMsg<byte[]>);

 var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
 int produced =0;
 int consumed =0;

 var producer = Task.Run(() =>
 {
 try
 {
 for (int i =0; i < total; i++)
 {
 buf.Enqueue(msg, cts.Token);
 produced++;
 }
 }
 catch (OperationCanceledException) { }
 }, cts.Token);

 var consumer = Task.Run(() =>
 {
 try
 {
 for (int i =0; i < total; i++)
 {
 var _ = buf.Dequeue(cts.Token);
 consumed++;
 }
 }
 catch (OperationCanceledException) { }
 }, cts.Token);

 await Task.WhenAll(producer, consumer);
 produced.Should().Be(total);
 consumed.Should().Be(total);
 buf.Count.Should().Be(0);
 }
 finally { buf.Stop(); }
 }

 /// <summary>
 /// Verifies Stop prevents further use and throws <see cref="ObjectDisposedException"/>.
 /// Also ensures Stop can be called twice without throwing.
 /// </summary>
 [Fact]
 public void Stop_ThenUse_ThrowsObjectDisposed_AndStopTwice_NoThrow()
 {
 var buf = CreateAndStart(8);
 buf.Stop();

 var msg = default(NatsMsg<byte[]>);
 Action act1 = () => buf.Enqueue(msg);
 act1.Should().Throw<ObjectDisposedException>();
 Action act2 = () => { var _ = buf.Dequeue(); };
 act2.Should().Throw<ObjectDisposedException>();

 // calling Stop again is a no-op
 Action act3 = () => buf.Stop();
 act3.Should().NotThrow();
 }
}
