using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Threading.Channels;
using Cassandra;
using Microsoft.Extensions.DependencyInjection;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;
using TomasAI.IFM.Framework.MarketData.TickAggregation;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Application.Actor.IntegrationTests;

/// <summary>Runs bounded synthetic quote traffic through one realtime actor and its Scylla projection.</summary>
internal sealed class TickQuoteSoakRunner(IServiceProvider services)
{
    private readonly TickQuoteBufferPool _pool = CreatePool();
    private readonly ushort _capacityBatchSize = ReadCapacityBatchSize();
    private readonly DateOnly _valueDate = DateOnly.FromDateTime(DateTime.UtcNow);
    private readonly string[] _contracts =
    [
        "SOAK-ES-" + Guid.NewGuid().ToString("N"),
        "SOAK-ESOPT-" + Guid.NewGuid().ToString("N")
    ];
    private readonly List<double> _visibilityLagMs = [];
    private readonly long[] _assetSent = new long[2];
    private readonly long[] _assetQuoteItems = new long[2];
    private readonly bool _capacityProfile =
        Environment.GetEnvironmentVariable("IFM_TICK_QUOTE_SOAK_PROFILE") == "option-heavy-capacity";
    private long _outstandingLeases;
    private long _sent;

    private static ushort ReadCapacityBatchSize()
    {
        var configured = Environment.GetEnvironmentVariable("IFM_TICK_QUOTE_SOAK_BATCH_SIZE");
        if (string.IsNullOrWhiteSpace(configured))
            return FuturesTickQuoteDataSegment.MaximumCount;
        if (!ushort.TryParse(configured, CultureInfo.InvariantCulture, out var size)
            || size is 0 or > FuturesTickQuoteDataSegment.MaximumCount)
            throw new ArgumentOutOfRangeException(nameof(configured));
        return size;
    }

    private static TickQuoteBufferPool CreatePool()
    {
        if (Environment.GetEnvironmentVariable("IFM_TICK_QUOTE_SOAK_PROFILE")
                == "option-heavy-capacity"
            && ReadCapacityBatchSize() <= 512)
            return new TickQuoteBufferPool([(ReadCapacityBatchSize(), 16)]);
        return new TickQuoteBufferPool();
    }

