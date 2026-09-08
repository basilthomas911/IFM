using NSubstitute;
using TomasAI.IFM.Application.MarketData.Pricing;
using static TomasAI.IFM.Application.MarketData.UnitTests.OrderCompositionPricingPrerequisiteTests;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class OrderCompositionPreparationTests
{
    internal static CompositionPreparationKey Key() => new(Guid.NewGuid(), 5, new('a', 64));
    internal static CompositionSnapshotRequest Request() => new(Guid.NewGuid(), "scope", "Weekly", Generation, At, At.AddSeconds(2), false);
    internal static MarketCompositionSnapshot Snapshot(CompositionSnapshotRequest request)
    {
        var snapshot = new MarketCompositionSnapshot(1, request.SnapshotId, request.ScopeId, "complete-empty", request.Horizon,
            request.GenerationId, request.EvaluatedAtUtc, At.AddSeconds(1), [], "");
        return snapshot with { Digest = PricingSemanticHash.Compute(snapshot) };
    }
    [Fact]
    public async Task Retry_after_commit_uses_identical_capture_and_never_calls_market_again()
    {
        var key = Key(); var request = Request(); var store = new Store();
        var market = Substitute.For<ICompositionMarketDataApi>();
        market.CaptureAsync("GLBX.MDP3", request, default).Returns(new CompositionSnapshotResult(Snapshot(request), null));
        var first = await new CompositionPreparationService(market, store, new Clock(At)).PrepareAsync(key, "GLBX.MDP3", request, default);
        var restarted = new CompositionPreparationService(market, store, new Clock(At));
        var replay = await restarted.PrepareAsync(key, "GLBX.MDP3", request with { SnapshotId = Guid.NewGuid(), GenerationId = Guid.NewGuid() }, default);
        Assert.Null(first.Failure); Assert.Equal(first.Preparation, replay.Preparation);
        await market.Received(1).CaptureAsync(Arg.Any<string>(), Arg.Any<CompositionSnapshotRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Expired_committed_capture_fails_without_recapture()
    {
        var key = Key(); var request = Request(); var store = new Store();
        var market = Substitute.For<ICompositionMarketDataApi>();
        market.CaptureAsync("GLBX.MDP3", request, default).Returns(new CompositionSnapshotResult(Snapshot(request), null));
        await new CompositionPreparationService(market, store, new Clock(At)).PrepareAsync(key, "GLBX.MDP3", request, default);
        var expired = await new CompositionPreparationService(market, store, new Clock(At.AddSeconds(1))).PrepareAsync(key, "GLBX.MDP3", Request(), default);
        Assert.Null(expired.Preparation); Assert.Equal("AcceptedSnapshotExpired", expired.Failure!.Code);
        await market.Received(1).CaptureAsync(Arg.Any<string>(), Arg.Any<CompositionSnapshotRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Failed_capture_and_storage_fault_never_return_prepared_work()
    {
        var key = Key(); var request = Request(); var store = new Store(); var market = Substitute.For<ICompositionMarketDataApi>();
        market.CaptureAsync("GLBX.MDP3", request, default).Returns(new CompositionSnapshotResult(null, new("StaleData", "Quote", "", "stale")));
        Assert.NotNull((await new CompositionPreparationService(market, store, new Clock(At)).PrepareAsync(key, "GLBX.MDP3", request, default)).Failure);
        Assert.Null(store.Value);
        market.CaptureAsync("GLBX.MDP3", request, default).Returns(new CompositionSnapshotResult(Snapshot(request), null));
        store.Fail = true;
        await Assert.ThrowsAsync<IOException>(() => new CompositionPreparationService(market, store, new Clock(At)).PrepareAsync(key, "GLBX.MDP3", request, default));
        Assert.Null(store.Value);
    }

    [Fact]
    public async Task Conflicting_workflow_binding_cannot_reuse_accepted_snapshot()
    {
        var key = Key(); var request = Request(); var store = new Store(); var market = Substitute.For<ICompositionMarketDataApi>();
        market.CaptureAsync("GLBX.MDP3", request, default).Returns(new CompositionSnapshotResult(Snapshot(request), null));
        var service = new CompositionPreparationService(market, store, new Clock(At));
        await service.PrepareAsync(key, "GLBX.MDP3", request, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PrepareAsync(key with { InputSha256 = new('b', 64) }, "GLBX.MDP3", request, default));
    }

    [Fact]
    public async Task Complete_empty_universe_is_committed_and_replayed_without_market_access()
    {
        var store = new Store(); var market = Substitute.For<ICompositionMarketDataApi>();
        var service = new CompositionPreparationService(market, store, new Clock(At)); var key = Key();
        var first = await service.PrepareEmptyAsync(key, "empty", "Daily", Generation, At.AddSeconds(2), default);
        Assert.Null(first.Failure); Assert.Empty(first.Preparation!.Snapshot.Instruments);
        Assert.Equal(2, first.Preparation.SchemaVersion);
        var replay = await service.PrepareEmptyAsync(key, "other", "Weekly", Guid.NewGuid(), At.AddSeconds(3), default);
        Assert.Equal(first.Preparation, replay.Preparation);
        Assert.Empty(market.ReceivedCalls());
    }

    [Fact]
    public void Historical_schema_one_hash_without_discovery_property_remains_valid_but_tampering_fails()
    {
        var request = Request();
        var value = new CompositionPreparation(1, Key(), "GLBX.MDP3", request, Snapshot(request), At, "");
        value = value with { Digest = PricingSemanticHash.Compute(new
        { value.SchemaVersion, value.Key, value.Dataset, value.Request, value.Snapshot, value.PreparedAtUtc, Digest = "" }) };
        CompositionPreparationService.Validate(value);
        Assert.Throws<InvalidDataException>(() => CompositionPreparationService.Validate(value with { SchemaVersion = 2 }));
        Assert.Throws<InvalidDataException>(() => CompositionPreparationService.Validate(value with { Key = Key() }));
    }

    sealed class Store : ICompositionPreparationStore
    {
        public CompositionPreparation? Value; public bool Fail;
        public Task<CompositionPreparation?> ReadAsync(CompositionPreparationKey key, CancellationToken token)
        {
            if (Value is not null && Value.Key != key) throw new InvalidOperationException("Conflicting identity.");
            return Task.FromResult(Value);
        }
        public Task<CompositionPreparation> CommitAsync(CompositionPreparation proposed, CancellationToken token)
        {
            if (Fail) throw new IOException("Injected storage failure.");
            Value ??= proposed; return Task.FromResult(Value);
        }
    }
}
