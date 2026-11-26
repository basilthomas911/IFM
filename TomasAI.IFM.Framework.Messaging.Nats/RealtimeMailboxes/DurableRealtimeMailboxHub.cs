using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.MarketData.Contracts.TickAggregation;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream.RealtimeMailboxes;

/// <summary>Realtime-only broker mailbox transport. The local scheduler holds at most one fetched message per mailbox.</summary>
public sealed class DurableRealtimeMailboxHub(RealtimeMailboxOptions options, INatsProducerOptions connectionOptions,
    NatsConnectionManager connectionManager, ILogger<DurableRealtimeMailboxHub> logger) : IRealtimePublicationCapture, IRealtimeGenerationDrain, IAsyncDisposable
{
    readonly Lock registryGate = new();
    readonly ConcurrentDictionary<string, Mailbox> mailboxes = new(StringComparer.Ordinal);
    readonly ConcurrentDictionary<ActorThreadId, Mailbox> mailboxesByIdentity = new();
    readonly ConcurrentDictionary<string, string> actorKeys = new(StringComparer.Ordinal);
    readonly ConcurrentDictionary<string, byte> registeredStreams = new(StringComparer.Ordinal);
    readonly ConcurrentDictionary<string, string> publicationFailures = new(StringComparer.Ordinal);
    readonly ConcurrentDictionary<string, Publication> publications = new(StringComparer.Ordinal);
    readonly ConcurrentDictionary<string, byte> confirming = new(StringComparer.Ordinal);
    readonly ConcurrentDictionary<string, Lazy<Task<INatsJSStream>>> streams = new(StringComparer.Ordinal);
    readonly ConcurrentDictionary<string, StreamTransport> transports = new(StringComparer.Ordinal);
    readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    readonly CancellationTokenSource stopping = new();
    readonly SemaphoreSlim startGate = new(1, 1);
    readonly Channel<bool> sourceReady = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });
    readonly Channel<Mailbox> runnable = Channel.CreateBounded<Mailbox>(new BoundedChannelOptions(options.MaximumMailboxes) { FullMode = BoundedChannelFullMode.Wait });
    readonly Channel<CapturedRealtimeSource> confirmations = Channel.CreateBounded<CapturedRealtimeSource>(new BoundedChannelOptions(options.MaximumPendingSources)
        { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
    static readonly NatsByteArrayMessageSerializer Serializer = new();
    IActorSupervisor supervisor = null!;
    INatsJSContext js = null!;
    RealtimeSourceOutbox? outbox;
    Task[] workers = [];
    Task? publisher, monitor, confirmer;
    readonly ConcurrentDictionary<string, RealtimeBrokerStreamSnapshot> brokerStreams = new(StringComparer.Ordinal);
    int disposed;
    long suppressedPhysicalSettlements;
    long consumerRefreshCount;
    internal long ConsumerRefreshCount => Interlocked.Read(ref consumerRefreshCount);
    // Fault seam runs outside the transport gate so tests can reconstruct a
    // stream after durable handler completion and before physical settlement.
    internal Func<ActorThreadId, Guid, bool, int, CancellationToken, ValueTask>? BeforePhysicalSettlementAsync { get; set; }
    internal Func<Guid, CancellationToken, ValueTask>? BeforeSourceConfirmationAsync { get; set; }
    internal long SuppressedPhysicalSettlements => Interlocked.Read(ref suppressedPhysicalSettlements);
    string? connectionFailure;
    public bool Enabled => options.Enabled;
    public bool IsRunning => ready.Task.IsCompletedSuccessfully && !stopping.IsCancellationRequested;
    public RealtimeMailboxMetrics Metrics { get; } = new();
    public int MailboxCount => mailboxes.Count;
    public long BrokerStoredBytes => brokerStreams.Values.Sum(value => value.Bytes);
    public long BrokerStoredMessages => brokerStreams.Values.Sum(value => value.Messages);
    public IReadOnlyList<RealtimeBrokerStreamSnapshot> StreamSnapshots => brokerStreams.Values.OrderBy(value => value.Stream).Take(200).ToArray();
    public IReadOnlyList<RealtimeBrokerMailboxSnapshot> GetMailboxSnapshots(int offset, int limit) => mailboxes.Values
        .OrderBy(value => value.Key).Skip(Math.Max(0, offset)).Take(Math.Clamp(limit, 1, 200))
        .Select(value => new RealtimeBrokerMailboxSnapshot(value.Identity, value.BrokerPending, value.BrokerUnacknowledged,
            Volatile.Read(ref value.Scheduled) != 0, value.Failure)).ToArray();
    public int PendingSourceCount => outbox?.PendingCount ?? 0;
    public long PendingSourceBytes => outbox?.PendingBytes ?? 0;
    public long RetainedSourceBytes => outbox?.RetainedSegmentBytes ?? 0;
    public long ReceiptBytes => outbox?.ReceiptBytes ?? 0;
    public long JournalFlushCount => outbox?.WriterFlushCount ?? 0;
    public double JournalFlushMilliseconds => outbox?.WriterFlushMilliseconds ?? 0;
    public string? LastFailure => outbox?.Failure?.Message ?? Metrics.LastFailure;
    public string? CurrentFailure => outbox?.Failure?.Message ?? outbox?.CapacityFailure ?? Volatile.Read(ref connectionFailure)
        ?? brokerStreams.Values.FirstOrDefault(value => value.Error is not null)?.Error ?? publicationFailures.Values.FirstOrDefault()
        ?? mailboxes.Values.Select(value => Volatile.Read(ref value.Failure)).FirstOrDefault(value => value is not null);
    public ulong BrokerPendingMessages => mailboxes.Values.Aggregate(0UL, (count, mailbox) => count + mailbox.BrokerPending);
    public long BrokerUnacknowledgedMessages => mailboxes.Values.Sum(mailbox => (long)mailbox.BrokerUnacknowledged);

    sealed class Mailbox(ActorThreadId identity, string key, string stream, string subject)
    {
        public readonly ActorThreadId Identity = identity;
        public readonly string Key = key;
        public readonly string Stream = stream, Subject = subject;
        public readonly SemaphoreSlim Initialization = new(1, 1);
        public INatsJSConsumer? Consumer;
        public int Scheduled, TransportVersion, DelayScheduled;
        public long RetryNotBefore, PublishRetryNotBefore;
        public string? Failure;
        public long Version;
        public long Fetched;
        public bool PendingKnown;
        public bool SinglePhysicalSlotKnown;
        public ulong BrokerPending; public int BrokerUnacknowledged;
    }
    sealed class StreamTransport
    {
        public readonly RealtimeStreamFence Gate = new();
        public int Version;
    }
    StreamTransport Transport(Mailbox mailbox) => transports.GetOrAdd(StreamName(mailbox), _ => new());
    int CurrentTransportVersion(Mailbox mailbox) => Volatile.Read(ref Transport(mailbox).Version);
    sealed class Publication(CapturedRealtimeSource value)
    {
        public readonly CapturedRealtimeSource Value = value;
        public readonly ConcurrentDictionary<string, byte> Published = new(StringComparer.Ordinal);
        public readonly ConcurrentDictionary<string, byte> Delivered = new(StringComparer.Ordinal);
    }
    public async Task StartAsync(IActorSupervisor owner, CancellationToken token = default)
    {
        if (!Enabled) return;
        await startGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
            if (IsRunning) { if (!ReferenceEquals(supervisor, owner)) throw new InvalidOperationException("Realtime hub has another supervisor owner."); return; }
            options.Validate(); supervisor = owner;
            var schedulerType = owner.ThreadPool?.GetType();
            if (schedulerType?.Namespace is not { } schedulerNamespace || !(schedulerNamespace == "TomasAI.IFM.Application.EventModelActor"
                || schedulerNamespace.StartsWith("TomasAI.IFM.Application.EventModelActor.", StringComparison.Ordinal))) schedulerType = null;
            await RealtimeMailboxRuntimePreparation.PrepareAsync(schedulerType, token).ConfigureAwait(false);
            js = await connectionManager.GetJetStreamContextAsync(connectionOptions.Url, token).ConfigureAwait(false);
            outbox = new(options); VerifyOwnershipProtocol(); LoadRegistry();
            var actors = owner.Children.Keys.Where(identity => identity.ActorType == ActorType.Realtime)
                .Select(identity => identity.Name).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            if (actors.Length > options.MaximumMailboxes) throw new IOException("Realtime actor stream registry exceeds its configured capacity.");
            await Parallel.ForEachAsync(actors, new ParallelOptions { MaxDegreeOfParallelism = options.DeliveryConcurrency, CancellationToken = token },
                async (actor, ct) =>
                {
                    if (string.IsNullOrWhiteSpace(actor) || actor.Length > 256) throw new InvalidDataException("Invalid realtime actor stream name.");
                    var name = ActorStreamName(actor);
                    await streams.GetOrAdd(name, _ => new Lazy<Task<INatsJSStream>>(() => PrepareStreamAsync(actor, name, ct))).Value.WaitAsync(ct).ConfigureAwait(false);
                }).ConfigureAwait(false);
            confirmer = ConfirmSourcesAsync(stopping.Token);
            publisher = PublishPendingAsync(stopping.Token);
            workers = Enumerable.Range(0, options.DeliveryConcurrency).Select(_ => DeliverReadyAsync(stopping.Token)).ToArray();
            await Parallel.ForEachAsync(mailboxes.Values,new ParallelOptions{MaxDegreeOfParallelism=options.DeliveryConcurrency,CancellationToken=token},async(mailbox,ct)=>
            {
                await EnsureMailboxAsync(mailbox, ct).ConfigureAwait(false);
                // Create/update already returned fresh ConsumerInfo. Publish
                // scheduling handles messages arriving after that response.
                if (mailbox.Consumer!.Info.NumPending > 0 || mailbox.Consumer.Info.NumAckPending > 0) Schedule(mailbox);
            }).ConfigureAwait(false);
            monitor = MonitorBrokerAsync(stopping.Token);
            ready.TrySetResult();
        }
        catch (Exception error) { Metrics.Failure(error); ready.TrySetException(error); throw; }
        finally { startGate.Release(); }
    }
    public async ValueTask<RealtimeCaptureReceipt> CaptureAsync(RealtimeSourcePublication source, CancellationToken cancellationToken = default)
    {
        if (!Enabled) throw new InvalidOperationException("Durable realtime capture is disabled.");
        await ready.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        var started = Stopwatch.GetTimestamp();
        try
        {
            var capture = FreezeSource(source);
            var receipt = await outbox!.CaptureAsync(capture, cancellationToken).ConfigureAwait(false);
            sourceReady.Writer.TryWrite(true);
            Metrics.Captured(Stopwatch.GetElapsedTime(started).TotalMilliseconds); return receipt;
        }
        catch (Exception error) { Metrics.Failure(error); throw; }
    }
    CapturedRealtimeSource FreezeSource(RealtimeSourcePublication source)
    {
        var destinations = NatsActorConsumer.BuildPubSubDestinations(supervisor, ActorType.Realtime, source.Subject).ToArray();
        if (FuturesItiOrderedIngress.HasTradeConsumer)
            destinations = destinations.Where(d => d.Name != "FuturesItiSignalRealtime").ToArray();
        var orderedReplay = source.Subject.Verb == "TradeReplayBatch" && FuturesItiOrderedIngress.HasReplayConsumer;
        if (orderedReplay) destinations = [];
        if (destinations.Length == 0 && !orderedReplay) throw new InvalidOperationException("Realtime publication has no registered required destination.");
        foreach (var destination in destinations) GetMailbox(destination.ThreadId);
        return RealtimeMailboxCodec.Capture(source, destinations, options.MaximumPayloadBytes);
    }
    public async ValueTask<IReadOnlyList<RealtimeCaptureReceipt>> CaptureBatchAsync(IReadOnlyList<RealtimeSourcePublication> sources, CancellationToken cancellationToken = default)
    {
        if (!Enabled) throw new InvalidOperationException("Durable realtime capture is disabled.");
        if (sources.Count is < 1 or > 64) throw new InvalidDataException("Realtime source batch exceeds bounds.");
        await ready.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        var started = Stopwatch.GetTimestamp();
        var captures = sources.Select(FreezeSource).ToArray();
        var receipts = await outbox!.CaptureBatchAsync(captures, cancellationToken).ConfigureAwait(false);
        sourceReady.Writer.TryWrite(true);
        Metrics.Captured(Stopwatch.GetElapsedTime(started).TotalMilliseconds, receipts.Count);
        return receipts;
    }
    public async ValueTask<IReadOnlyList<RealtimePendingGeneration>> GetPendingGenerationsAsync(CancellationToken cancellationToken = default)
    {
        if (!Enabled) return Array.Empty<RealtimePendingGeneration>();
        await ready.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        return outbox!.PendingGenerations();
    }
    public async ValueTask WaitForDrainAsync(string dataset, Guid generation, CancellationToken cancellationToken = default)
    {
        if (!Enabled || generation == Guid.Empty) return;
        await ready.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        while (outbox!.PendingGenerationCount(dataset, generation) > 0)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
            if (outbox.Failure is { } error) throw new IOException("Generation drain source journal unavailable.", error);
            await Task.Delay(10, cancellationToken).ConfigureAwait(false);
        }
    }
    Mailbox GetMailbox(ActorThreadId identity)
    {
        if (mailboxesByIdentity.TryGetValue(identity, out var cached)) return cached;
        var key = RealtimeMailboxCodec.MailboxKey(identity);
        if (mailboxes.TryGetValue(key, out var found))
        { if (found.Identity != identity) throw new InvalidDataException("Realtime mailbox digest/identity conflict."); mailboxesByIdentity.TryAdd(identity, found); return found; }
        lock (registryGate)
        {
            if (mailboxes.TryGetValue(key, out found)) { mailboxesByIdentity.TryAdd(identity, found); return found; }
            if (mailboxes.Count >= options.MaximumMailboxes) throw new IOException("Realtime mailbox registry capacity exhausted.");
            var actorKey = actorKeys.GetOrAdd(identity.Name, RealtimeMailboxCodec.ActorKey);
            var created = new Mailbox(identity, key, options.StreamPrefix + "_" + options.Epoch + "_" + actorKey,
                options.StreamPrefix + "." + options.Epoch + "." + actorKey + "." + key);
            mailboxes.TryAdd(key, created); mailboxesByIdentity.TryAdd(identity, created); return created;
        }
    }
    static string StreamName(Mailbox mailbox) => mailbox.Stream;
    string ActorStreamName(string actor) => options.StreamPrefix + "_" + options.Epoch + "_" + actorKeys.GetOrAdd(actor, RealtimeMailboxCodec.ActorKey);
    static string Subject(Mailbox mailbox) => mailbox.Subject;
    bool IsBatched(Mailbox mailbox) => supervisor.Children.GetValueOrDefault(mailbox.Identity.MailboxId) is IBatchedRealtimeActor;
    async Task EnsureMailboxAsync(Mailbox mailbox, CancellationToken token)
    {
        using var setup = CancellationTokenSource.CreateLinkedTokenSource(token, stopping.Token);
        setup.CancelAfter(options.PublishTimeout);
        token = setup.Token;
        var transport = Transport(mailbox);
        if (mailbox.Consumer is not null && Volatile.Read(ref mailbox.TransportVersion) == Volatile.Read(ref transport.Version)) return;
        var preparationStarted = Stopwatch.GetTimestamp();
        await mailbox.Initialization.WaitAsync(token).ConfigureAwait(false);
        try
        {
            int version;
            Lazy<Task<INatsJSStream>> initialization;
            await using (var fence = await transport.Gate.EnterReadAsync(token).ConfigureAwait(false))
            {
                if (mailbox.Consumer is not null && mailbox.TransportVersion == transport.Version) return;
                version = transport.Version;
                initialization = streams.GetOrAdd(StreamName(mailbox), _ => new Lazy<Task<INatsJSStream>>(() => PrepareStreamAsync(mailbox, token)));
            }
            // Independent mailbox setup must not serialize broker RPCs behind
            // the actor-wide incarnation fence. Install only into the exact
            // version captured above; a reset discards this stale result.
            await initialization.Value.WaitAsync(token).ConfigureAwait(false);
            var consumer = await js.CreateOrUpdateConsumerAsync(StreamName(mailbox), new ConsumerConfig("m_" + mailbox.Key)
            {
                FilterSubject = Subject(mailbox), AckPolicy = ConsumerConfigAckPolicy.Explicit, DeliverPolicy = ConsumerConfigDeliverPolicy.All,
                MaxAckPending = 1, MaxDeliver = -1, AckWait = TimeSpan.FromSeconds(30)
            }, token).ConfigureAwait(false);
            await using (var fence = await transport.Gate.EnterReadAsync(token).ConfigureAwait(false))
            {
                if (version != transport.Version) throw new IOException("Broker incarnation changed during mailbox preparation.");
                mailbox.BrokerPending = consumer.Info.NumPending; mailbox.BrokerUnacknowledged = consumer.Info.NumAckPending;
                mailbox.SinglePhysicalSlotKnown = consumer.Info.Config.MaxAckPending == 1 && consumer.Info.NumAckPending <= 1;
                SaveRegistry(mailbox); mailbox.TransportVersion = version; mailbox.Consumer = consumer;
            }
        }
        finally { mailbox.Initialization.Release(); Metrics.Prepared(preparationStarted); }
    }
    Task<INatsJSStream> PrepareStreamAsync(Mailbox mailbox, CancellationToken token) => PrepareStreamAsync(mailbox.Identity.Name, mailbox.Stream, token);
    async Task<INatsJSStream> PrepareStreamAsync(string actor, string name, CancellationToken token)
    {
        var config = new StreamConfig(name, [options.StreamPrefix + "." + options.Epoch + "." + RealtimeMailboxCodec.ActorKey(actor) + ".>"])
        {
            Storage = options.Storage == RealtimeMailboxStorage.Memory ? StreamConfigStorage.Memory : StreamConfigStorage.File,
            Retention = StreamConfigRetention.Workqueue, Discard = StreamConfigDiscard.New, DiscardNewPerSubject = true,
            MaxMsgsPerSubject = options.MaximumMessagesPerMailbox, MaxBytes = options.MaximumActorBytes,
            MaxMsgSize = options.MaximumPayloadBytes + 65536, NumReplicas = 1,
            Description = "IFM realtime source owner " + OwnerIdentity()
        };
        // Existing stream creation is idempotent only for the exact same storage and bounds. Never mutate a live storage backend.
        INatsJSStream stream;
        try
        {
            stream = registeredStreams.ContainsKey(name)
                ? await js.GetStreamAsync(name, cancellationToken: token).ConfigureAwait(false)
                : await js.CreateStreamAsync(config, token).ConfigureAwait(false);
        }
        catch (NatsJSApiException error) when (error.Error.Code == 404)
        {
            // Protocol v2 retains every uncompleted recipient body on local durable storage.
            // Missing broker memory can therefore be rebuilt; other errors must never authorize replacement.
            stream = await js.CreateStreamAsync(config, token).ConfigureAwait(false);
        }
        var actual = stream.Info.Config;
        if (actual.Description != config.Description
            || actual.Storage != config.Storage || actual.MaxMsgsPerSubject != config.MaxMsgsPerSubject
            || actual.MaxBytes != config.MaxBytes || actual.MaxMsgSize != config.MaxMsgSize
            || actual.Retention != config.Retention || actual.Discard != config.Discard)
            throw new InvalidOperationException("Realtime stream belongs to another source-journal owner or has incompatible retention/bounds.");
        return stream;
    }
    string OwnerIdentity() => Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(
        Encoding.UTF8.GetBytes(Environment.MachineName + "|" + Path.GetFullPath(options.Directory).ToUpperInvariant())));
    async Task MonitorBrokerAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var probe = CancellationTokenSource.CreateLinkedTokenSource(token); probe.CancelAfter(options.PublishTimeout);
                    await connectionManager.ProbeAsync(probe.Token).ConfigureAwait(false); Volatile.Write(ref connectionFailure, null);
                }
                catch (Exception error) when (!token.IsCancellationRequested)
                { Volatile.Write(ref connectionFailure, error.Message); Metrics.Failure(error); }
                foreach (var stream in streams.Values.Where(value => value.IsValueCreated && value.Value.IsCompletedSuccessfully).Select(value => value.Value.Result))
                {
                    var streamTransport = transports.GetOrAdd(stream.Info.Config.Name, _ => new());
                    var streamVersion = Volatile.Read(ref streamTransport.Version);
                    try
                    {
                        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(options.PublishTimeout);
                        await stream.RefreshAsync(cancellationToken: timeout.Token).ConfigureAwait(false);
                        var state = stream.Info.State;
                        brokerStreams[stream.Info.Config.Name] = new(stream.Info.Config.Name, checked((long)state.Bytes),
                            checked((long)state.Messages), DateTimeOffset.TryParse(state.FirstTs, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out var oldest) ? oldest : default, DateTimeOffset.UtcNow, null);
                    }
                    catch (Exception error) when (!token.IsCancellationRequested)
                    {
                        if (error is NatsJSApiException api && api.Error.Code == 404)
                        {
                            try { await ResetStreamAsync(stream.Info.Config.Name, token, streamVersion).ConfigureAwait(false); }
                            catch (Exception resetError) when (!token.IsCancellationRequested) { Metrics.Failure(resetError); }
                        }
                        Metrics.Failure(error);
                        var name = stream.Info.Config.Name;
                        brokerStreams.AddOrUpdate(name, _ => new(name, 0, 0, default, DateTimeOffset.UtcNow, error.Message),
                            (_, previous) => previous with { Error = error.Message });
                    }
                }
                await Task.Delay(TimeSpan.FromSeconds(1), token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }
    async Task PublishPendingAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var pending = outbox!.Pending();
                if (pending.Length == 0) { await WaitForSourceChangeAsync(token).ConfigureAwait(false); continue; }
                var byMailbox = new Dictionary<string, List<(Publication Source, ActorSubject Target)>>(StringComparer.Ordinal);
                foreach (var captured in pending)
                {
                    var publication = publications.GetOrAdd(captured.Key, _ => new(captured));
                    foreach (var target in captured.Destinations)
                    {
                        var mailbox = GetMailbox(target.ThreadId);
                        if (outbox.IsDestinationCompleted(captured, mailbox.Key)) { publication.Delivered.TryAdd(mailbox.Key, 0); continue; }
                        if (publication.Published.ContainsKey(mailbox.Key) || Environment.TickCount64 < Interlocked.Read(ref mailbox.PublishRetryNotBefore)) continue;
                        if (!byMailbox.TryGetValue(mailbox.Key, out var list)) byMailbox.Add(mailbox.Key, list = []);
                        if (list.Count < options.CaptureBatchSize) list.Add((publication, target));
                    }
                }
                await Parallel.ForEachAsync(byMailbox, new ParallelOptions { MaxDegreeOfParallelism = options.DeliveryConcurrency, CancellationToken = token }, async (pair, ct) =>
                {
                    var mailbox = mailboxes[pair.Key];
                    try
                    {
                        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(options.PublishTimeout);
                        await EnsureMailboxAsync(mailbox, timeout.Token).ConfigureAwait(false);
                        var transportVersion = (await SnapshotTransportAsync(mailbox, timeout.Token).ConfigureAwait(false)).Version;
                        foreach (var group in PublicationGroups(pair.Value, IsBatched(mailbox)))
                        {
                            // One ordered publish/PubAck for this bounded physical group.
                            // Every original source still owns its own handler receipt.
                            (await js.PublishAsync(Subject(mailbox), group.Wire, serializer: Serializer,
                                opts: new NatsJSPubOpts { MsgId = (group.Members.Length == 1 ? group.Members[0].Source.Value.Key
                                    : Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(group.Wire))) + "." + mailbox.Key }, cancellationToken: timeout.Token).ConfigureAwait(false)).EnsureSuccess();
                            var streamTransport = Transport(mailbox);
                            await using (var fence = await streamTransport.Gate.EnterReadAsync(timeout.Token).ConfigureAwait(false))
                            {
                                if (transportVersion != streamTransport.Version)
                                    throw new IOException("Broker generation changed during publication; retained source will republish.");
                                publicationFailures.TryRemove(mailbox.Key, out _);
                                foreach (var (source, _) in group.Members) { source.Published.TryAdd(mailbox.Key, 0); Metrics.Published(); }
                                Interlocked.Increment(ref mailbox.Version); Schedule(mailbox);
                            }
                        }
                    }
                    catch (Exception error) when (!ct.IsCancellationRequested)
                    {
                        // Stop this destination at its first failure; unrelated mailbox publishers continue.
                        Interlocked.Exchange(ref mailbox.PublishRetryNotBefore, Environment.TickCount64 + (long)Math.Ceiling(options.FailureRedeliveryDelay.TotalMilliseconds));
                        publicationFailures[mailbox.Key] = error.Message; Metrics.Failure(error);
                        if (streams.TryGetValue(StreamName(mailbox), out var initialization)
                            && initialization.IsValueCreated && initialization.Value.IsCompleted && !initialization.Value.IsCompletedSuccessfully)
                            streams.TryRemove(StreamName(mailbox), out _);
                    }
                }).ConfigureAwait(false);
                foreach (var value in pending)
                {
                    if (value.Destinations.Length != 0 && (!publications.TryGetValue(value.Key, out var publication)
                        || publication.Delivered.Count != value.Destinations.Length)) continue;
                    if (!confirming.TryAdd(value.Key, 0)) continue;
                    if (!confirmations.Writer.TryWrite(value)) confirming.TryRemove(value.Key, out _);
                }
                if (byMailbox.Count == 0) await WaitForSourceChangeAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
            catch (Exception error) { Metrics.Failure(error); await Task.Delay(options.FailureRedeliveryDelay, token).ConfigureAwait(false); }
        }
    }
    async Task ConfirmSourcesAsync(CancellationToken token)
    {
        var batch = new List<CapturedRealtimeSource>(64);
        try
        {
            while (await confirmations.Reader.WaitToReadAsync(token).ConfigureAwait(false))
            {
                batch.Clear();
                while (batch.Count < 64 && confirmations.Reader.TryRead(out var value)) batch.Add(value);
                // Full-source bookkeeping has its own bounded pump. Publishing
                // and handler proof collection must not wait on one receipt
                // fsync at a time; the source remains owned until this flush.
                try
                {
                    if (BeforeSourceConfirmationAsync is { } hook)
                        await Task.WhenAll(batch.Select(value => hook(value.Source.SourceEventId, token).AsTask())).WaitAsync(token).ConfigureAwait(false);
                    await outbox!.ConfirmBrokerOwnershipBatchAsync(batch, token).ConfigureAwait(false);
                    foreach (var value in batch)
                    { publications.TryRemove(value.Key, out _); publicationFailures.TryRemove("source:" + value.Key, out _); }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { }
                catch (Exception error)
                {
                    foreach (var value in batch) publicationFailures["source:" + value.Key] = error.Message;
                    Metrics.Failure(error);
                    try { await Task.Delay(options.FailureRedeliveryDelay, token).ConfigureAwait(false); }
                    catch (OperationCanceledException) when (token.IsCancellationRequested) { }
                }
                finally
                {
                    foreach (var value in batch) confirming.TryRemove(value.Key, out _);
                    sourceReady.Writer.TryWrite(true);
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }
    IEnumerable<(byte[] Wire, (Publication Source, ActorSubject Target)[] Members)> PublicationGroups(
        List<(Publication Source, ActorSubject Target)> sources, bool batched)
    {
        var maximum = checked(options.MaximumPayloadBytes + 65536);
        var wires = new List<byte[]>(64); var members = new List<(Publication, ActorSubject)>(64); var bytes = 64;
        foreach (var member in sources)
        {
            var wire = RealtimeMailboxCodec.Delivery(member.Source.Value, member.Target);
            if (wires.Count > 0 && (!batched || wires.Count == 64 || bytes + 4L + wire.Length > maximum))
            {
                yield return (wires.Count == 1 ? wires[0] : RealtimeMailboxDeliveryBatchCodec.Encode(wires, maximum), members.ToArray());
                wires.Clear(); members.Clear(); bytes = 64;
            }
            wires.Add(wire); members.Add(member); bytes = checked(bytes + 4 + wire.Length);
        }
        if (wires.Count > 0) yield return (wires.Count == 1 ? wires[0] : RealtimeMailboxDeliveryBatchCodec.Encode(wires, maximum), members.ToArray());
    }
    async Task WaitForSourceChangeAsync(CancellationToken token)
    {
        var now = Environment.TickCount64;
        var retry = mailboxes.Values.Select(mailbox => Interlocked.Read(ref mailbox.PublishRetryNotBefore))
            .Where(at => at > now).DefaultIfEmpty(long.MaxValue).Min();
        if (retry == long.MaxValue) { await sourceReady.Reader.ReadAsync(token).ConfigureAwait(false); return; }
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromMilliseconds(retry - now));
        try { await sourceReady.Reader.ReadAsync(deadline.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { }
    }
    void Schedule(Mailbox mailbox)
    {
        if (Environment.TickCount64 < Interlocked.Read(ref mailbox.RetryNotBefore))
        {
            if (Interlocked.CompareExchange(ref mailbox.DelayScheduled, 1, 0) == 0) _ = ScheduleDeferredAsync(mailbox, stopping.Token);
            return;
        }
        if (Interlocked.CompareExchange(ref mailbox.Scheduled, 1, 0) == 0 && !runnable.Writer.TryWrite(mailbox))
        { Volatile.Write(ref mailbox.Scheduled, 0); Metrics.Failure(new IOException("Realtime ready registry unexpectedly full.")); }
    }
    async Task DeliverReadyAsync(CancellationToken token)
    {
        try
        {
            await foreach (var mailbox in runnable.Reader.ReadAllAsync(token).ConfigureAwait(false))
            {
                var retry = false; var moreWork = false; var version = Interlocked.Read(ref mailbox.Version);
                var fetched = Interlocked.Read(ref mailbox.Fetched);
                var transportVersion = CurrentTransportVersion(mailbox);
                try
                {
                    await EnsureMailboxAsync(mailbox, token).ConfigureAwait(false);
                    var transport = await SnapshotTransportAsync(mailbox, token).ConfigureAwait(false);
                    transportVersion = transport.Version;
                    if (IsBatched(mailbox)) retry |= await DeliverBatchAsync(mailbox, transport.Consumer, transportVersion, token).ConfigureAwait(false);
                    else await foreach (var delivery in transport.Consumer.FetchAsync<byte[]>(new NatsJSFetchOpts { MaxMsgs = 1, Expires = TimeSpan.FromMilliseconds(1100) }, Serializer, token).ConfigureAwait(false))
                    {
                        ObserveDelivery(mailbox, delivery);
                        if (delivery.Data is null) throw new InvalidDataException("Realtime mailbox returned an empty envelope.");
                        var source = RealtimeMailboxCodec.Decode(delivery.Data, options.MaximumPayloadBytes);
                        var target = source.Destinations.SingleOrDefault(d => d.ThreadId == mailbox.Identity);
                        if (target == default) throw new InvalidDataException("Realtime broker envelope addressed to another mailbox.");
                        var outcome = BrokerDeliveryResult.Succeeded;
                        if (!outbox!.IsDestinationCompleted(source, mailbox.Key))
                        {
                            var actor = supervisor.Children.GetValueOrDefault(target.ActorId) ?? throw new InvalidOperationException("Realtime destination actor is unavailable.");
                            var message = new RealtimeBrokerActorMessage(source, target);
                            var admission = await actor.Mailbox.ThreadQueues.TryAdmitAsync(message, target, token).ConfigureAwait(false);
                            if (!admission.Accepted) { message.Dispose(); throw new IOException("Realtime in-flight delivery refused: " + admission.Reason); }
                            outcome = await message.Completion.WaitAsync(token).ConfigureAwait(false);
                            if (outcome == BrokerDeliveryResult.Succeeded)
                                await outbox.RecordDestinationCompletedAsync(source, mailbox.Key, token).ConfigureAwait(false);
                        }
                        if (outcome == BrokerDeliveryResult.Succeeded)
                        {
                            await SettlePhysicalAsync(mailbox, source, transportVersion, delivery, true, token).ConfigureAwait(false);
                            if (publications.TryGetValue(source.Key, out var captured)) captured.Delivered.TryAdd(mailbox.Key, 0);
                            sourceReady.Writer.TryWrite(true);
                        }
                        else { await SettlePhysicalAsync(mailbox, source, transportVersion, delivery, false, token).ConfigureAwait(false); retry = true; }
                        Volatile.Write(ref mailbox.Failure, outcome == BrokerDeliveryResult.Succeeded ? null : "Realtime delivery retained for " + outcome);
                        Metrics.Settled(outcome);
                    }
                    if (transportVersion == CurrentTransportVersion(mailbox))
                    {
                        // The broker's delivery metadata already supplies pending
                        // work. Publish-version fencing below covers arrivals
                        // after that snapshot. Empty/recovery pulls still refresh
                        // so abandoned unacknowledged work is never forgotten.
                        if (fetched == Interlocked.Read(ref mailbox.Fetched) || !mailbox.PendingKnown || !mailbox.SinglePhysicalSlotKnown)
                        {
                            await transport.Consumer.RefreshAsync(token).ConfigureAwait(false);
                            Interlocked.Increment(ref consumerRefreshCount);
                            mailbox.BrokerPending = transport.Consumer.Info.NumPending; mailbox.BrokerUnacknowledged = transport.Consumer.Info.NumAckPending;
                            mailbox.SinglePhysicalSlotKnown = transport.Consumer.Info.Config.MaxAckPending == 1 && transport.Consumer.Info.NumAckPending <= 1;
                        }
                        moreWork = mailbox.BrokerPending > 0 || mailbox.BrokerUnacknowledged > 0;
                    }
                    else { moreWork = true; sourceReady.Writer.TryWrite(true); }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { }
                catch (Exception error)
                {
                    mailbox.PendingKnown = false;
                    if (error is NatsJSApiException api && api.Error.Code == 404)
                    {
                        try { await ResetStreamAsync(StreamName(mailbox), token, transportVersion).ConfigureAwait(false); }
                        catch (Exception resetError) when (!token.IsCancellationRequested) { Metrics.Failure(resetError); }
                    }
                    Volatile.Write(ref mailbox.Failure, error.Message); Metrics.Failure(error); retry = true;
                }
                finally
                {
                    if (retry) Interlocked.Exchange(ref mailbox.RetryNotBefore, Environment.TickCount64 + (long)Math.Ceiling(options.FailureRedeliveryDelay.TotalMilliseconds));
                    else Interlocked.Exchange(ref mailbox.RetryNotBefore, 0);
                    Volatile.Write(ref mailbox.Scheduled, 0);
                    // A healthy backlog is runnable work, not a failed delivery.
                    // Empty, unchanged mailboxes remain unscheduled.
                    if (!token.IsCancellationRequested && (retry || moreWork || version != Interlocked.Read(ref mailbox.Version))) Schedule(mailbox);
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }
    static void ObserveDelivery(Mailbox mailbox, INatsJSMsg<byte[]> delivery)
    {
        if (delivery.Metadata is { } metadata)
        {
            if (metadata.Stream != mailbox.Stream || metadata.Consumer != "m_" + mailbox.Key)
                throw new InvalidDataException("Broker delivery metadata addresses another stream or consumer.");
            mailbox.PendingKnown = true; mailbox.BrokerPending = metadata.NumPending;
        }
        else mailbox.PendingKnown = false;
        mailbox.BrokerUnacknowledged = 1;
        Interlocked.Increment(ref mailbox.Fetched);
    }
    async Task<bool> DeliverBatchAsync(Mailbox mailbox, INatsJSConsumer consumer, int transportVersion, CancellationToken token)
    {
        var deliveries = new Dictionary<string, (List<INatsJSMsg<byte[]>> Deliveries, CapturedRealtimeSource Source, ActorSubject Target)>(StringComparer.Ordinal);
        var pullStarted = Stopwatch.GetTimestamp();
        await foreach (var delivery in consumer.FetchNoWaitAsync<byte[]>(new NatsJSFetchOpts
            // One physical group already holds up to 64 logical messages.
            // The SDK accepts a count OR a byte limit. The verified stream
            // MaxMsgSize and group decoder enforce bytes; fetch one slot.
            { MaxMsgs = 1, Expires = TimeSpan.FromMilliseconds(1100) }, Serializer, token).ConfigureAwait(false))
        {
            ObserveDelivery(mailbox, delivery);
            if (delivery.Data is null) throw new InvalidDataException("Empty broker batch payload.");
            INatsJSMsg<byte[]> physical = delivery;
            foreach (var wire in RealtimeMailboxDeliveryBatchCodec.Decode(delivery.Data, checked(options.MaximumPayloadBytes + 65536)))
            {
                var source = RealtimeMailboxCodec.Decode(wire, options.MaximumPayloadBytes);
                var target = source.Destinations.SingleOrDefault(value => value.ThreadId == mailbox.Identity);
                if (target == default) throw new InvalidDataException("Broker delivery group has invalid destination.");
                if (deliveries.TryGetValue(source.Key, out var existing))
                {
                    if (existing.Source.SourceHash != source.SourceHash || existing.Source.ManifestHash != source.ManifestHash)
                        throw new InvalidDataException("Broker duplicate has conflicting original bytes.");
                    existing.Deliveries.Add(physical);
                }
                else deliveries.Add(source.Key, (new List<INatsJSMsg<byte[]>> { physical }, source, target));
            }
        }
        Metrics.Pulled(pullStarted);
        if (deliveries.Count == 0) return false;
        var ordered = outbox!.OrderForDelivery(deliveries.Values.Select(value => value.Source));
        if (!outbox.CanDeliverOrdered(ordered, mailbox.Key))
        {
            Volatile.Write(ref mailbox.Failure, "Waiting for the earlier required source before batched delivery.");
            // A physical group may mix completed and unfinished members. NAK
            // the entire group until all members can run in source order.
            await SettleBatchPhysicalAsync(false).ConfigureAwait(false);
            return true;
        }
        var uncompleted = ordered.Where(source => !outbox.IsDestinationCompleted(source, mailbox.Key)).ToArray();
        var pending = uncompleted.Select(source => new RealtimeBrokerActorMessage(source, deliveries[source.Key].Target)).ToArray();
        if (pending.Length > 0)
        {
            var actor = supervisor.Children.GetValueOrDefault(mailbox.Identity.MailboxId) ?? throw new InvalidOperationException("Batched destination unavailable.");
            var batch = new RealtimeBrokerActorBatch(pending);
            var admitted = await actor.Mailbox.ThreadQueues.TryAdmitAsync(batch, batch.Subject, token).ConfigureAwait(false);
            if (!admitted.Accepted) { batch.Dispose(); throw new IOException("Batched in-flight delivery refused: " + admitted.Reason); }
            var outcomes = await Task.WhenAll(pending.Select(message => message.Completion)).WaitAsync(token).ConfigureAwait(false);
            if (outcomes.Any(result => result != BrokerDeliveryResult.Succeeded))
            {
                await SettleBatchPhysicalAsync(false).ConfigureAwait(false);
                foreach (var outcome in outcomes) Metrics.Settled(outcome);
                Volatile.Write(ref mailbox.Failure, "Batched delivery retained for ordered retry.");
                return true;
            }
            await Task.WhenAll(uncompleted.Select(source => outbox.RecordDestinationCompletedAsync(source, mailbox.Key, token).AsTask())).ConfigureAwait(false);
        }
        // All original member proofs were flushed above. Only now may any
        // physical group be ACKed; each wire message is settled exactly once.
        await SettleBatchPhysicalAsync(true).ConfigureAwait(false);
        foreach (var source in ordered)
        {
            if (publications.TryGetValue(source.Key, out var captured)) captured.Delivered.TryAdd(mailbox.Key, 0);
            Metrics.Settled(BrokerDeliveryResult.Succeeded);
        }
        Volatile.Write(ref mailbox.Failure, null); sourceReady.Writer.TryWrite(true);
        return false;

        async Task SettleBatchPhysicalAsync(bool acknowledge)
        {
            var seen = new HashSet<INatsJSMsg<byte[]>>(ReferenceEqualityComparer.Instance);
            foreach (var item in deliveries.Values)
                foreach (var physical in item.Deliveries)
                    if (seen.Add(physical))
                        await SettlePhysicalAsync(mailbox, item.Source, transportVersion, physical, acknowledge, token).ConfigureAwait(false);
        }
    }
    async Task ScheduleDeferredAsync(Mailbox mailbox, CancellationToken token)
    {
        try
        {
            long remaining;
            while ((remaining = Interlocked.Read(ref mailbox.RetryNotBefore) - Environment.TickCount64) > 0)
                await Task.Delay(TimeSpan.FromMilliseconds(remaining), token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        finally
        {
            Volatile.Write(ref mailbox.DelayScheduled, 0);
            if (!token.IsCancellationRequested) Schedule(mailbox);
        }
    }
    async ValueTask<(INatsJSConsumer Consumer, int Version)> SnapshotTransportAsync(Mailbox mailbox, CancellationToken token)
    {
        using var setup = CancellationTokenSource.CreateLinkedTokenSource(token, stopping.Token);
        setup.CancelAfter(options.PublishTimeout); token = setup.Token;
        await mailbox.Initialization.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var transport = Transport(mailbox);
            await using (var fence = await transport.Gate.EnterReadAsync(token).ConfigureAwait(false))
            {
                if (mailbox.Consumer is null || mailbox.TransportVersion != transport.Version)
                    throw new IOException("Realtime transport was reset before fetch.");
                return (mailbox.Consumer, transport.Version);
            }
        }
        finally { mailbox.Initialization.Release(); }
    }
    async ValueTask<bool> SettlePhysicalAsync(Mailbox mailbox, CapturedRealtimeSource source, int transportVersion,
        INatsJSMsg<byte[]> delivery, bool acknowledge, CancellationToken token)
    {
        var settlementStarted = Stopwatch.GetTimestamp();
        if (BeforePhysicalSettlementAsync is { } hook)
            await hook(mailbox.Identity, source.Source.SourceEventId, acknowledge, transportVersion, token).ConfigureAwait(false);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(options.PublishTimeout);
        await mailbox.Initialization.WaitAsync(timeout.Token).ConfigureAwait(false);
        try
        {
            var transport = Transport(mailbox);
            await using (var fence = await transport.Gate.EnterReadAsync(timeout.Token).ConfigureAwait(false))
            {
                if (transportVersion != transport.Version)
                {
                    // Handler proof/source ownership remain authoritative. An
                    // old physical sequence cannot settle a new incarnation.
                    Interlocked.Increment(ref suppressedPhysicalSettlements);
                    mailbox.PendingKnown = false;
                    sourceReady.Writer.TryWrite(true);
                    return false;
                }
                if (acknowledge) await delivery.AckAsync(new AckOpts { DoubleAck = true }, timeout.Token).ConfigureAwait(false);
                else await delivery.NakAsync(new AckOpts { NakDelay = options.FailureRedeliveryDelay }, timeout.Token).ConfigureAwait(false);
                mailbox.BrokerUnacknowledged = acknowledge ? 0 : 1;
                return true;
            }
        }
        finally { mailbox.Initialization.Release(); Metrics.PhysicallySettled(settlementStarted); }
    }
    async Task ResetStreamAsync(string name, CancellationToken token, int? expectedVersion = null)
    {
        // Stable across existing AND newly registered thread mailboxes sharing
        // this actor stream. Never wait for mailbox.Initialization under here;
        // every path needing both gates takes mailbox first, then stream.
        using var setup = CancellationTokenSource.CreateLinkedTokenSource(token, stopping.Token);
        setup.CancelAfter(options.PublishTimeout); token = setup.Token;
        var transport = transports.GetOrAdd(name, _ => new());
        await using (var fence = await transport.Gate.EnterWriteAsync(token).ConfigureAwait(false))
        {
            if (expectedVersion is { } expected && expected != transport.Version) return;
            Interlocked.Increment(ref transport.Version);
            streams.TryRemove(name, out _);
            foreach (var mailbox in mailboxes.Values.Where(value => StreamName(value) == name))
            {
                mailbox.Consumer = null;
                mailbox.PendingKnown = false; mailbox.SinglePhysicalSlotKnown = false;
                foreach (var source in publications.Values) source.Published.TryRemove(mailbox.Key, out _);
                Interlocked.Increment(ref mailbox.Version); Schedule(mailbox);
            }
        }
        sourceReady.Writer.TryWrite(true);
    }
    void VerifyOwnershipProtocol()
    {
        var root = Path.Combine(options.Directory, options.Epoch); var marker = Path.Combine(root, "ownership.protocol");
        if (File.Exists(Path.Combine(root, "archive-only.protocol")))
            throw new InvalidDataException("Capture-only archives cannot be activated as handler mailboxes.");
        if (File.Exists(marker))
        { if (File.ReadAllText(marker) != "2:source-until-handler-proof") throw new InvalidDataException("Unknown realtime ownership protocol."); return; }
        var registry = Path.Combine(root, "mailboxes");
        if (Directory.Exists(registry) && Directory.EnumerateFiles(registry, "*.rmb").Any())
            throw new InvalidDataException("Legacy realtime ownership requires explicit drain or receipt migration before broker reconstruction.");
        using var file = new FileStream(marker, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        file.Write(Encoding.UTF8.GetBytes("2:source-until-handler-proof")); file.Flush(true);
    }
    void SaveRegistry(Mailbox mailbox)
    {
        // Protocol2 source ownership is already durable, including this exact
        // destination. Per-mailbox fsync under the stream gate adds no recovery
        // authority. Keep only the transport's local discovery hint here.
        registeredStreams.TryAdd(StreamName(mailbox),0);
    }
    void LoadRegistry()
    {
        var root=Path.Combine(options.Directory,options.Epoch,"mailboxes");
        foreach(var identity in RealtimeMailboxRegistry.Recover(root,outbox!.Pending(),options.MaximumMailboxes))
        {
            var mailbox=GetMailbox(identity);registeredStreams.TryAdd(StreamName(mailbox),0);
        }
    }
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        await stopping.CancelAsync().ConfigureAwait(false);
        ready.TrySetCanceled(stopping.Token);
        runnable.Writer.TryComplete(); sourceReady.Writer.TryComplete(); confirmations.Writer.TryComplete();
        if (publisher is not null) { try { await publisher.ConfigureAwait(false); } catch (OperationCanceledException) { } }
        await Task.WhenAll(workers).ConfigureAwait(false);
        if (confirmer is not null) await confirmer.ConfigureAwait(false);
        if (monitor is not null) await monitor.ConfigureAwait(false);
        if (outbox is not null) await outbox.DisposeAsync().ConfigureAwait(false);
        stopping.Dispose(); GC.SuppressFinalize(this);
    }
}

public sealed record RealtimeBrokerStreamSnapshot(string Stream, long Bytes, long Messages, DateTimeOffset OldestMessageUtc, DateTimeOffset ObservedAtUtc, string? Error);
public sealed record RealtimeBrokerMailboxSnapshot(ActorThreadId Identity, ulong BrokerPending, int BrokerUnacknowledged, bool Scheduled, string? Failure);