    /// <summary>Runs the configured elapsed soak and writes minute samples and a final verdict.</summary>
    public async Task RunAsync(CancellationToken stopping)
    {
        var configured = Environment.GetEnvironmentVariable("IFM_TICK_QUOTE_SOAK_DURATION_MINUTES");
        var minutes = double.TryParse(configured, CultureInfo.InvariantCulture, out var parsed)
            ? parsed : 120;
        if (minutes is < 1 or > 180)
            throw new ArgumentOutOfRangeException(nameof(minutes));
        var total = TimeSpan.FromMinutes(minutes);
        var warmup = TimeSpan.FromTicks(total.Ticks / 8);
        var active = TimeSpan.FromTicks(total.Ticks * 7 / 8);
        var sampleInterval = TimeSpan.FromSeconds(Math.Min(60, total.TotalSeconds / 12));
        var outputDir = Environment.GetEnvironmentVariable("IFM_TICK_QUOTE_SOAK_OUTPUT")
            ?? Path.Combine(Directory.GetCurrentDirectory(), "SoakResults");
        Directory.CreateDirectory(outputDir);
        var runId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var csvPath = Path.Combine(outputDir, $"tick-quote-soak-{runId}.csv");
        var verdictPath = Path.Combine(outputDir, $"tick-quote-soak-{runId}.json");
        var startedUtc = DateTime.UtcNow;
        var started = Stopwatch.GetTimestamp();
        var supervisor = services.GetRequiredService<IActorSupervisor>();
        var actorId = new ActorMailboxId(ActorType.Realtime, FuturesTickQuoteDataChangedEvent.Actor);
        if (!supervisor.IsReady || !supervisor.ActorExists(actorId) || supervisor.Children.Count != 1)
            throw new InvalidOperationException("The isolated soak must own only the ready TickAggregation realtime actor.");

        await using var publisher = new TickAggregationEventPublisher(
            supervisor, policy: new RealtimeTickPublisherPolicy());
        using var cluster = Cluster.Builder().AddContactPoint("localhost").Build();
        using var session = cluster.Connect("market_data_test_db");
        var readStatement = session.Prepare(
            "SELECT quote_count FROM tick_quote_data WHERE asset_type_id=? AND contract_id=? AND value_date=? AND aggregation_time=? AND sequence_id=?;");
        var countStatement = session.Prepare(
            "SELECT COUNT(*) FROM tick_quote_data WHERE asset_type_id=? AND contract_id=? AND value_date=?;");
        var sentinels = Channel.CreateUnbounded<Sentinel>(new UnboundedChannelOptions
        {
            SingleWriter = true,
            SingleReader = true
        });
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(stopping);
        using var process = Process.GetCurrentProcess();
        await using var csv = new StreamWriter(csvPath);
        await csv.WriteLineAsync(
            "utc,phase,elapsed_s,sent,published,futures_quotes,option_quotes,depth,inflight,failed,rejected,expired,outstanding_leases,allocated_Bps,working_set_B,private_B,heap_B,loh_B,gen0_delta,gen1_delta,gen2_delta,live_managed_B,cql_idle_B,cql_new_arrays,decoder_overflow");

        Task generator = Task.CompletedTask;
        Task observer = Task.CompletedTask;
        Task sampler = Task.CompletedTask;
        string? failure = null;
        try
        {
            await publisher.StartAsync(cancellation.Token);
            Console.WriteLine($"SOAK START {startedUtc:O} duration={minutes:F1}min actor={actorId} profile={(_capacityProfile ? "option-heavy-capacity" : "legacy")} activeBatch={(_capacityProfile ? _capacityBatchSize : 64)} hardLimit={FuturesTickQuoteDataSegment.MaximumCount} csv={csvPath}");
            generator = GenerateAsync(publisher, sentinels.Writer, started, warmup, active, total, cancellation.Token);
            observer = ObserveAsync(session, readStatement, sentinels.Reader, cancellation.Token);
            sampler = SampleAsync(publisher, csv, process, started, warmup, active, total,
                sampleInterval, cancellation.Token);

            var first = await Task.WhenAny(generator, observer, sampler).ConfigureAwait(false);
            await first.ConfigureAwait(false);
            if (first != generator)
                throw new InvalidOperationException("A soak observer or sampler ended before traffic generation completed.");
            await publisher.StopAsync(cancellation.Token).ConfigureAwait(false);
            await observer.ConfigureAwait(false);
            await sampler.ConfigureAwait(false);

            var snapshot = publisher.GetSnapshot();
            if (snapshot.Failed != 0 || snapshot.Rejected != 0 || snapshot.Expired != 0
                || snapshot.ShutdownDiscarded != 0 || snapshot.Published != Interlocked.Read(ref _sent)
                || snapshot.Depth != 0 || Interlocked.Read(ref _outstandingLeases) != 0)
                throw new InvalidOperationException(
                    $"Publisher did not drain cleanly: sent={_sent}, published={snapshot.Published}, "
                    + $"depth={snapshot.Depth}, failed={snapshot.Failed}, rejected={snapshot.Rejected}, "
                    + $"expired={snapshot.Expired}, discarded={snapshot.ShutdownDiscarded}, leases={_outstandingLeases}.");
            for (var asset = 0; asset < 2; asset++)
            {
                var rows = await CountRowsAsync(session, countStatement, asset).ConfigureAwait(false);
                if (rows != Interlocked.Read(ref _assetSent[asset]))
                    throw new InvalidOperationException(
                        $"Stored {rows} {Asset(asset)} rows but accepted {_assetSent[asset]} quote batches.");
            }
        }
        catch (Exception exception)
        {
            failure = exception.ToString();
            await cancellation.CancelAsync().ConfigureAwait(false);
            sentinels.Writer.TryComplete();
            try { await Task.WhenAll(generator, observer, sampler).ConfigureAwait(false); }
            catch { /* The original failure is reported below. */ }
            try { await publisher.StopAsync().ConfigureAwait(false); }
            catch { /* Preserve the first failure and publisher diagnostics. */ }
        }

        await csv.FlushAsync().ConfigureAwait(false);
        _visibilityLagMs.Sort();
        var final = publisher.GetSnapshot();
        var managedBeforeDiagnosticGc = GC.GetTotalMemory(false);
        long? managedAfterDiagnosticGc = null;
        if (Environment.GetEnvironmentVariable("IFM_TICK_QUOTE_SOAK_FINAL_GC") == "true")
        {
            GC.Collect(2, GCCollectionMode.Forced, blocking: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Forced, blocking: true);
            managedAfterDiagnosticGc = GC.GetTotalMemory(false);
        }
        var verdict = new
        {
            Status = failure is null ? "Passed" : "Failed",
            Failure = failure,
            StartedUtc = startedUtc,
            EndedUtc = DateTime.UtcNow,
            PlannedDurationMinutes = minutes,
            ElapsedMinutes = Stopwatch.GetElapsedTime(started).TotalMinutes,
            RawCqlEnabled = true,
            Profile = _capacityProfile ? "option-heavy-capacity" : "legacy",
            QuoteLimit = FuturesTickQuoteDataSegment.MaximumCount,
            ActiveBatchSize = _capacityProfile ? _capacityBatchSize : (ushort)64,
            FuturesContractId = _contracts[0],
            FuturesOptionContractId = _contracts[1],
            SentBatches = Interlocked.Read(ref _sent),
            FuturesBatches = Interlocked.Read(ref _assetSent[0]),
            FuturesOptionBatches = Interlocked.Read(ref _assetSent[1]),
            FuturesQuoteItems = Interlocked.Read(ref _assetQuoteItems[0]),
            FuturesOptionQuoteItems = Interlocked.Read(ref _assetQuoteItems[1]),
            Publisher = final,
            ManagedBeforeDiagnosticGcBytes = managedBeforeDiagnosticGc,
            ManagedAfterDiagnosticGcBytes = managedAfterDiagnosticGc,
            OutstandingLeases = Interlocked.Read(ref _outstandingLeases),
            VisibilitySamples = _visibilityLagMs.Count,
            VisibilityLagP50Ms = Percentile(0.50),
            VisibilityLagP95Ms = Percentile(0.95),
            VisibilityLagP99Ms = Percentile(0.99),
            CsvPath = csvPath
        };
        await File.WriteAllTextAsync(verdictPath,
            JsonSerializer.Serialize(verdict, new JsonSerializerOptions { WriteIndented = true })).ConfigureAwait(false);
        Console.WriteLine($"SOAK {verdict.Status.ToUpperInvariant()} elapsed={verdict.ElapsedMinutes:F1}min sent={verdict.SentBatches} published={final.Published} p95Visibility={verdict.VisibilityLagP95Ms:F1}ms leases={verdict.OutstandingLeases} verdict={verdictPath}");
        _pool.Dispose();
        if (failure is not null)
            throw new InvalidOperationException($"Tick quote soak failed; see {verdictPath}. {failure}");
    }

