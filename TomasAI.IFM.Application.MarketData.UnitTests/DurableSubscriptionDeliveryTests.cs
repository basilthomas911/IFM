using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Subscriptions;
using TomasAI.IFM.Application.MarketData.Subscriptions.Persistence;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class DurableSubscriptionDeliveryTests
{
    static DurableSubscriptionSnapshot Snapshot(long revision = 1) => new(1, "IFM", "GLBX.MDP3", revision,
        [new("order-1", 1, Guid.NewGuid(), new('a', 64), new("TradeOrder", "1", "legs"), DurableAuthorityStatus.Active, "Working",
            [new(Guid.NewGuid(), 1, SubscriptionLeasePurpose.WorkingOrder, new("Databento", "GLBX.MDP3", "ES-option", "mbp-1", SubscriptionAssetKind.FuturesOption, "ES-future"))])]);

    [Fact]
    public async Task Restart_restores_durable_union_without_TTL_and_unknown_authority_retains_it()
    {
        await using var coordinator = new MarketDataSubscriptionCoordinator("IFM", "GLBX.MDP3", new(2026, 9, 8));
        var snapshot = Snapshot(); Assert.True(await coordinator.ApplyDurableAsync(snapshot));
        Assert.Null(Assert.Single(coordinator.Current.Leases).ExpiresAtUtc);
        Assert.Equal(2, coordinator.Current.Routes.Count);
        Assert.True(await coordinator.ApplyDurableAsync(snapshot));
        var unknown = snapshot with { Revision = 2, Authorities = [snapshot.Authorities[0] with { SourceVersion = 2, Status = DurableAuthorityStatus.Unknown }] };
        Assert.True(await coordinator.ApplyDurableAsync(unknown));
        Assert.Single(coordinator.Current.Leases);
        Assert.False(await coordinator.ApplyDurableAsync(snapshot));
        Assert.True(await coordinator.ApplyDurableAsync(unknown with { Revision = 3, Authorities = [unknown.Authorities[0] with { SourceVersion = 3, Status = DurableAuthorityStatus.Terminal, Leases = [] }] }));
        Assert.Empty(coordinator.Current.Routes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Outbox_is_acknowledged_only_after_complete_current_revision_realization(bool ready)
    {
        var snapshot = Snapshot(); var store = Substitute.For<IDurableSubscriptionIntentStore>();
        store.ReadAsync("IFM", "GLBX.MDP3", default).Returns(snapshot);
        var transition = new DurableSubscriptionOutboxItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, "order-1", 1, DurableAuthorityStatus.Active, "Working", DateTimeOffset.UtcNow);
        store.ReadPendingOutboxAsync("IFM", "GLBX.MDP3", 100, default).Returns([transition]);
        store.AcknowledgeOutboxAsync("IFM", "GLBX.MDP3", transition.TransitionId, default).Returns(true);
        await using var coordinator = new MarketDataSubscriptionCoordinator("IFM", "GLBX.MDP3", new(2026, 9, 8));
        var result = await new DurableSubscriptionDelivery(store).ReconcileAsync(coordinator,
            (manifest, _) => Task.FromResult(new DurableRealization(manifest.Revision, Guid.NewGuid(), ready)), default);
        Assert.Equal(ready, result.AllRoutesReady);
        await store.Received(ready ? 1 : 0).AcknowledgeOutboxAsync("IFM", "GLBX.MDP3", transition.TransitionId, default);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Handoff_commits_all_legs_before_readiness_and_never_releases_discovery_on_failed_realization(bool ready)
    {
        var snapshot = Snapshot(); var state = snapshot.Authorities[0];
        var reference = new BusinessSubscriptionSourceReference(BusinessSubscriptionSourceKind.TradeOrder, state.Owner.WorkflowId, state.SourceVersion, state.SourceEventId);
        var mutation = new DurableAuthorityMutation("IFM", "GLBX.MDP3", Guid.NewGuid(), Guid.NewGuid(), 0,
            state.SourceId, state.SourceVersion, state.SourceEventId, state.Owner, state.Status, state.ReasonCode, state.Leases, []);
        var source = Substitute.For<ICommittedBusinessSubscriptionSource>(); source.ReadAsync(reference, default).Returns(mutation);
        var store = Substitute.For<IDurableSubscriptionIntentStore>(); var committed = false; var released = false;
        store.ApplyAsync(Arg.Any<DurableAuthorityMutation>(), default).Returns(_ =>
        { committed = true; return new DurableIntentResult(mutation.OperationId, DurableIntentResultCode.Committed, 1, Guid.NewGuid()); });
        store.ReadAsync("IFM", "GLBX.MDP3", default).Returns(snapshot);
        store.ReadPendingOutboxAsync("IFM", "GLBX.MDP3", 100, default).Returns([]);
        await using var coordinator = new MarketDataSubscriptionCoordinator("IFM", "GLBX.MDP3", new(2026, 9, 8));
        var operation = new DurableSubscriptionDelivery(store).HandoffAsync(reference, source, coordinator,
            (manifest, _) => { Assert.True(committed); Assert.False(released); return Task.FromResult(new DurableRealization(manifest.Revision, Guid.NewGuid(), ready)); },
            _ => { Assert.True(committed); released = true; return Task.CompletedTask; }, default);
        if (ready) Assert.Equal(DurableIntentResultCode.Committed, (await operation).Code);
        else await Assert.ThrowsAsync<InvalidOperationException>(() => operation);
        Assert.Equal(ready, released);
    }
}
