using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NATS.Net;
using TomasAI.IFM.Framework.Messaging.Nats;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventProjector;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.EventSourceDb;

public sealed partial class MarkerProjectorPipelineTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task JetStream_queue_recreation_and_post_commit_replay_preserve_single_effect(bool batched, bool failAfterCommit)
    {
        var endpoint = Environment.GetEnvironmentVariable("IFM_MARKER_TEST_NATS_URL");
        if (endpoint != "nats://127.0.0.1:24223")
            throw new InvalidOperationException("Explicit isolated JetStream endpoint on 24223 is required.");
        var options = new NatsJetStreamConsumerOptions { Url = endpoint };
        var name = "MarkerJetStream_" + Guid.NewGuid().ToString("N");
        var stream = await Append(batched, name, 1);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var interval = TimeSpan.FromMilliseconds(100); // Test-only retry interval.
        await using var admin = new NatsClient(endpoint);
        var jetStream = admin.CreateJetStreamContext();

        // Publish with workers stopped, then dispose the entire producer connection.
        // A fresh queue instance must consume the server-owned durable message.
        await using (var producer = new NatsJSDurableReplayQueue(options))
        {
            await producer.PrepareAsync(name, interval, deadline.Token);
            await producer.EnqueueAsync(name, stream.Events[0], deadline.Token);
            await producer.EnqueueAsync(name, stream.Events[0], deadline.Token);
        }
        var deliveries = new ConcurrentQueue<Guid>();
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = 0;
        await using var consumer = new NatsJSDurableReplayQueue(options);
        await consumer.DequeueAsync(name, async source =>
        {
            try
            {
                var message = Assert.IsType<ProbeEvent>(source);
                Assert.Equal(stream.Events[0].EventId, message.EventId);
                Assert.Equal(stream.Events[0].Id, message.Id);
                deliveries.Enqueue(message.Id);
                // Fresh projector instance on every transport delivery.
                await Projector(name).ProcessDomainEventAsync(message);
                if (Interlocked.Increment(ref attempts) == 1 && failAfterCommit)
                    throw new InjectedFailure();
                completed.TrySetResult();
                return EventProjectorDeliveryResult.Completed;
            }
            catch (InjectedFailure) { throw; }
            catch (Exception error) { completed.TrySetException(error); throw; }
        }, deadline.Token);
        try
        {
            await consumer.StartAsync(name, interval, deadline.Token);
            await completed.Task.WaitAsync(deadline.Token);
            // Wait for server-observed settlement, not just the handler callback.
            while (true)
            {
                var process = await jetStream.GetConsumerAsync($"IFM_{name}_PROCESS", $"{name}-process-worker", deadline.Token);
                var replay = await jetStream.GetConsumerAsync($"IFM_{name}_REPLAY", $"{name}-replay-worker", deadline.Token);
                if (process.Info.NumPending == 0 && process.Info.NumAckPending == 0 &&
                    replay.Info.NumPending == 0 && replay.Info.NumAckPending == 0) break;
                await Task.Delay(10, deadline.Token);
            }
            var processStream = await jetStream.GetStreamAsync($"IFM_{name}_PROCESS", cancellationToken: deadline.Token);
            var replayStream = await jetStream.GetStreamAsync($"IFM_{name}_REPLAY", cancellationToken: deadline.Token);
            Assert.Equal(1L, processStream.Info.State.Messages);
            Assert.Equal(failAfterCommit ? 1L : 0L, replayStream.Info.State.Messages);
            await consumer.StopAsync(name, deadline.Token);
            await VerifyCompleted(stream, 1);
            Assert.Equal(failAfterCommit ? 2 : 1, attempts);
            Assert.Single(deliveries.Distinct());
        }
        finally { await consumer.StopAsync(name); }
    }
}
