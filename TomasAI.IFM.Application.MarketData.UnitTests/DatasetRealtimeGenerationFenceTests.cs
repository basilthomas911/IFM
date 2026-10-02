using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.FuturesVwapSignal;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class DatasetRealtimeGenerationFenceTests
{
    static readonly ActorSubject Source = new(ActorType.Realtime,
        FuturesMarketPriceUpdatedRealtimeEvent.Actor,
        FuturesMarketPriceUpdatedRealtimeEvent.Verb, "ESZ26");

    [Fact]
    public async Task Close_fences_queued_old_events_and_waits_for_running_handler()
    {
        var registry = new DatasetWorkerAdmissionRegistry();
        var old = Identity(Guid.NewGuid());
        registry.Admit(old);
        Assert.True(registry.TryEnter(Source, old.Dataset, old.GenerationId, out var running));

        registry.Close(old.Dataset, old.GenerationId);
        Assert.False(registry.TryEnter(Source, old.Dataset, old.GenerationId, out _));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var drain = registry.WaitForProcessingDrainAsync(old.Dataset, old.GenerationId,
            TimeSpan.FromSeconds(1), deadline.Token);
        Assert.False(drain.IsCompleted);

        running!.Dispose();
        await drain;
        var replacement = Identity(Guid.NewGuid());
        registry.Admit(replacement);
        Assert.False(registry.TryEnter(Source, old.Dataset, old.GenerationId, out _));
        Assert.True(registry.TryEnter(Source, replacement.Dataset, replacement.GenerationId,
            out var next));
        next!.Dispose();
    }

    [Fact]
    public void Managed_realtime_route_rejects_missing_identity_but_unrelated_routes_are_unchanged()
    {
        var registry = new DatasetWorkerAdmissionRegistry();
        var current = Identity(Guid.NewGuid());
        registry.Admit(current);

        Assert.False(registry.TryEnter(Source, null, Guid.Empty, out _));
        Assert.True(registry.TryEnter(new ActorSubject(ActorType.Realtime, "IndependentActor",
            "Updated", "ESZ26"), null, Guid.Empty, out _));
        Assert.True(registry.TryEnter(Source, current.Dataset, current.GenerationId,
            out var lease));
        lease!.Dispose();
    }

    [Fact]
    public void Direct_readmission_cannot_overlap_an_active_old_actor_handler()
    {
        var registry = new DatasetWorkerAdmissionRegistry();
        var old = Identity(Guid.NewGuid());
        registry.Admit(old);
        Assert.True(registry.TryEnter(Source, old.Dataset, old.GenerationId, out var running));
        var replacement = Identity(Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() => registry.Admit(replacement));
        Assert.True(registry.TryGet(old.Dataset, out var stillAdmitted));
        Assert.Equal(old, stillAdmitted);

        running!.Dispose();
        registry.Admit(replacement);
        Assert.True(registry.TryGet(replacement.Dataset, out var admitted));
        Assert.Equal(replacement, admitted);
    }

    [Theory]
    [InlineData(FuturesTickTradeDataChangedEvent.Actor, FuturesTickTradeDataChangedEvent.Verb)]
    [InlineData(FuturesTickQuoteDataChangedEvent.Actor, FuturesTickQuoteDataChangedEvent.Verb)]
    [InlineData(FuturesMarketPriceUpdatedRealtimeEvent.Actor, FuturesMarketPriceUpdatedRealtimeEvent.Verb)]
    [InlineData(FuturesVwapSourceCheckpoint.Actor, FuturesMarketPriceUpdatedRealtimeEvent.Verb)]
    [InlineData(FuturesTradeReplayBatchRealtimeEvent.Actor, FuturesTradeReplayBatchRealtimeEvent.Verb)]
    [InlineData(FuturesSessionStatisticsUpdatedRealtimeEvent.Actor, FuturesSessionStatisticsUpdatedRealtimeEvent.Verb)]
    public void Every_supervised_publication_route_requires_current_generation(string actor, string verb)
    {
        var registry = new DatasetWorkerAdmissionRegistry();
        var current = Identity(Guid.NewGuid());
        registry.Admit(current);
        var route = new ActorSubject(ActorType.Realtime, actor, verb, "ESZ26");

        Assert.False(registry.TryEnter(route, current.Dataset, Guid.NewGuid(), out _));
        Assert.False(registry.TryEnter(route, null, Guid.Empty, out _));
        Assert.True(registry.TryEnter(route, current.Dataset, current.GenerationId,
            out var lease));
        lease!.Dispose();
    }

    [Fact]
    public async Task Candidate_probe_allows_only_exact_tick_storage_route_and_drains_before_full_admission()
    {
        var registry = new DatasetWorkerAdmissionRegistry();
        var candidate = Identity(Guid.NewGuid());
        var trade = new ActorSubject(ActorType.Realtime,
            FuturesTickTradeDataChangedEvent.Actor, FuturesTickTradeDataChangedEvent.Verb, "ESZ26");
        var quote = new ActorSubject(ActorType.Realtime,
            FuturesTickQuoteDataChangedEvent.Actor, FuturesTickQuoteDataChangedEvent.Verb, "ESZ26");
        registry.BeginProbe(candidate);
        Assert.True(registry.HasProbe(candidate));
        Assert.False(registry.HasProbe(candidate with { GenerationId = Guid.NewGuid() }));

        Assert.True(registry.TryEnter(trade, candidate.Dataset, candidate.GenerationId, out var running));
        Assert.True(registry.TryEnter(quote, candidate.Dataset, candidate.GenerationId, out var quoteLease));
        quoteLease!.Dispose();
        Assert.False(registry.TryEnter(trade, candidate.Dataset, Guid.NewGuid(), out _));
        Assert.False(registry.TryEnter(Source, candidate.Dataset, candidate.GenerationId, out _));
        Assert.Throws<InvalidOperationException>(() => registry.Admit(candidate));

        var drain = registry.EndProbeAsync(candidate, TimeSpan.FromSeconds(2));
        Assert.False(drain.IsCompleted);
        Assert.False(registry.TryEnter(trade, candidate.Dataset, candidate.GenerationId, out _));
        running!.Dispose();
        await drain;
        Assert.False(registry.HasProbe(candidate));
        Assert.False(registry.TryEnter(trade, candidate.Dataset, candidate.GenerationId, out _));
        registry.Admit(candidate);
        Assert.True(registry.TryEnter(Source, candidate.Dataset, candidate.GenerationId,
            out var admitted));
        admitted!.Dispose();
    }

    private static DatasetWorkerAdmission Identity(Guid generation) => new(
        "GLBX.MDP3", new DateOnly(2026, 9, 30), Guid.NewGuid(), generation, 1);
}
