using System.Buffers;
using System.Collections.Immutable;
using System.Diagnostics;
using MessagePack;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Framework.Serialization;
using Xunit.Abstractions;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class WorkerSnapshotAllocationTests(ITestOutputHelper output)
{
    const int Maximum = 8 * 1024 * 1024;

    static async Task<DatasetWorkerControlFrame> Frame(int count = 256)
    {
        var snapshot = await OptionChainCacheTests.Snapshot();
        var template = snapshot.Instruments[0];
        var pricing = template.Instrument.Pricing!;
        var calendar = pricing.Calendar with
        {
            TradingDates = Enumerable.Range(0, 365).Select(day => pricing.Calendar.CoverageFrom.AddDays(day)).ToImmutableArray()
        };
        var rows = Enumerable.Range(0, count).Select(i => template with
        {
            Instrument = template.Instrument with
            {
                ContractId = $"ES-option-{i}",
                Pricing = pricing with { Calendar = calendar, Contract = pricing.Contract with { ContractId = $"ES-option-{i}" } }
            }
        }).ToImmutableArray();
        return new()
        {
            Kind = DatasetWorkerMessageKind.CompositionSnapshotResult,
            WorkerInstanceId = Guid.NewGuid(), GenerationId = snapshot.GenerationId,
            Dataset = "GLBX.MDP3", ValueDate = new(2026, 9, 8),
            CorrelationId = Guid.NewGuid(), Sequence = 1, BootstrapToken = new string('A', 64),
            CompositionResult = new(snapshot with { Instruments = rows }, null)
        };
    }

    [Fact]
    public async Task Repeated_snapshot_reuses_context_but_preserves_changed_quotes_and_context_revisions()
    {
        var frame = await Frame();
        using var stream = new MemoryStream();
        await DatasetWorkerFrameCodec.WriteAsync(stream, frame, Maximum, default);
        var initialLength = stream.Length;
        var row = frame.CompositionResult!.Snapshot!.Instruments[0];
        var changed = frame with { Sequence = 2, CompositionResult = new(frame.CompositionResult.Snapshot with
        {
            Instruments = frame.CompositionResult.Snapshot.Instruments.SetItem(0, row with
            { Instrument = row.Instrument with { Strike = row.Instrument.Strike + 1,
                Quote = row.Instrument.Quote! with { Bid = row.Instrument.Quote.Bid + 1, Ask = row.Instrument.Quote.Ask + 1 } } })
        }, null) };
        await DatasetWorkerFrameCodec.WriteAsync(stream, changed, Maximum, default);
        var repeatedLength = stream.Length - initialLength;
        var refreshed = row.Instrument.Pricing! with
        {
            GenerationId = Guid.NewGuid(), Rate = row.Instrument.Pricing.Rate with { RatePercent = 9 },
            Calendar = row.Instrument.Pricing.Calendar with { Version = "refreshed" }
        };
        var third = frame with { Sequence = 3, CompositionResult = new(frame.CompositionResult.Snapshot with
        {
            Instruments = [row with { Instrument = row.Instrument with { Pricing = refreshed } }]
        }, null) };
        await DatasetWorkerFrameCodec.WriteAsync(stream, third, Maximum, default);
        stream.Position = 0;
        var first = (await DatasetWorkerFrameCodec.ReadAsync(stream, Maximum, default)).CompositionResult!.Snapshot!;
        var second = (await DatasetWorkerFrameCodec.ReadAsync(stream, Maximum, default)).CompositionResult!.Snapshot!;
        var last = (await DatasetWorkerFrameCodec.ReadAsync(stream, Maximum, default)).CompositionResult!.Snapshot!;
        Assert.Same(first.Instruments[0].Instrument.Pricing, second.Instruments[0].Instrument.Pricing);
        Assert.Same(first.Instruments[0].Instrument.Pricing!.Calendar, first.Instruments[255].Instrument.Pricing!.Calendar);
        Assert.Same(first.Instruments[0].Instrument.Pricing!.Rate, first.Instruments[255].Instrument.Pricing!.Rate);
        Assert.Equal(row.Instrument.Strike + 1, second.Instruments[0].Instrument.Strike);
        Assert.Equal(row.Instrument.Quote!.Bid + 1, second.Instruments[0].Instrument.Quote!.Bid);
        Assert.Equal(row.Instrument.Quote.Ask + 1, second.Instruments[0].Instrument.Quote!.Ask);
        Assert.NotSame(first.Instruments[0].Instrument.Pricing, last.Instruments[0].Instrument.Pricing);
        Assert.Equal(refreshed.GenerationId, last.Instruments[0].Instrument.Pricing!.GenerationId);
        Assert.Equal(9, last.Instruments[0].Instrument.Pricing!.Rate.RatePercent);
        Assert.Equal("refreshed", last.Instruments[0].Instrument.Pricing!.Calendar.Version);
        Assert.Equal(PricingSemanticHash.Compute(changed.CompositionResult!.Snapshot), PricingSemanticHash.Compute(second));
        Assert.True(repeatedLength < initialLength);
        // Reconnecting starts with complete definitions rather than references to the old pipe.
        using var fresh = new MemoryStream();
        await DatasetWorkerFrameCodec.WriteAsync(fresh, frame, Maximum, default);
        fresh.Position = 0;
        Assert.Equal(first.Digest, (await DatasetWorkerFrameCodec.ReadAsync(fresh, Maximum, default)).CompositionResult!.Snapshot!.Digest);
    }

    [Fact]
    public async Task Bounded_context_dictionary_survives_eviction_and_redefinition()
    {
        var frame = await Frame(1);
        using var stream = new MemoryStream();
        var original = frame.CompositionResult!.Snapshot!;
        for (var i = 0; i <= WorkerPricingContextCodec.Capacity; i++)
        {
            var row = original.Instruments[0];
            var snapshot = original with { Instruments = [row with { Instrument = row.Instrument with
                { Pricing = row.Instrument.Pricing! with { PricerVersion = $"version-{i}" } } }] };
            await DatasetWorkerFrameCodec.WriteAsync(stream, frame with { CompositionResult = new(snapshot, null) }, Maximum, default);
        }
        await DatasetWorkerFrameCodec.WriteAsync(stream, frame, Maximum, default);
        stream.Position = 0;
        for (var i = 0; i <= WorkerPricingContextCodec.Capacity; i++)
        {
            var read = await DatasetWorkerFrameCodec.ReadAsync(stream, Maximum, default);
            Assert.Equal($"version-{i}", read.CompositionResult!.Snapshot!.Instruments[0].Instrument.Pricing!.PricerVersion);
        }
        var final = await DatasetWorkerFrameCodec.ReadAsync(stream, Maximum, default);
        Assert.Equal(original.Instruments[0].Instrument.Pricing!.PricerVersion,
            final.CompositionResult!.Snapshot!.Instruments[0].Instrument.Pricing!.PricerVersion);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Write_keeps_pool_lease_until_IO_finishes_and_returns_it_on_failure(bool fail)
    {
        var frame = await Frame(1);
        var pool = new TrackingPool();
        using var stream = new DelayedStream(fail);
        var write = DatasetWorkerFrameCodec.WriteAsync(stream, frame, Maximum, default, pool).AsTask();
        await stream.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(pool.Rents - 1, pool.Returns); // previous growth buffers may already be returned
        stream.Release.SetResult();
        if (fail)
        {
            await Assert.ThrowsAsync<IOException>(() => write);
            await Assert.ThrowsAsync<InvalidDataException>(() => DatasetWorkerFrameCodec.WriteAsync(stream, frame, Maximum, default).AsTask());
        }
        else
        {
            await write;
            stream.Position = 0;
            Assert.NotNull((await DatasetWorkerFrameCodec.ReadAsync(stream, Maximum, default)).CompositionResult!.Snapshot);
        }
        Assert.Equal(pool.Rents, pool.Returns);
        Assert.True(pool.AllCleared);
    }

    [Fact]
    public async Task Cancelled_write_returns_buffer_and_requires_a_fresh_connection()
    {
        var frame = await Frame(1);
        var pool = new TrackingPool();
        using var stream = new DelayedStream(false);
        using var cancel = new CancellationTokenSource();
        var write = DatasetWorkerFrameCodec.WriteAsync(stream, frame, Maximum, cancel.Token, pool).AsTask();
        await stream.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => write);
        Assert.Equal(pool.Rents, pool.Returns);
        await Assert.ThrowsAsync<InvalidDataException>(() => DatasetWorkerFrameCodec.WriteAsync(stream, frame, Maximum, default).AsTask());
    }

    [Fact]
    public async Task Missing_context_definition_fails_immediately_and_fences_the_reader()
    {
        var frame = await Frame(1);
        using var writer = new MemoryStream();
        await DatasetWorkerFrameCodec.WriteAsync(writer, frame, Maximum, default);
        var firstLength = (int)writer.Length;
        await DatasetWorkerFrameCodec.WriteAsync(writer, frame, Maximum, default);
        using var reader = new MemoryStream(writer.ToArray()[firstLength..]);
        await Assert.ThrowsAsync<MessagePackSerializationException>(() => DatasetWorkerFrameCodec.ReadAsync(reader, Maximum, default).AsTask());
        await Assert.ThrowsAsync<InvalidDataException>(() => DatasetWorkerFrameCodec.ReadAsync(reader, Maximum, default).AsTask());
    }

    [Fact]
    public void Cached_context_hash_keeps_canonical_identity_and_invalidates_changed_values()
    {
        var original = OrderCompositionPricingPrerequisiteTests.Context();
        var expected = PricingSemanticHashCompatibilityTests.Legacy(original);
        Assert.Equal(expected, PricingSemanticHash.Compute(original));
        Assert.Same(PricingSemanticHash.Compute(original), PricingSemanticHash.Compute(original));
        var changed = original with { Rate = original.Rate with { RatePercent = original.Rate.RatePercent + 1 } };
        Assert.Equal(PricingSemanticHashCompatibilityTests.Legacy(changed), PricingSemanticHash.Compute(changed));
        Assert.NotEqual(expected, PricingSemanticHash.Compute(changed));
    }

    [Fact]
    public async Task Warm_snapshot_transport_reduces_allocations_with_identical_payloads()
    {
        var frame = await Frame();
        var bytes = MessagePackSerializer.Serialize(frame, MessagePackBinarySerializer.ContentOptions);
        using var writes = new MemoryStream(Maximum);
        await DatasetWorkerFrameCodec.WriteAsync(writes, frame, Maximum, default);
        // Warm pools, generated formatters and connection dictionaries before measuring identical captures.
        for (var i = 0; i < 5; i++) { writes.SetLength(0); await DatasetWorkerFrameCodec.WriteAsync(writes, frame, Maximum, default); }
        const int iterations = 30;
        var gen2 = GC.CollectionCount(2);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var timer = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++) _ = MessagePackSerializer.Serialize(frame, MessagePackBinarySerializer.ContentOptions);
        var legacyMs = timer.Elapsed.TotalMilliseconds;
        var legacy = GC.GetAllocatedBytesForCurrentThread() - before;
        var legacyGen2 = GC.CollectionCount(2) - gen2; gen2 = GC.CollectionCount(2);
        before = GC.GetAllocatedBytesForCurrentThread(); timer.Restart();
        for (var i = 0; i < iterations; i++) { writes.SetLength(0); await DatasetWorkerFrameCodec.WriteAsync(writes, frame, Maximum, default); }
        var optimizedMs = timer.Elapsed.TotalMilliseconds;
        var optimized = GC.GetAllocatedBytesForCurrentThread() - before;
        var optimizedGen2 = GC.CollectionCount(2) - gen2;
        output.WriteLine($"256 contracts, 365 calendar dates, {iterations} identical snapshots: legacy serialize {legacy:N0} bytes ({legacyMs / iterations:F3} ms/capture); pooled/context serialize+write {optimized:N0} bytes ({optimizedMs / iterations:F3} ms/capture). Wire bytes {bytes.Length:N0} -> {writes.Length - 4:N0}. Gen2 collections {legacyGen2} -> {optimizedGen2} (observational, process-wide).");
        Assert.True(optimized < legacy / 2, $"Allocation {optimized} versus {legacy}.");
        using var reads = new MemoryStream();
        for (var i = 0; i <= iterations; i++) await DatasetWorkerFrameCodec.WriteAsync(reads, frame, Maximum, default);
        reads.Position = 0;
        await DatasetWorkerFrameCodec.ReadAsync(reads, Maximum, default);
        before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < iterations; i++) _ = MessagePackSerializer.Deserialize<DatasetWorkerControlFrame>(bytes, MessagePackBinarySerializer.ContentOptions);
        var legacyRead = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < iterations; i++) await DatasetWorkerFrameCodec.ReadAsync(reads, Maximum, default);
        var optimizedRead = GC.GetAllocatedBytesForCurrentThread() - before;
        output.WriteLine($"Deserialize allocations: full context {legacyRead:N0} bytes -> cached context {optimizedRead:N0} bytes.");
        Assert.True(optimizedRead < legacyRead / 2);
    }

    sealed class TrackingPool : ArrayPool<byte>
    {
        public int Rents, Returns;
        public bool AllCleared = true;
        public override byte[] Rent(int length) { Rents++; return new byte[length]; }
        public override void Return(byte[] array, bool clearArray = false)
        { Returns++; AllCleared &= clearArray; Array.Fill(array, (byte)0xa5); }
    }
    sealed class DelayedStream(bool fail) : MemoryStream
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token = default)
        {
            Started.TrySetResult(); await Release.Task.WaitAsync(token);
            if (fail) throw new IOException("Injected transport failure.");
            await base.WriteAsync(buffer, token);
        }
    }
}