    private async Task GenerateAsync(
        TickAggregationEventPublisher publisher, ChannelWriter<Sentinel> sentinels,
        long started, TimeSpan warmup, TimeSpan active, TimeSpan total,
        CancellationToken cancellationToken)
    {
        var nextSentinel = TimeSpan.Zero;
        var capacityInterval = new[]
        {
            TimeSpan.FromSeconds(_capacityBatchSize / 200d),
            TimeSpan.FromSeconds(_capacityBatchSize / 2_000d)
        };
        var nextCapacityBatch = new[] { capacityInterval[0], capacityInterval[1] };
        try
        {
            while (Stopwatch.GetElapsedTime(started) < active)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var elapsed = Stopwatch.GetElapsedTime(started);
                var sustained = elapsed >= warmup;
                var sustainedElapsed = sustained ? elapsed - warmup : TimeSpan.Zero;
                var burstPeriod = TimeSpan.FromTicks(total.Ticks / 8);
                var burstWindow = TimeSpan.FromTicks(total.Ticks / 120);
                var burst = sustained && sustainedElapsed.Ticks % burstPeriod.Ticks < burstWindow.Ticks;
                var rate = sustained ? burst ? 64 : 8 : 1;
                var asset = _capacityProfile
                    ? nextCapacityBatch[0] <= nextCapacityBatch[1] ? 0 : 1
                    : (int)(Interlocked.Read(ref _sent) & 1);
                if (_capacityProfile && elapsed < nextCapacityBatch[asset])
                {
                    await Task.Delay(TimeSpan.FromTicks(Math.Min(
                        (nextCapacityBatch[asset] - elapsed).Ticks, TimeSpan.FromMilliseconds(50).Ticks)),
                        cancellationToken).ConfigureAwait(false);
                    continue;
                }
                var sequence = Interlocked.Read(ref _assetSent[asset]) + 1;
                var quoteCount = _capacityProfile
                    ? _capacityBatchSize
                    : (ushort)((sequence % 3) switch { 0 => 1, 1 => 32, _ => 64 });
                var lease = new CountingLease(this,
                    await _pool.RentAsync(_capacityProfile ? _capacityBatchSize : (ushort)64,
                        cancellationToken).ConfigureAwait(false));
                try
                {
                    var timestamp = DateTime.UtcNow;
                    var timestampNs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000_000;
                    for (var index = 0; index < quoteCount; index++)
                    {
                        var bid = 5_000m + (index % 20) / 4m;
                        lease.Buffer[index] = new FuturesTickQuoteData(
                            (uint)sequence, timestampNs, timestampNs,
                            (byte)(index % 8), decimal.ToInt64(bid * 1_000_000_000m),
                            index % 5 == 0 ? null : bid,
                            (uint)(10 + index), 1,
                            decimal.ToInt64((bid + 0.25m) * 1_000_000_000m),
                            index % 7 == 0 ? null : bid + 0.25m,
                            (uint)(11 + index), 1);
                    }
                    try { lease.SetCount(quoteCount); }
                    catch (ArgumentOutOfRangeException exception)
                    {
                        throw new InvalidOperationException(
                            $"Quote lease rejected count={quoteCount}, bufferLength={lease.Buffer.Length}, sequence={sequence}.",
                            exception);
                    }
                    var entity = new TickDataEntityId(_contracts[asset], _valueDate, Asset(asset));
                    var @event = new FuturesTickQuoteDataChangedEvent
                    {
                        Subject = new ActorSubject(ActorType.Realtime,
                            FuturesTickQuoteDataChangedEvent.Actor,
                            FuturesTickQuoteDataChangedEvent.Verb, entity.Format()),
                        Id = Guid.NewGuid(),
                        CommandId = Guid.NewGuid(),
                        EntityId = entity,
                        AggregateId = entity.Format(),
                        EventSource = nameof(TickQuoteSoakRunner),
                        ReceivedOn = timestamp,
                        TickDataId = new TickDataId(entity.ContractId, _valueDate, sequence, timestamp),
                        AssetTypeId = entity.AssetTypeId,
                        Dataset = "GLBX.MDP3",
                        DefinitionDate = _valueDate,
                        PublisherId = 1,
                        InstrumentId = (uint)(asset == 0 ? 101 : 102),
                        EmissionReason = QuoteEmissionReason.BufferFull,
                        QuoteCount = quoteCount,
                        QuoteData = new FuturesTickQuoteDataSegment(lease.Buffer, quoteCount)
                    };
                    var sentAt = Stopwatch.GetTimestamp();
                    await publisher.PublishAsync(@event, lease, cancellationToken).ConfigureAwait(false);
                    Interlocked.Increment(ref _assetSent[asset]);
                    Interlocked.Increment(ref _sent);
                    Interlocked.Add(ref _assetQuoteItems[asset], quoteCount);
                    var sentinel = new Sentinel(asset, sequence, timestamp, quoteCount, sentAt);
                    if (elapsed >= nextSentinel)
                    {
                        sentinels.TryWrite(sentinel);
                        nextSentinel = elapsed + TimeSpan.FromSeconds(10);
                    }
                }
                catch { lease.Dispose(); throw; }
                if (_capacityProfile)
                    nextCapacityBatch[asset] += capacityInterval[asset];
                else
                    await Task.Delay(TimeSpan.FromSeconds(1d / rate), cancellationToken).ConfigureAwait(false);
            }
        }
        finally { sentinels.TryComplete(); }
    }

    private async Task ObserveAsync(ISession session, PreparedStatement statement,
        ChannelReader<Sentinel> sentinels, CancellationToken cancellationToken)
    {
        await foreach (var sentinel in sentinels.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            if (Environment.GetEnvironmentVariable("IFM_TICK_QUOTE_SOAK_INJECT_READBACK_FAILURE") == "true")
                throw new InvalidOperationException("Injected readback failure to verify fail-fast supervision.");
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var rows = await session.ExecuteAsync(statement.Bind(
                    (sbyte)Asset(sentinel.Asset), _contracts[sentinel.Asset],
                    new LocalDate(_valueDate.Year, _valueDate.Month, _valueDate.Day),
                    new LocalTime(sentinel.Timestamp.Hour, sentinel.Timestamp.Minute,
                        sentinel.Timestamp.Second, sentinel.Timestamp.Millisecond * 1_000_000),
                    sentinel.Sequence)).ConfigureAwait(false);
                var found = rows.Any(row => row.GetValue<short>(0) == sentinel.Count);
                if (found)
                {
                    _visibilityLagMs.Add(Stopwatch.GetElapsedTime(sentinel.SentAt).TotalMilliseconds);
                    break;
                }
                if (Stopwatch.GetElapsedTime(sentinel.SentAt) > TimeSpan.FromSeconds(10))
                    throw new TimeoutException($"Quote row {sentinel.Asset}/{sentinel.Sequence} was not visible within ten seconds.");
                await Task.Delay(TimeSpan.FromMilliseconds(20), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task SampleAsync(TickAggregationEventPublisher publisher, StreamWriter csv,
        Process process, long started, TimeSpan warmup, TimeSpan active,
        TimeSpan total, TimeSpan interval, CancellationToken cancellationToken)
    {
        var prior = GC.GetTotalAllocatedBytes(false);
        var priorAt = Stopwatch.GetTimestamp();
        var gen0 = GC.CollectionCount(0);
        var gen1 = GC.CollectionCount(1);
        var gen2 = GC.CollectionCount(2);
        while (Stopwatch.GetElapsedTime(started) < total)
        {
            await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
            var elapsed = Stopwatch.GetElapsedTime(started);
            var snapshot = publisher.GetSnapshot();
            var allocated = GC.GetTotalAllocatedBytes(false);
            var at = Stopwatch.GetTimestamp();
            var seconds = Stopwatch.GetElapsedTime(priorAt, at).TotalSeconds;
            var info = GC.GetGCMemoryInfo();
            var loh = info.GenerationInfo.Length > 3 ? info.GenerationInfo[3].SizeAfterBytes : 0;
            var currentGen0 = GC.CollectionCount(0);
            var currentGen1 = GC.CollectionCount(1);
            var currentGen2 = GC.CollectionCount(2);
            process.Refresh();
            var phase = elapsed < warmup ? "warmup" : elapsed < active ? "sustained" : "drain";
            await csv.WriteLineAsync(FormattableString.Invariant(
                $"{DateTime.UtcNow:O},{phase},{elapsed.TotalSeconds:F1},{Interlocked.Read(ref _sent)},{snapshot.Published},{Interlocked.Read(ref _assetQuoteItems[0])},{Interlocked.Read(ref _assetQuoteItems[1])},{snapshot.Depth},{snapshot.InFlight},{snapshot.Failed},{snapshot.Rejected},{snapshot.Expired},{Interlocked.Read(ref _outstandingLeases)},{(allocated - prior) / seconds:F0},{process.WorkingSet64},{process.PrivateMemorySize64},{info.HeapSizeBytes},{loh},{currentGen0 - gen0},{currentGen1 - gen1},{currentGen2 - gen2},{GC.GetTotalMemory(false)},{PooledTickQuoteCqlBuffer.IdleBytes},{PooledTickQuoteCqlBuffer.NewArrays},{PooledQuoteSegmentBuffers.OverflowAllocations}"));
            await csv.FlushAsync().ConfigureAwait(false);
            Console.WriteLine(FormattableString.Invariant(
                $"SOAK {phase} {elapsed.TotalMinutes:F1}/{total.TotalMinutes:F1}min sent={_sent} published={snapshot.Published} depth={snapshot.Depth} failed={snapshot.Failed} leases={_outstandingLeases} alloc={(allocated - prior) / seconds / 1_000_000:F2}MB/s workingSet={process.WorkingSet64 / 1_000_000d:F1}MB G0/G1/G2={currentGen0 - gen0}/{currentGen1 - gen1}/{currentGen2 - gen2}"));
            if (snapshot.Faulted || snapshot.Failed != 0 || snapshot.Rejected != 0 || snapshot.Expired != 0)
                throw new InvalidOperationException($"Realtime publisher failed: {snapshot.Failure}: {snapshot.FailureDetail}");
            prior = allocated;
            priorAt = at;
            gen0 = currentGen0;
            gen1 = currentGen1;
            gen2 = currentGen2;
        }
    }

    private async Task<long> CountRowsAsync(ISession session, PreparedStatement statement, int asset)
    {
        using var rows = await session.ExecuteAsync(statement.Bind(
            (sbyte)Asset(asset), _contracts[asset],
            new LocalDate(_valueDate.Year, _valueDate.Month, _valueDate.Day))).ConfigureAwait(false);
        return rows.Single().GetValue<long>(0);
    }

    private double Percentile(double fraction)
        => _visibilityLagMs.Count == 0 ? 0 :
            _visibilityLagMs[(int)Math.Ceiling(_visibilityLagMs.Count * fraction) - 1];

    private static AssetTypeId Asset(int index)
        => index == 0 ? AssetTypeId.Futures : AssetTypeId.FuturesOption;

    private sealed record Sentinel(int Asset, long Sequence, DateTime Timestamp, ushort Count, long SentAt);

    private sealed class CountingLease : ITickQuoteBufferLease
    {
        private readonly TickQuoteSoakRunner _owner;
        private readonly ITickQuoteBufferLease _inner;
        private int _disposed;

        internal CountingLease(TickQuoteSoakRunner owner, ITickQuoteBufferLease inner)
        {
            _owner = owner;
            _inner = inner;
            Interlocked.Increment(ref owner._outstandingLeases);
        }

        public FuturesTickQuoteData[] Buffer => _inner.Buffer;
        public ushort Count => _inner.Count;
        public void SetCount(ushort count) => _inner.SetCount(count);
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            _inner.Dispose();
            Interlocked.Decrement(ref _owner._outstandingLeases);
        }
    }
}
