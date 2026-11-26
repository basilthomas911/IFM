using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Model;
using System.Buffers.Binary;
using BenchmarkDotNet.Attributes;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.IntegrationTesting;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.TestSupport;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes;

/// <summary>Isolated transport feasibility probe. Not a production mailbox adapter or an end-to-end actor benchmark.</summary>
[MemoryDiagnoser]
public class RealtimeMailboxTransportProbe
{
    [Params(1, 64)] public int Count { get; set; }
    [Params(512, 8192)] public int PayloadBytes { get; set; }
    static IsolatedIntegrationInfrastructure? infrastructure;
    NatsConnection connection = null!;
    INatsSub<byte[]> core = null!;
    INatsJSContext jetStream = null!;
    INatsJSConsumer memory = null!, file = null!;
    ActorThreadQueueV2 queue = null!;
    byte[] payload = null!;
    static readonly NatsByteArrayMessageSerializer Serializer = new();
    long sequence;

    public static async Task StartBroker()
    {
        if (infrastructure is not null) return;
        var value = new IsolatedIntegrationInfrastructure("mailboxprobe", includeCql: false);
        try { await value.StartAsync(); infrastructure = value; }
        catch { await value.DisposeAsync(); throw; }
    }
    public static async Task StopBroker()
    {
        var value = infrastructure; infrastructure = null;
        if (value is not null) await value.DisposeAsync();
    }

