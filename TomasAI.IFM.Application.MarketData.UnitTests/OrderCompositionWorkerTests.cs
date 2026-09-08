using NSubstitute;
using System.Collections.Immutable;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.MarketData.Contracts.LastPrice;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.DataBento.Interop;
using TomasAI.IFM.Framework.MarketData.DataBento.LastPrice;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;
using static TomasAI.IFM.Application.MarketData.UnitTests.OrderCompositionPricingPrerequisiteTests;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed partial class OrderCompositionWorkerTests
{
    [Theory]
    [InlineData(2)] [InlineData(4)]
    public async Task Selected_business_legs_remain_priced_after_discovery_expiry_and_context_refresh(int count)
    {
        using var prices = Prices(); using var feed = new ChainFeed(); var factory = Factory(feed);
        var clock = new MutableClock(); await using var runtime = Runtime(factory, prices, clock);
        var original = Request();
        var definitions = Enumerable.Range(0, count).Select(index => new WorkerOptionDefinition(Context() with
        {
            Contract = Contract() with { ContractId = "leg-" + index, InstrumentId = (uint)(10 + index), RawSymbol = "fixture-leg-" + index }
        }, 5000 + index, true)).ToImmutableArray();
        original = original with { Options = definitions };
        var acquired = await runtime.AcquireAsync(original, default); Assert.True(acquired.Active);
        var owners = definitions.Select(x => new WorkerOptionChainOwner(Guid.NewGuid(), [x.Pricing.Contract.ContractId])).ToImmutableArray();
        Assert.True((await runtime.ReleaseAsync(new(original.ScopeId, Guid.Empty, Generation,
            new(1, "IFM", owners, WorkerOptionChainRuntime.PhysicalDigest(definitions))), default)).Active);
        Assert.True((await runtime.ReleaseAsync(new(original.ScopeId, original.LeaseId, Generation), default)).Active);
        clock.Now = At.AddMinutes(3);
        prices.TryUpdateQuote(new("ES-future", Date, 4999.75m, 10, 1, 5000.25m, 10, 1, 2, clock.Now, clock.Now));
        var refreshed = original with { LeaseId = owners[0].LeaseId, LeaseExpiresAtUtc = clock.Now.AddSeconds(60),
            ExpectedContextDigest = acquired.ContextDigest,
            Options = definitions.Select(x => x with { Pricing = x.Pricing with { PublicationPolicyVersion = "fixture-refreshed/v2" } }).ToImmutableArray() };
        Assert.True((await runtime.AcquireAsync(refreshed, default)).Active);
        for (var index = 0; index < count; index++) feed.Push(QuoteRecord(3, (uint)(10 + index), clock.Now));
        await Until(() => definitions.All(x => prices.GetFuturesOptionReader(x.Pricing.Contract.ContractId, Date).TryGetLastQuoteWithGreeks(out _)));
        var result = await new MarketCompositionSnapshotProvider(runtime, clock).CaptureAsync(
            new(Guid.NewGuid(), original.ScopeId, "Daily", Generation, clock.Now, clock.Now.AddSeconds(2), true), default);
        Assert.Null(result.Failure); Assert.Equal(count, result.Snapshot!.Instruments.Length);
        Assert.All(result.Snapshot.Instruments, x => Assert.NotNull(x.Valuation));
        factory.Received(1).CreateOptionChainFeed(Arg.Any<DatabentoFeedOptions>());
        Assert.Equal(0, feed.Stops);
        Assert.False((await runtime.ReleaseAsync(new(original.ScopeId, Guid.Empty, Generation,
            new(2, "IFM", [], WorkerOptionChainRuntime.PhysicalDigest(definitions))), default)).Active);
        Assert.Equal(1, feed.Stops);
    }

    static readonly DateOnly Date = DateOnly.FromDateTime(At.UtcDateTime);
    static WorkerOptionChainRequest Request() => new("es-scope", Guid.NewGuid(), Generation, Date,
        new(2026, 10, 2), At.AddSeconds(60), [new(Context(), 5000, true)]);

    [Fact]
    public async Task Worker_uses_real_feed_consumer_pricer_and_snapshot_then_releases_only_last_owner()
    {
        using var prices = Prices();
        using var feed = new ChainFeed();
        var factory = Factory(feed);
        await using var runtime = Runtime(factory, prices);
        var request = Request();
        Assert.True((await runtime.AcquireAsync(request, default)).Active);
        var second = request with { LeaseId = Guid.NewGuid() };
        Assert.True((await runtime.AcquireAsync(second, default)).Active);
        factory.Received(1).CreateOptionChainFeed(Arg.Any<DatabentoFeedOptions>());
        feed.Push(QuoteRecord(1));
        var reader = prices.GetFuturesOptionReader("ES-option-call", Date);
        await Until(() => reader.TryGetLastQuoteWithGreeks(out _));
        Assert.True(reader.TryGetLastQuoteWithGreeks(out var observed));
        Assert.True(observed.Greeks.IsValid, observed.Greeks.PricingFailure?.Code);
        var capture = new CompositionSnapshotRequest(Guid.NewGuid(), request.ScopeId, "Daily", Generation, At, At.AddSeconds(2), true);
        var snapshot = await new MarketCompositionSnapshotProvider(runtime, new Clock(At)).CaptureAsync(capture, default);
        Assert.Null(snapshot.Failure);
        Assert.NotNull(Assert.Single(snapshot.Snapshot!.Instruments).Valuation);
        // A new underlying observation is used on the next callback, without rebuilding the context or HTTP.
        prices.TryUpdateQuote(new("ES-future", Date, 5009.75m, 10, 1, 5010.25m, 10, 1, 2, At, At));
        feed.Push(QuoteRecord(2));
        await Until(() => reader.TryGetLastQuoteWithGreeks(out var q) && q.Tick.SourceSequence == 2);
        Assert.True(reader.TryGetLastQuoteWithGreeks(out observed));
        Assert.Equal(5010m, observed.Greeks.FuturesPrice);
        Assert.True((await runtime.ReleaseAsync(new(request.ScopeId, request.LeaseId, Generation), default)).Active);
        Assert.Equal(0, feed.Stops);
        Assert.False((await runtime.ReleaseAsync(new(request.ScopeId, second.LeaseId, Generation), default)).Active);
        Assert.Equal(1, feed.Stops);
        Assert.Equal("LeaseEnded", (await runtime.AcquireAsync(request, default)).Failure!.Code);
        var unavailable = await new MarketCompositionSnapshotProvider(runtime, new Clock(At)).CaptureAsync(capture, default);
        Assert.Equal("ChainUnavailable", unavailable.Failure!.Code);
    }

    [Theory]
    [InlineData("rate")] [InlineData("generation")] [InlineData("expiry")] [InlineData("calendar")]
    public async Task Invalid_prerequisites_allocate_no_native_feed(string failure)
    {
        using var prices = Prices(); using var feed = new ChainFeed(); var factory = Factory(feed);
        await using var runtime = Runtime(factory, prices);
        var request = Request(); var option = request.Options[0];
        if (failure == "rate") option = option with { Pricing = option.Pricing with { Rate = option.Pricing.Rate with { AnnualContinuousRate = 5 } } };
        if (failure == "calendar") option = option with { Pricing = option.Pricing with { Calendar = Calendar() with { CoverageUntil = Date } } };
        request = request with { Options = [option] };
        if (failure == "generation") request = request with { GenerationId = Guid.NewGuid() };
        if (failure == "expiry") request = request with { LeaseExpiresAtUtc = At };
        Assert.NotNull((await runtime.AcquireAsync(request, default)).Failure);
        factory.DidNotReceive().CreateOptionChainFeed(Arg.Any<DatabentoFeedOptions>());
    }

    [Fact]
    public async Task Context_refresh_is_atomic_fenced_and_preserves_the_native_feed()
    {
        using var prices = Prices(); using var feed = new ChainFeed(); var factory = Factory(feed);
        await using var runtime = Runtime(factory, prices); var original = Request();
        var acquired = await runtime.AcquireAsync(original, default);
        var changed = original with
        {
            ExpectedContextDigest = acquired.ContextDigest,
            Options = [original.Options[0] with { Pricing = original.Options[0].Pricing with { PublicationPolicyVersion = "reviewed-v2" } }]
        };
        var result = await runtime.AcquireAsync(changed, default);
        Assert.True(result.Active); Assert.NotEqual(acquired.ContextDigest, result.ContextDigest);
        Assert.Equal("ConflictingChainScope", (await runtime.AcquireAsync(original with { ExpectedContextDigest = acquired.ContextDigest }, default)).Failure!.Code);
        Assert.Equal("ConflictingChainScope", (await runtime.AcquireAsync(changed with
        { ExpectedContextDigest = result.ContextDigest, Options = [changed.Options[0] with { Strike = 5010 }] }, default)).Failure!.Code);
        feed.Push(QuoteRecord(1));
        await Until(() => prices.GetFuturesOptionReader("ES-option-call", Date).TryGetLastQuoteWithGreeks(out _));
        var page = await runtime.ReadAsync(new(Guid.NewGuid(), original.ScopeId, "Daily", Generation, At, At.AddSeconds(1), true), null, default);
        Assert.Equal("reviewed-v2", Assert.Single(page.Instruments).Pricing!.PublicationPolicyVersion);
        factory.Received(1).CreateOptionChainFeed(Arg.Any<DatabentoFeedOptions>());
    }

    [Fact]
    public async Task Business_ownership_survives_discovery_release_and_expiry_until_its_explicit_terminal_revision()
    {
        using var prices = Prices(); using var feed = new ChainFeed(); var factory = Factory(feed);
        var clock = new MutableClock(); await using var runtime = Runtime(factory, prices, clock); var request = Request();
        await runtime.AcquireAsync(request, default);
        var first = new WorkerOptionChainOwner(Guid.NewGuid(), ["ES-option-call"]);
        var second = new WorkerOptionChainOwner(Guid.NewGuid(), ["ES-option-call"]);
        var ownership = new WorkerOptionChainOwnership(1, "IFM", [first, second], WorkerOptionChainRuntime.PhysicalDigest(request.Options));
        Assert.True((await runtime.ReleaseAsync(new(request.ScopeId, Guid.Empty, Generation, ownership), default)).Active);
        Assert.True((await runtime.ReleaseAsync(new(request.ScopeId, request.LeaseId, Generation), default)).Active);
        clock.Now = At.AddMinutes(3);
        Assert.Equal(0, feed.Stops);
        var one = ownership with { Revision = 2, Owners = [second] };
        Assert.True((await runtime.ReleaseAsync(new(request.ScopeId, Guid.Empty, Generation, one), default)).Active);
        Assert.Equal("OwnershipConflict", (await runtime.ReleaseAsync(new(request.ScopeId, Guid.Empty, Generation, ownership), default)).Failure!.Code);
        Assert.False((await runtime.ReleaseAsync(new(request.ScopeId, Guid.Empty, Generation, one with { Revision = 3, Owners = [] }), default)).Active);
        Assert.Equal(1, feed.Stops);
    }

    [Fact]
    public async Task Business_update_cannot_shrink_the_declared_scope_to_drop_other_owners()
    {
        using var prices = Prices(); using var feed = new ChainFeed();
        await using var runtime = Runtime(Factory(feed), prices); var request = Request();
        await runtime.AcquireAsync(request, default);
        var ownership = new WorkerOptionChainOwnership(1, "IFM", [], new('f', 64));
        Assert.Equal("OwnershipInvalid", (await runtime.ReleaseAsync(new(request.ScopeId, Guid.Empty, Generation, ownership), default)).Failure!.Code);
        Assert.Equal(0, feed.Stops);
    }

    [Fact]
    public async Task Terminal_ownership_watermark_survives_physical_scope_recreation()
    {
        using var prices = Prices(); using var feed = new ChainFeed(); var factory = Factory(feed);
        await using var runtime = Runtime(factory, prices); var request = Request();
        await runtime.AcquireAsync(request, default);
        var ownership = new WorkerOptionChainOwnership(1, "IFM", [new(Guid.NewGuid(), ["ES-option-call"])], WorkerOptionChainRuntime.PhysicalDigest(request.Options));
        await runtime.ReleaseAsync(new(request.ScopeId, Guid.Empty, Generation, ownership), default);
        await runtime.ReleaseAsync(new(request.ScopeId, request.LeaseId, Generation), default);
        await runtime.ReleaseAsync(new(request.ScopeId, Guid.Empty, Generation, ownership with { Revision = 2, Owners = [] }), default);
        using var replacement = new ChainFeed(); factory.CreateOptionChainFeed(Arg.Any<DatabentoFeedOptions>()).Returns(replacement);
        Assert.True((await runtime.AcquireAsync(request with { LeaseId = Guid.NewGuid() }, default)).Active);
        Assert.Equal("OwnershipConflict", (await runtime.ReleaseAsync(new(request.ScopeId, Guid.Empty, Generation, ownership), default)).Failure!.Code);
        Assert.Equal(0, replacement.Stops);
    }

    [Fact]
    public async Task Conflicting_scope_and_stale_release_preserve_existing_owner()
    {
        using var prices = Prices(); using var feed = new ChainFeed(); var factory = Factory(feed);
        await using var runtime = Runtime(factory, prices); var request = Request();
        Assert.True((await runtime.AcquireAsync(request, default)).Active);
        Assert.Equal("ConflictingChainScope", (await runtime.AcquireAsync(request with { ScopeId = "other", LeaseId = Guid.NewGuid() }, default)).Failure!.Code);
        Assert.Equal("ConflictingChainScope", (await runtime.AcquireAsync(request with { Options = [request.Options[0] with { Strike = 5010 }] }, default)).Failure!.Code);
        Assert.NotNull((await runtime.ReleaseAsync(new(request.ScopeId, request.LeaseId, Guid.NewGuid()), default)).Failure);
        Assert.Equal(0, feed.Stops);
    }

    [Fact]
    public async Task Feed_start_failure_disposes_provisional_chain_and_allows_retry()
    {
        using var prices = Prices(); using var feed = new ChainFeed { FailStart = true }; var factory = Factory(feed);
        await using var runtime = Runtime(factory, prices);
        await Assert.ThrowsAsync<IOException>(() => runtime.AcquireAsync(Request(), default));
        Assert.True(feed.Disposed);
        using var replacement = new ChainFeed(); factory.CreateOptionChainFeed(Arg.Any<DatabentoFeedOptions>()).Returns(replacement);
        Assert.True((await runtime.AcquireAsync(Request(), default)).Active);
    }

    [Fact]
    public async Task Worker_control_wire_preserves_typed_context_and_rejects_cross_generation_request()
    {
        var frame = new DatasetWorkerControlFrame
        {
            Kind = DatasetWorkerMessageKind.AcquireOptionChain, WorkerInstanceId = Guid.NewGuid(), Dataset = "GLBX.MDP3",
            ValueDate = Date, GenerationId = Generation, CorrelationId = Guid.NewGuid(), Sequence = 1,
            BootstrapToken = new('a', 64), OptionChain = Request()
        };
        using var stream = new MemoryStream();
        await DatasetWorkerFrameCodec.WriteAsync(stream, frame, 1024 * 1024, default); stream.Position = 0;
        var restored = await DatasetWorkerFrameCodec.ReadAsync(stream, 1024 * 1024, default);
        Assert.Equal(frame.OptionChain!.Options[0].Pricing.Contract, restored.OptionChain!.Options[0].Pricing.Contract);
        Assert.Equal(PricingSemanticHash.Compute(frame.OptionChain), PricingSemanticHash.Compute(restored.OptionChain));
        await Assert.ThrowsAsync<InvalidDataException>(async () => await DatasetWorkerFrameCodec.WriteAsync(stream,
            frame with { GenerationId = Guid.NewGuid() }, 1024 * 1024, default));
    }

    [Fact]
    public async Task Ownership_requires_a_distinct_wire_operation_and_cannot_be_acknowledged_as_a_release()
    {
        var request = Request();
        var frame = new DatasetWorkerControlFrame
        {
            Kind = DatasetWorkerMessageKind.ApplyOptionChainOwnership, WorkerInstanceId = Guid.NewGuid(), Dataset = "GLBX.MDP3",
            ValueDate = Date, GenerationId = Generation, CorrelationId = Guid.NewGuid(), Sequence = 1, BootstrapToken = new('a', 64),
            OptionChainRelease = new(request.ScopeId, Guid.Empty, Generation,
                new(1, "IFM", [new(Guid.NewGuid(), ["ES-option-call"])], WorkerOptionChainRuntime.PhysicalDigest(request.Options)))
        };
        using var stream = new MemoryStream();
        await DatasetWorkerFrameCodec.WriteAsync(stream, frame, 1024 * 1024, default); stream.Position = 0;
        var restored = await DatasetWorkerFrameCodec.ReadAsync(stream, 1024 * 1024, default);
        Assert.Equal(PricingSemanticHash.Compute(frame.OptionChainRelease), PricingSemanticHash.Compute(restored.OptionChainRelease));
        await Assert.ThrowsAsync<InvalidDataException>(async () => await DatasetWorkerFrameCodec.WriteAsync(stream,
            frame with { Kind = DatasetWorkerMessageKind.ReleaseOptionChain }, 1024 * 1024, default));
        await Assert.ThrowsAsync<InvalidDataException>(async () => await DatasetWorkerFrameCodec.WriteAsync(stream,
            frame with { OptionChainRelease = frame.OptionChainRelease with { Ownership = null } }, 1024 * 1024, default));
    }

    [Fact]
    public async Task Expired_lease_is_drained_and_cannot_be_resurrected_by_renewal()
    {
        using var prices = Prices(); using var feed = new ChainFeed(); var factory = Factory(feed);
        var clock = new MutableClock();
        await using var runtime = Runtime(factory, prices, clock); var request = Request();
        Assert.True((await runtime.AcquireAsync(request, default)).Active);
        clock.Now = At.AddSeconds(61);
        var renewal = request with { LeaseExpiresAtUtc = At.AddSeconds(120) };
        Assert.Equal("LeaseEnded", (await runtime.AcquireAsync(renewal, default)).Failure!.Code);
        Assert.Equal(1, feed.Stops);
    }

    [Fact]
    public async Task Failed_native_drain_signals_worker_health_failure()
    {
        using var prices = Prices(); using var feed = new ChainFeed { FailStop = true }; var factory = Factory(feed);
        string? fault = null;
        await using var runtime = Runtime(factory, prices, terminalFault: message => fault = message);
        var request = Request();
        Assert.True((await runtime.AcquireAsync(request, default)).Active);
        await Assert.ThrowsAsync<IOException>(() => runtime.ReleaseAsync(new(request.ScopeId, request.LeaseId, Generation), default));
        Assert.NotNull(fault); Assert.True(feed.Disposed);
    }

    static WorkerOptionChainRuntime Runtime(IDatabentoFeedFactory factory, DatabentoLastPriceStore prices, TimeProvider? time = null,
        Action<string>? terminalFault = null)
    {
        var aggregation = Substitute.For<ITickAggregationService>();
        aggregation.GetTickerStatus("ES-future").Returns(new TickAggregationTickerStatus("ES-future", true, true, true));
        return new(Generation, Date, factory, DatabentoFeedOptions.ForProfile(FeedDeploymentProfile.SyntheticCi, "GLBX.MDP3"),
            aggregation, prices, time ?? new Clock(At), terminalFault);
    }
    static IDatabentoFeedFactory Factory(ChainFeed feed)
    {
        var factory = Substitute.For<IDatabentoFeedFactory>();
        factory.CreateOptionChainFeed(Arg.Any<DatabentoFeedOptions>()).Returns(feed); return factory;
    }
    static DatabentoLastPriceStore Prices()
    {
        var prices = new DatabentoLastPriceStore(Date, 16); prices.RegisterContract("ES-future", AssetTypeId.Futures);
        prices.TryUpdateQuote(new("ES-future", Date, 4999.75m, 10, 1, 5000.25m, 10, 1, 1, At, At)); return prices;
    }
    static MarketRecord64 QuoteRecord(uint sequence, uint instrumentId = 10, DateTimeOffset? observed = null)
    {
        var ns = checked(((observed ?? At).UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks) * 100);
        return new(new QuoteRecord64(new(instrumentId, 1, MarketRecordKind.Quote, 0, ns, (long)ns, sequence),
            99_750_000_000, 100_250_000_000, 10, 10, 1, 1));
    }
    static async Task Until(Func<bool> condition)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition()) await Task.Delay(5, deadline.Token);
    }
    sealed class ChainFeed : IDatabentoOptionChainFeed
    {
        readonly BoundedBatchChannel channel = new(4, 64);
        public int Stops; public bool Disposed; public bool FailStart; public bool FailStop;
        public ISynchronousBatchReader<MarketDataBatch64> Reader => channel;
        public void Subscribe(OptionChainSubscription subscription, TimeSpan timeout) { }
        public void Start(TimeSpan timeout, Action<TimeSpan> consumer)
        { if (FailStart) throw new IOException("Injected start failure."); consumer(timeout); }
        public void Push(MarketRecord64 record)
        { var batch = channel.RentBatch(() => false); batch.Add(record); Assert.True(channel.Publish(batch, () => false)); }
        public void Stop(TimeSpan timeout) { Stops++; channel.Complete(); if (FailStop) throw new IOException("Injected native drain failure."); }
        public FeedHealthSnapshot GetHealth() => throw new NotSupportedException();
        public void Dispose() { Disposed = true; channel.Complete(); }
    }
    sealed class MutableClock : TimeProvider
    {
        public DateTimeOffset Now = At;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
