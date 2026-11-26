using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MessagePack;
using NATS.Client.Core;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.EventModelActor;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.RealtimeMailboxes;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes;

/// <summary>Real broker, source journals, ActorThreadPoolV2 and per-thread queues.
/// The actor is an ordered deterministic CPU-only owner surrogate, not a strategy/financial owner.</summary>
public static class RealtimeLoadQualification
{
    static int IoSlotsFromEnvironment()
    {
        var text = Environment.GetEnvironmentVariable("IFM_RT_LOAD_IO_SLOTS");
        if (string.IsNullOrWhiteSpace(text)) return new RealtimeMailboxOptions().DeliveryConcurrency;
        return int.TryParse(text, out var value) && value is >= 1 and <= 128 ? value
            : throw new InvalidOperationException("IFM_RT_LOAD_IO_SLOTS must be between 1 and 128.");
    }
    static NatsRequestReplyMode ReplyModeFromEnvironment()
    {
        var text=Environment.GetEnvironmentVariable("IFM_RT_LOAD_REPLY_MODE");
        if(string.IsNullOrWhiteSpace(text))return NatsRequestReplyMode.SharedInbox;
        return Enum.TryParse<NatsRequestReplyMode>(text,true,out var mode)&&Enum.IsDefined(mode)?mode:
            throw new InvalidOperationException("IFM_RT_LOAD_REPLY_MODE must be SharedInbox or Direct.");
    }
    static readonly DateTimeOffset OriginalTime = new(2026, 11, 25, 14, 30, 0, TimeSpan.Zero);
    static readonly Guid Generation = Guid.Parse("2a54d02d-f237-4ead-97a6-d432ea9bfb12");
    public static async Task RunColdAsync(string storageName, string outputDirectory)
    {
        if (!Enum.TryParse<RealtimeMailboxStorage>(storageName, out var storage) || !Enum.IsDefined(storage)) throw new ArgumentException("Memory or File required.");
        if (Environment.GetEnvironmentVariable("IFM_TEST_REALTIME_NATS") != "nats://127.0.0.1:45222")
            throw new InvalidOperationException("Requires dedicated disposable NATS on 127.0.0.1:45222.");
        var output = Path.GetFullPath(outputDirectory); Directory.CreateDirectory(output);
        await using var runtime = new Runtime(storage, Path.Combine(output, "load-state"), 4096);
        await runtime.Start();
        var result = await runtime.Phase("cold-1000-identities-3-recipients", runtime.Tape("cold", 1000, i => "cold-" + i));
        await File.WriteAllTextAsync(Path.Combine(output, "cold-result.json"), JsonSerializer.Serialize(new
        {
            Storage = storageName, RequestReplyMode = ReplyModeFromEnvironment().ToString(), BrokerIoSlots = IoSlotsFromEnvironment(),
            ServerGc = System.Runtime.GCSettings.IsServerGC, ProcessorCount = Environment.ProcessorCount,
            StartupBeforeReadinessMilliseconds = runtime.StartupMilliseconds,
            Scope = "Cold-only diagnostic; identical full-load cold tape. Startup prepares code/control metadata and registered actor streams, never the 1000 unknown identities or their 3000 consumers. Not full qualification.",
            Result = result, runtime.Committed, runtime.Errors, Witness = runtime.Witness()
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
    public static async Task RunAsync(string storageName, string outputDirectory)
    {
        if (!Enum.TryParse<RealtimeMailboxStorage>(storageName, out var storage)) throw new ArgumentException("Memory or File required.");
        if (Environment.GetEnvironmentVariable("IFM_TEST_REALTIME_NATS") != "nats://127.0.0.1:45222")
            throw new InvalidOperationException("Requires dedicated disposable NATS on 127.0.0.1:45222.");
        var output = Path.GetFullPath(outputDirectory); Directory.CreateDirectory(output);
        var results = new List<object>();
        await using (var runtime = new Runtime(storage, Path.Combine(output, "load-state"), 4096))
        {
            await runtime.Start();
            var cold = runtime.Tape("cold", 1000, i => "cold-" + i);
            results.Add(await runtime.Phase("cold-1000-identities-3-recipients", cold));
            var steady = runtime.Tape("steady", 3000, i => "hot-" + i % 4);
            results.Add(await runtime.Phase("steady-100-sources-per-second-30-seconds", steady, 100));
            var burst = runtime.Tape("burst", 4000, i => "hot-" + i % 4);
            results.Add(await runtime.Phase("burst-4000-sources-3-recipients", burst));
            var before = runtime.Committed;
            await runtime.Restart();
            foreach (var chunk in cold.Take(64).Concat(steady.Take(64)).Concat(burst.Take(64)).Chunk(64))
            {
                var receipts = await runtime.Hub.CaptureBatchAsync(chunk.Select(item => item.Publication).ToArray());
                if (receipts.Any(item => !item.AlreadyCaptured)) throw new InvalidDataException("Cold receipt replay was not recognized.");
            }
            await runtime.Drain();
            if (runtime.Committed != before) throw new InvalidDataException("Restart replay repeated owner effects.");
            results.Add(new { Name = "completed-receipt-restart-replay", Sources = 192, DuplicateEffects = runtime.Committed - before, Verified = true });
            results.Add(new { Name = "owner-witness", Hash = runtime.Witness(), runtime.Committed, runtime.Errors,
                runtime.MaxConcurrentHandlers, runtime.PeakPending, runtime.PeakPendingBytes, runtime.PeakReceiptBytes,
                runtime.PeakWorkingSet, runtime.PeakBrokerBytes, runtime.PeakBrokerMessages });
        }
        await using (var runtime = new Runtime(storage, Path.Combine(output, "capacity-state"), 128))
        {
            await runtime.Start(); runtime.Hold = new(TaskCreationOptions.RunContinuationsAsynchronously);
            var held = runtime.Tape("hold", 128, _ => "HOLD");
            await runtime.Send(held.Take(1).ToArray());
            await runtime.WaitHeld();
            var unrelated = runtime.Tape("unrelated", 1, _ => "unrelated");
            await runtime.Send(unrelated);
            await runtime.WaitCommitted(3);
            if (runtime.Committed != 3) throw new InvalidDataException("Held work affected unrelated mailbox progress.");
            // Handler completion precedes the independently flushed source receipt.
            // Fill the finite budget only after that unrelated source retires.
            await runtime.WaitPending(1);
            await runtime.Send(held.Skip(1).ToArray());
            var overflow = runtime.Tape("overflow", 1, _ => "HOLD"); var refused = false;
            try { await runtime.Send(overflow); } catch (IOException) { refused = true; }
            if (!refused || runtime.Hub.PendingSourceCount != 128) throw new InvalidDataException("Expected finite source capacity refusal.");
            runtime.Hold.TrySetResult(); await runtime.Drain();
            await runtime.Send(overflow); await runtime.Drain();
            if (runtime.Committed != 390) throw new InvalidDataException("Retained refused source did not recover exactly once.");
            results.Add(new { Name = "capacity-hold-unrelated-progress-drain-resume", Capacity = 128,
                RefusedSourceRetained = true, SuccessfulEffects = runtime.Committed, runtime.Errors, Verified = true });
        }
        await File.WriteAllTextAsync(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new
        {
            Storage = storageName, RequestReplyMode = ReplyModeFromEnvironment().ToString(), SourceTimeUtc = OriginalTime, SourceGeneration = Generation,
            ServerGc = System.Runtime.GCSettings.IsServerGC, ProcessorCount = Environment.ProcessorCount,
            LogicalRecipients = 3, BrokerIoSlots = IoSlotsFromEnvironment(), Scheduler = "ActorThreadPoolV2 / ActorThreadQueues / physical slot 1 / 16 workers",
            Note = "Transport/scheduler qualification with deterministic CPU-only owner; decision operators, financial owners and session completeness are separate gates. CapacityQualified=true exists only inside this disposable probe.",
            Results = results
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"LOAD_COMPLETE {storageName} {output}");
    }

    public sealed record Source(RealtimeSourcePublication Publication, Guid Id);
    sealed class Runtime : IAsyncDisposable
    {
        readonly RealtimeMailboxOptions options;
        readonly NatsConnectionManager manager = new(ReplyModeFromEnvironment());
        readonly IActorSupervisor supervisor = Substitute.For<IActorSupervisor>();
        readonly IContainerInstance container = Substitute.For<IContainerInstance>();
        readonly Dictionary<ActorMailboxId, IActor> actors = [];
        readonly Dictionary<string, long> nextSequence = [];
        readonly ConcurrentDictionary<string, State> states = new();
        readonly ConcurrentDictionary<Guid, long> started = new();
        readonly ConcurrentDictionary<Guid, string> hashes = new();
        readonly ConcurrentQueue<string> errors = new();
        readonly ConcurrentQueue<double> receivedLatency = new();
        readonly ConcurrentQueue<double> handlerTime = new();
        readonly ActorThreadPoolV2 pool;
        readonly TaskCompletionSource held = new(TaskCreationOptions.RunContinuationsAsynchronously);
        readonly CancellationTokenSource sampling = new();
        readonly Task sampler;
        long committed, active, maxActive;
        public TaskCompletionSource? Hold;
        public DurableRealtimeMailboxHub Hub { get; private set; }
        public long Committed => Interlocked.Read(ref committed);
        public int Errors => errors.Count;
        public long MaxConcurrentHandlers => maxActive;
        public double StartupMilliseconds { get; private set; }
        public long PeakPending, PeakPendingBytes, PeakReceiptBytes, PeakWorkingSet, PeakBrokerBytes, PeakBrokerMessages;
        public Runtime(RealtimeMailboxStorage storage, string root, int capacity)
        {
            options = new() { Enabled = true, CapacityQualified = true, Storage = storage, Directory = root,
                Epoch = "qualification_" + storage + "_" + Guid.NewGuid().ToString("N")[..12], MaximumPendingSources = capacity, DeliveryConcurrency = IoSlotsFromEnvironment() };
            supervisor.Container.Returns(container);
            supervisor.RuntimeContext.Returns(new SupervisorRuntimeContext());
            container.Resolve<IActorThreadQueue>().Returns(_ => new ActorThreadQueueV2(1));
            pool = new ActorThreadPoolV2(supervisor, NullLogger.Instance); pool.Initialize(16);
            supervisor.ThreadPool.Returns(pool);
            supervisor.GetThread(Arg.Any<ActorThreadId>()).Returns(call => pool.GetThread(call.Arg<ActorThreadId>()));
            foreach (var name in new[] { "LoadPrimary", "LoadFanout1", "LoadFanout2" })
            {
                var id = new ActorMailboxId(ActorType.Realtime, name);
                actors.Add(id, new Owner(this, id, new ActorMailbox(supervisor, id, maxRetainedIdleQueues: 4096)));
            }
            supervisor.Children.Returns(actors);
            supervisor.ActorExists(Arg.Any<ActorMailboxId>()).Returns(call => actors.ContainsKey(call.Arg<ActorMailboxId>()));
            supervisor.GetRealtimeRoutes(Arg.Any<ActorTypeId>()).Returns(ImmutableArray.Create(
                new RealtimeActorRoute(new(ActorType.Realtime, "LoadFanout1")), new RealtimeActorRoute(new(ActorType.Realtime, "LoadFanout2"))));
            Hub = Make(); container.Resolve<IRealtimePublicationCapture>().Returns(_ => Hub);
            sampler = Sample();
        }
        DurableRealtimeMailboxHub Make() => new(options, new NatsProducerOptions { Url = "nats://127.0.0.1:45222" }, manager, NullLogger<DurableRealtimeMailboxHub>.Instance);
        public async Task Start() { var started = Stopwatch.GetTimestamp(); await Hub.StartAsync(supervisor); StartupMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds; }
        public async Task Restart() { await Drain(); await Hub.DisposeAsync(); Hub = Make(); await Start(); }
        public Source[] Tape(string label, int count, Func<int, string> entity)
        {
            var data = new byte[512]; new Random(1701).NextBytes(data);
            var result = new Source[count];
            for (var i = 0; i < count; i++)
            {
                var key = entity(i); nextSequence.TryGetValue(key, out var seq); nextSequence[key] = ++seq;
                var id = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(label + "/" + i)).AsSpan(0, 16));
                var subject = new ActorSubject(ActorType.Realtime, "LoadPrimary", "Updated", key);
                var value = new ProbeEvent { Subject = subject, Id = id, EventId = seq, ReceivedOn = OriginalTime.UtcDateTime, Data = data };
                var bytes = MessagePackSerializer.Serialize(value);
                hashes[id] = Convert.ToHexString(SHA256.HashData(bytes));
                result[i] = new(new(subject, id, bytes, new MessageOrigin(OriginalTime, "mailbox-qualification", Generation, Generation), "GLBX.MDP3", Generation), id);
            }
            return result;
        }
        public async Task Send(Source[] sources)
        {
            foreach (var chunk in sources.Chunk(64))
            {
                foreach (var item in chunk) started.TryAdd(item.Id, Stopwatch.GetTimestamp());
                await Hub.CaptureBatchAsync(chunk.Select(item => item.Publication).ToArray());
            }
        }
        public async Task<object> Phase(string name, Source[] sources, int rate = 0)
        {
            while (receivedLatency.TryDequeue(out _)) { } while (handlerTime.TryDequeue(out _)) { }
            var capture = new List<double>(); var before = Committed; var clock = Stopwatch.StartNew();
            var timing = Hub.Metrics.Timing; var flushCount = Hub.JournalFlushCount; var flushMs = Hub.JournalFlushMilliseconds;
            var allocated = GC.GetTotalAllocatedBytes(true); var gc = GC.CollectionCount(2);
            using var process = Process.GetCurrentProcess(); var cpu = process.TotalProcessorTime;
            using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var batches = sources.Chunk(rate == 0 ? 64 : 10).ToArray(); var emitted = 0;
            foreach (var batch in batches)
            {
                if (rate > 0)
                {
                    var delay = TimeSpan.FromSeconds((double)emitted / rate) - clock.Elapsed;
                    if (delay > TimeSpan.Zero) await Task.Delay(delay);
                }
                foreach (var item in batch) digest.AppendData(item.Publication.Payload.Span);
                var tick = Stopwatch.GetTimestamp(); await Send(batch);
                capture.Add(Stopwatch.GetElapsedTime(tick).TotalMilliseconds); emitted += batch.Length;
            }
            var publicationElapsed = clock.Elapsed.TotalMilliseconds; await Drain();
            if (Committed - before != sources.Length * 3L || Errors != 0) throw new InvalidDataException("Load identity/order/body/settlement invariant failed: " + string.Join(";", errors));
            var result = new { Name = name, Sources = sources.Length, Effects = Committed - before,
                InputHash = Convert.ToHexString(digest.GetHashAndReset()), PayloadBytes = sources[0].Publication.Payload.Length,
                PublicationMilliseconds = publicationElapsed, DrainMilliseconds = clock.Elapsed.TotalMilliseconds - publicationElapsed,
                TotalMilliseconds = clock.Elapsed.TotalMilliseconds, CaptureBatch = Summary(capture), SourceToHandler = Summary(receivedLatency), Handler = Summary(handlerTime),
                StartupBeforeReadinessMilliseconds = StartupMilliseconds,
                AllocatedBytes = GC.GetTotalAllocatedBytes(true) - allocated, Gen2 = GC.CollectionCount(2) - gc,
                TimingBefore = timing, TimingAfter = Hub.Metrics.Timing,
                WriterFlushCount = Hub.JournalFlushCount - flushCount, WriterFlushMilliseconds = Hub.JournalFlushMilliseconds - flushMs,
                CpuMilliseconds = (process.TotalProcessorTime - cpu).TotalMilliseconds, Verified = true };
            var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(options.Directory)!, name + ".json"), json);
            Console.WriteLine(JsonSerializer.Serialize(result)); return result;
        }
        public async Task Drain()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(4));
            while (Hub.PendingSourceCount > 0)
            {
                if (Errors > 0) throw new InvalidDataException(string.Join(";", errors));
                await Task.Delay(5, timeout.Token);
            }
            if (!await pool.WaitForIdleAsync(TimeSpan.FromMilliseconds(5), TimeSpan.FromSeconds(10))) throw new TimeoutException("Scheduler did not drain.");
        }
        public async Task WaitHeld()
        { try { await held.Task.WaitAsync(TimeSpan.FromSeconds(10)); } catch (TimeoutException error) { throw new IOException("Held source did not arrive: " + Hub.CurrentFailure + "; " + Hub.LastFailure, error); } }
        public async Task WaitCommitted(long count)
        { using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)); while (Committed < count) await Task.Delay(5, timeout.Token); }
        public async Task WaitPending(int count)
        { using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)); while (Hub.PendingSourceCount != count) await Task.Delay(5, timeout.Token); }
        async ValueTask Receive(IReadOnlyList<IActorMessage> messages, ActorThreadId thread, CancellationToken token)
        {
            var state = states.GetOrAdd(thread.ToString(), _ => new());
            if (Interlocked.Increment(ref state.Active) != 1) errors.Enqueue("Concurrent same-thread execution: " + thread);
            var concurrent = Interlocked.Increment(ref active); Raise(ref maxActive, concurrent);
            var tick = Stopwatch.GetTimestamp();
            try
            {
                if (thread.EntityId == "HOLD" && Hold is { } gate) { held.TrySetResult(); await gate.Task.WaitAsync(token); }
                foreach (var message in messages)
                {
                    var value = message.AsEvent<ProbeEvent>() ?? throw new InvalidDataException("Missing typed input.");
                    if (value.Subject != message.SourceSubject || value.EventId != state.Last + 1 || value.ReceivedOn != OriginalTime.UtcDateTime
                        || message.Origin?.OriginatedAtUtc != OriginalTime || message.SourceGenerationId != Generation
                        || hashes[value.Id] != Convert.ToHexString(SHA256.HashData(MessagePackSerializer.Serialize(value))))
                        throw new InvalidDataException("Input body/order/origin/identity mismatch: " + thread);
                    state.Last = value.EventId; state.LastId = value.Id;
                    receivedLatency.Enqueue(Stopwatch.GetElapsedTime(started[value.Id]).TotalMilliseconds);
                    Interlocked.Increment(ref committed);
                }
            }
            catch (Exception error) { errors.Enqueue(error.ToString()); throw; }
            finally { handlerTime.Enqueue(Stopwatch.GetElapsedTime(tick).TotalMilliseconds); Interlocked.Decrement(ref active); Interlocked.Decrement(ref state.Active); }
        }
        public string Witness() => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", states.OrderBy(item => item.Key, StringComparer.Ordinal).Select(item => $"{item.Key}|{item.Value.Last}|{item.Value.LastId}")))));
        async Task Sample()
        {
            try { while (!sampling.IsCancellationRequested) { Raise(ref PeakPending, Hub.PendingSourceCount); Raise(ref PeakPendingBytes, Hub.PendingSourceBytes); Raise(ref PeakReceiptBytes, Hub.ReceiptBytes);
                Raise(ref PeakBrokerBytes, Hub.BrokerStoredBytes); Raise(ref PeakBrokerMessages, Hub.BrokerStoredMessages);
                using var process = Process.GetCurrentProcess(); Raise(ref PeakWorkingSet, process.WorkingSet64); await Task.Delay(100, sampling.Token); } }
            catch (OperationCanceledException) when (sampling.IsCancellationRequested) { }
        }
        public async ValueTask DisposeAsync()
        { Hold?.TrySetResult(); await sampling.CancelAsync(); await sampler; await Hub.DisposeAsync(); await pool.DisposeAsync(); await manager.DisposeAsync(); sampling.Dispose(); }
        sealed class State { public long Last; public Guid LastId; public int Active; }
        sealed class Owner(Runtime runtime, ActorMailboxId id, IActorMailbox mailbox) : IActor, IBatchedRealtimeActor
        {
            public ActorMailboxId Id => id; public IActorMailbox Mailbox => mailbox; public bool IsRunning => true;
            public ValueTask StartAsync(IActorSupervisor supervisor) => ValueTask.CompletedTask;
            public ValueTask StopAsync() => ValueTask.CompletedTask;
            public ValueTask HandleMessageAsync(IActorMessage message) => runtime.Receive(new[] { message }, message.Subject.ThreadId, default);
            public ValueTask HandleBatchAsync(IReadOnlyList<IActorMessage> messages, ActorThreadId thread, CancellationToken token) => runtime.Receive(messages, thread, token);
        }
    }
    static void Raise(ref long target, long value)
    { long current; while (value > (current = Interlocked.Read(ref target)) && Interlocked.CompareExchange(ref target, value, current) != current) { } }
    static object Summary(IEnumerable<double> source)
    { var values = source.Order().ToArray(); double At(double p) => values.Length == 0 ? 0 : values[Math.Clamp((int)Math.Ceiling(p * values.Length) - 1, 0, values.Length - 1)];
        return new { Count = values.Length, P50Milliseconds = At(.5), P95Milliseconds = At(.95), P99Milliseconds = At(.99), MaxMilliseconds = At(1),
            OverTwoSeconds = values.Count(value => value > 2000), OverThirtySeconds = values.Count(value => value > 30000) }; }
    [MessagePackObject(true)]
    public sealed class ProbeEvent : IEvent
    {
        public ActorSubject Subject { get; init; } public Guid Id { get; init; } public long EventId { get; init; }
        public Guid CommandId { get; init; } public string AggregateId { get; init; } = "ES";
        public string EventSource { get; init; } = "qualification"; public DateTime ReceivedOn { get; init; }
        [IgnoreMember] public string UserName => "qualification"; [IgnoreMember] public string EventName => "LoadProbe";
        [IgnoreMember] public EventType EventType => EventType.DomainEvent;
        public byte[] Data { get; init; } = [];
    }
}