    [GlobalSetup]
    public async Task Setup()
    {
        try
        {
            await StartBroker();
            connection = new(new NatsOpts { Url = infrastructure!.NatsUrl });
            jetStream = new NatsJSContext(connection);
            core = await connection.SubscribeCoreAsync<byte[]>("mailboxprobe.core", serializer: Serializer,
                opts: new NatsSubOpts { ChannelOpts = new NatsSubChannelOpts { Capacity = 256, FullMode = System.Threading.Channels.BoundedChannelFullMode.Wait } });
            await connection.PingAsync();
            memory = await Prepare("memory", StreamConfigStorage.Memory);
            file = await Prepare("file", StreamConfigStorage.File);
            queue = new(new ActorAdmissionController(new ActorAdmissionOptions
            {
                Mode = ActorAdmissionMode.Enforce, GlobalMessageLimit = 32768, GlobalByteLimit = 512 * 1024 * 1024,
                MaximumPayloadBytes = 16 * 1024 * 1024, DefaultActorTypeMessageLimit = 8192,
                DefaultActorTypeByteLimit = 128 * 1024 * 1024
            }), capacity: 2048);
            queue.SetId(new(ActorType.Realtime, "Probe", "single")); queue.Start();
            payload = new byte[PayloadBytes]; Random.Shared.NextBytes(payload);
        }
        catch { await Cleanup(); throw; }
    }
    async Task<INatsJSConsumer> Prepare(string name, StreamConfigStorage storage)
    {
        await jetStream.CreateStreamAsync(new StreamConfig("MAILBOX_PROBE_" + name, ["mailboxprobe." + name + ".>"])
        {
            Storage = storage, Retention = StreamConfigRetention.Workqueue, Discard = StreamConfigDiscard.New,
            DiscardNewPerSubject = true, MaxMsgsPerSubject = 512, MaxBytes = 64 * 1024 * 1024,
            MaxMsgSize = 256 * 1024, NumReplicas = 1
        });
        return await jetStream.CreateOrUpdateConsumerAsync("MAILBOX_PROBE_" + name, new ConsumerConfig("single")
        {
            FilterSubject = "mailboxprobe." + name + ".single", AckPolicy = ConsumerConfigAckPolicy.Explicit,
            MaxAckPending = 128, AckWait = TimeSpan.FromSeconds(10), MaxDeliver = -1
        });
    }
    [Benchmark(Baseline = true)]
    public async Task<long> BeforeCoreAndLocalQueue()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        long expected = 0;
        for (var n = 0; n < Count; n++)
        {
            var id = ++sequence; expected += id; BinaryPrimitives.WriteInt64LittleEndian(payload, id);
            await connection.PublishAsync("mailboxprobe.core", payload, serializer: Serializer, cancellationToken: timeout.Token);
        }
        for (var n = 0; n < Count; n++)
        {
            var message = await core.Msgs.ReadAsync(timeout.Token); message.EnsureSuccess();
            if (!queue.Write(new ProbeMessage(message.Data!))) throw new InvalidOperationException("Local probe admission failed.");
        }
        long sum = 0;
        foreach (var message in queue.ReadAll()) { sum += Handle(((ProbeMessage)message).Payload); message.Dispose(); }
        if (sum != expected) throw new InvalidDataException("Core/local result changed.");
        return sum;
    }
    [Benchmark] public Task<long> AfterMemoryTransportProbe() => RoundTrip(memory, "memory");
    [Benchmark] public Task<long> AfterFileTransportProbe() => RoundTrip(file, "file");
    [Benchmark] public Task<long> AfterMemoryBatchedProbe() => RoundTrip(memory, "memory", batched: true);
    [Benchmark] public Task<long> AfterFileBatchedProbe() => RoundTrip(file, "file", batched: true);
    async Task<long> RoundTrip(INatsJSConsumer consumer, string name, bool batched = false)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        long expected = 0; var first = sequence + 1;
        var publications = batched ? new Task[Count] : null;
        for (var n = 0; n < Count; n++)
        {
            var id = ++sequence; expected += id; BinaryPrimitives.WriteInt64LittleEndian(payload, id);
            if (publications is not null) publications[n] = Publish(payload.ToArray());
            else await Publish(payload);
        }
        if (publications is not null) await Task.WhenAll(publications);
        long sum = 0; var count = 0;
        var acknowledgements = batched ? new List<Task>(Count) : null;
        await foreach (var message in consumer.FetchAsync<byte[]>(new NatsJSFetchOpts
            { MaxMsgs = Count, Expires = TimeSpan.FromSeconds(2) }, Serializer, timeout.Token))
        {
            var id = Handle(message.Data!);
            if (id != first + count) throw new InvalidDataException("Broker delivery order changed.");
            sum += id; count++;
            var ack = message.AckAsync(new AckOpts { DoubleAck = true }, timeout.Token);
            if (acknowledgements is null) await ack;
            else acknowledgements.Add(ack.AsTask());
        }
        if (acknowledgements is not null) await Task.WhenAll(acknowledgements);
        if (count != Count || sum != expected) throw new InvalidDataException("JetStream result changed.");
        return sum;

        async Task Publish(byte[] bytes)
        {
            var ack = await jetStream.PublishAsync("mailboxprobe." + name + ".single", bytes,
                serializer: Serializer, cancellationToken: timeout.Token); ack.EnsureSuccess();
        }
    }
    static long Handle(byte[] data) => BinaryPrimitives.ReadInt64LittleEndian(data);
    public async Task<object> Verify()
    {
        await BeforeCoreAndLocalQueue(); await AfterMemoryTransportProbe(); await AfterFileTransportProbe();
        await AfterMemoryBatchedProbe(); await AfterFileBatchedProbe();
        var handlerProof = new[] { await VerifyHandlerBoundary(memory, "memory"), await VerifyHandlerBoundary(file, "file") };
        var batchProof = new[] { await VerifyBatchBoundary(memory, "memory"), await VerifyBatchBoundary(file, "file") };
        return new { Count, PayloadBytes, checksumsMatch = true,
            handlerProof, batchProof,
            scope = "Isolated broker and actual bar-handler probes; command transport substituted. No production JetStream mailbox adapter or two-session qualification." };
    }

    async Task<object> VerifyBatchBoundary(INatsJSConsumer consumer, string name)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        for (var ordinal = 1; ordinal <= 64; ordinal++)
        {
            var bytes = MessagePackBinarySerializer.Shared.Serialize(RealtimeMailboxSafetyFixture.Trade(ordinal))!;
            var result = await jetStream.PublishAsync("mailboxprobe." + name + ".single", bytes, serializer: Serializer, cancellationToken: timeout.Token);
            result.EnsureSuccess();
        }
        var root = Path.Combine(Path.GetTempPath(), "IFM-broker-batch-" + Guid.NewGuid().ToString("N"));
        using (var store = new FuturesTradeSessionBarCheckpointStore(root))
        {
            var failed = new RealtimeMailboxSafetyFixture.Handler(store) { Fail = true };
            await Receive(failed, true);
            if (failed.Persisted.Count != 0) throw new InvalidDataException("Failed batch published unexpected bars.");
        }
        using (var store = new FuturesTradeSessionBarCheckpointStore(root))
        {
            var recovered = new RealtimeMailboxSafetyFixture.Handler(store);
            await Receive(recovered, false);
            if (recovered.Persisted.Count != 1) throw new InvalidDataException("Batch replay duplicated or lost the pending bar.");
        }
        await consumer.RefreshAsync();
        if (consumer.Info.NumPending != 0 || consumer.Info.NumAckPending != 0) throw new InvalidDataException("Batch input settlement incomplete.");
        return new { backend = name, inputs = 64, orderedRetry = true, restartRestoresBatch = true, acknowledgedOnlyAfterCommit = true };

        async Task Receive(RealtimeMailboxSafetyFixture.Handler handler, bool fail)
        {
            var inputs = new List<IEvent>(64);
            var settlements = new List<Func<bool, ValueTask>>(64);
            await foreach (var message in consumer.FetchAsync<byte[]>(new NatsJSFetchOpts { MaxMsgs = 64, Expires = TimeSpan.FromSeconds(3) }, Serializer, timeout.Token))
            {
                var input = MessagePackBinarySerializer.Shared.Deserialize<FuturesMarketPriceUpdatedRealtimeEvent>(message.Data!)!;
                if (input.Price.Trade!.Value.TradeOrdinal != inputs.Count + 1) throw new InvalidDataException("Broker batch retry order changed.");
                inputs.Add(input);
                settlements.Add(success => success ? message.AckAsync(new AckOpts { DoubleAck = true }, timeout.Token)
                    : message.NakAsync(delay: TimeSpan.FromMilliseconds(10), cancellationToken: timeout.Token));
            }
            if (inputs.Count != 64) throw new InvalidDataException("Incomplete broker input batch.");
            var succeeded = false;
            try { await handler.ReceiveBatch(inputs); succeeded = true; }
            catch (IOException) when (fail) { }
            if (succeeded == fail) throw new InvalidDataException("Unexpected batch commit outcome.");
            foreach (var settle in settlements) await settle(succeeded);
        }
    }
    async Task<object> VerifyHandlerBoundary(INatsJSConsumer consumer, string name)
    {
        async Task Deliver(RealtimeMailboxSafetyFixture.Handler handler, long? ordinal, bool fail = false)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            if (ordinal is { } next)
            {
                var bytes = MessagePackBinarySerializer.Shared.Serialize(RealtimeMailboxSafetyFixture.Trade(next))!;
                var stored = await jetStream.PublishAsync("mailboxprobe." + name + ".single", bytes,
                    serializer: Serializer, cancellationToken: timeout.Token); stored.EnsureSuccess();
            }
            var received = 0;
            await foreach (var message in consumer.FetchAsync<byte[]>(new NatsJSFetchOpts
                { MaxMsgs = 1, Expires = TimeSpan.FromSeconds(2) }, Serializer, timeout.Token))
            {
                var source = MessagePackBinarySerializer.Shared.Deserialize<FuturesMarketPriceUpdatedRealtimeEvent>(message.Data!)!;
                var failed = false;
                try
                {
                    if (!await handler.Receive(source)) throw new IOException("Handler did not complete.");
                }
                catch (IOException) when (fail) { failed = true; }
                if (failed) await message.NakAsync(delay: TimeSpan.FromMilliseconds(10), cancellationToken: timeout.Token);
                else
                {
                    if (fail) throw new InvalidDataException("Failed publication incorrectly succeeded.");
                    await message.AckAsync(new AckOpts { DoubleAck = true }, timeout.Token);
                }
                received++;
            }
            if (received != 1) throw new InvalidDataException("Missing broker probe input.");
        }
        var root = Path.Combine(Path.GetTempPath(), "IFM-broker-bar-" + name + "-" + Guid.NewGuid().ToString("N"));
        using (var store = new FuturesTradeSessionBarCheckpointStore(root))
        {
            var failed = new RealtimeMailboxSafetyFixture.Handler(store);
            await Deliver(failed, 1); failed.Fail = true; await Deliver(failed, 2, fail: true);
            await consumer.RefreshAsync();
            if (consumer.Info.NumPending == 0 && consumer.Info.NumAckPending == 0 || failed.Persisted.Count != 0)
                throw new InvalidDataException("Failed publication must remain broker-owned.");
        }
        using (var store = new FuturesTradeSessionBarCheckpointStore(root))
        {
            var recovered = new RealtimeMailboxSafetyFixture.Handler(store);
            await Deliver(recovered, null);
            await consumer.RefreshAsync();
            if (recovered.Persisted.Count != 1 || (consumer.Info.NumPending != 0 || consumer.Info.NumAckPending != 0))
                throw new InvalidDataException("Broker redelivery must recover the pending publication before ACK.");
        }
        using (var store = new FuturesTradeSessionBarCheckpointStore(root + "-open"))
        { var prior = new RealtimeMailboxSafetyFixture.Handler(store); await Deliver(prior, 1); }
        using (var store = new FuturesTradeSessionBarCheckpointStore(root + "-open"))
        {
            var fresh = new RealtimeMailboxSafetyFixture.Handler(store); await Deliver(fresh, 2);
            if (fresh.Persisted.Count != 1) throw new InvalidDataException("Open state was lost across registry restart.");
        }
        return new { backend = name, failedPublicationWasNotAcknowledged = true,
            pendingPublicationRecoveredBeforeAck = true, acknowledgedOpenStateSurvivesRestart = true };
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        queue?.Dispose();
        if (core is not null) await core.DisposeAsync();
        if (connection is not null) await connection.DisposeAsync();
    }
    sealed class ProbeMessage(byte[] payload) : IActorMessage
    {
        public byte[] Payload { get; } = payload;
        public int AdmissionSizeBytes => Payload.Length;
        public ActorSubject Subject => new(ActorType.Realtime, "Probe", "Consume", "single");
        public ActorSubject ReplySubject { get; set; }
        public TCommand? AsCommand<TCommand>() where TCommand : class, ICommand => default;
        public TEvent? AsEvent<TEvent>() where TEvent : class, IEvent => default;
        public TQuery? AsQuery<TQuery, TResult>() where TQuery : class, IQuery<TResult> where TResult : class => default;
        public ValueTask ReplyAsync<TResult>(TResult result) where TResult : class => ValueTask.CompletedTask;
        public void ReleasePayload() { }
        public void Dispose() { }
    }
}
