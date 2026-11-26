using System.Collections.Immutable;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.RealtimeMailboxes;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes;

/// <summary>Exact raw source tape and receiver: Core acceptance vs flushed source ownership plus broker settlement.
/// Transport boundary only; no claim of equal durability or whole-domain financial/GC qualification.</summary>
[MemoryDiagnoser]
public class DurableMailboxOwnershipBenchmarks
{
    [Params(1, 64)] public int Count { get; set; }
    [Params(512, 8192, 262144)] public int PayloadBytes { get; set; }
    [Params(false, true)] public bool HandlerBatching { get; set; }
    NatsConnectionManager manager = null!;
    NatsActorConsumer core = null!;
    DurableRealtimeMailboxHub memory = null!, file = null!;
    NATS.Net.NatsClient client = null!;
    readonly ActorSubject subject = new(ActorType.Realtime, "BenchmarkPrice", "Updated", "ES");
    readonly NatsByteArrayMessageSerializer serializer = new();
    byte[] payload = null!; string directory = ""; long delivered;

    [GlobalSetup]
    public async Task Setup()
    {
        var url = Environment.GetEnvironmentVariable("IFM_TEST_REALTIME_NATS")
            ?? throw new InvalidOperationException("Requires isolated IFM_TEST_REALTIME_NATS broker.");
        directory = Path.Combine("E:/IFM/BenchmarkArtifacts/durable-mailbox-cases", Guid.NewGuid().ToString("N"));
        payload = new byte[PayloadBytes]; new Random(1701).NextBytes(payload);
        var supervisor = Substitute.For<IActorSupervisor>(); var actor = HandlerBatching ? Substitute.For<IActor, IBatchedRealtimeActor>() : Substitute.For<IActor>();
        var mailbox = Substitute.For<IActorMailbox>(); var queues = Substitute.For<IActorThreadQueues>();
        actor.Mailbox.Returns(mailbox); mailbox.ThreadQueues.Returns(queues);
        supervisor.Children.Returns(new Dictionary<ActorMailboxId, IActor> { [subject.ActorId] = actor });
        supervisor.ActorExists(subject.ActorId).Returns(true);
        supervisor.GetRealtimeRoutes(Arg.Any<ActorTypeId>()).Returns(ImmutableArray<RealtimeActorRoute>.Empty);
        queues.TryAdmitAsync(Arg.Any<IActorMessage>(), Arg.Any<ActorSubject>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var message = call.Arg<IActorMessage>();
            var children = message is IRealtimeBrokerBatch group ? group.Messages : new[] { message };
            foreach (var child in children)
            {
                child.ReleasePayload();
                if (child is IActorDeliveryCompletion completion) completion.CompleteDeliveryAsync(true).GetAwaiter().GetResult();
                Interlocked.Increment(ref delivered);
            }
            message.Dispose();
            return ValueTask.FromResult(ActorAdmissionResult.AcceptedResult);
        });
        manager = new(); client = await manager.GetClientAsync(url);
        // All three paths share the same dedicated broker, raw payload tape and mock handler.
        core = new(new NatsConsumerOptions { Url = url }, NullLogger.Instance, manager);
        await core.StartAsync(supervisor, ActorType.Realtime);
        memory = Make(RealtimeMailboxStorage.Memory); file = Make(RealtimeMailboxStorage.File);
        await memory.StartAsync(supervisor); await file.StartAsync(supervisor);
        DurableRealtimeMailboxHub Make(RealtimeMailboxStorage storage) => new(new RealtimeMailboxOptions
        {
            Enabled = true, CapacityQualified = true, Storage = storage, Directory = Path.Combine(directory, storage.ToString()),
            Epoch = "benchmark_" + Guid.NewGuid().ToString("N")
        }, new NatsProducerOptions { Url = url }, manager, NullLogger<DurableRealtimeMailboxHub>.Instance);
    }
    [Benchmark(Baseline = true)] public async Task BeforeCorePubSub()
    {
        var target = Interlocked.Read(ref delivered) + Count;
        for (var i = 0; i < Count; i++) await client.PublishAsync(subject.ToString(), payload, serializer: serializer);
        await Wait(target);
    }
    [Benchmark] public Task AfterSerialMemoryDurableOwnership() => SendSerial(memory);
    [Benchmark] public Task AfterSerialFileDurableOwnership() => SendSerial(file);
    async Task SendSerial(DurableRealtimeMailboxHub hub)
    {
        var target = Interlocked.Read(ref delivered) + Count;
        for (var i = 0; i < Count; i++) await hub.CaptureAsync(new(subject, Guid.NewGuid(), payload));
        await Wait(target);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        while (hub.PendingSourceCount > 0) await Task.Delay(1, timeout.Token);
    }
    [Benchmark] public Task AfterMemoryDurableOwnership() => Send(memory);
    [Benchmark] public Task AfterFileDurableOwnership() => Send(file);
    async Task Send(DurableRealtimeMailboxHub hub)
    {
        var target = Interlocked.Read(ref delivered) + Count;
        var sources = Enumerable.Range(0, Count).Select(_ => new RealtimeSourcePublication(subject, Guid.NewGuid(), payload)).ToArray();
        await hub.CaptureBatchAsync(sources); await Wait(target);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        while (hub.PendingSourceCount > 0) await Task.Delay(1, timeout.Token);
    }
    async Task Wait(long target)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        while (Interlocked.Read(ref delivered) < target) await Task.Delay(1, timeout.Token);
    }
    [GlobalCleanup] public async Task Cleanup()
    {
        if (core is not null) await core.DisposeAsync();
        if (memory is not null) await memory.DisposeAsync(); if (file is not null) await file.DisposeAsync();
        if (manager is not null) await manager.DisposeAsync();
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}
