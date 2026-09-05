using FluentAssertions;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Subscriptions;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class Stage4SubscriptionCoordinatorTests
{
    static readonly DateOnly ValueDate = new(2026, 9, 4);

    [Fact]
    public async Task Two_owners_share_intent_and_final_release_removes_only_the_last_reference()
    {
        var time = new LeaseTime();
        await using var coordinator = await Open(time);
        var first = await coordinator.AcquireAsync(Acquire(time, "one"));
        var second = await coordinator.AcquireAsync(Acquire(time, "two"));
        first.Code.Should().Be(SubscriptionResultCode.DesiredAccepted);
        first.RealizedRevision.Should().Be(0, "intent is not an acknowledged provider route");
        coordinator.Current.Routes.Should().ContainSingle().Which.EffectiveOwners.Should().Be(2);
        await coordinator.ReleaseAsync(Release(time, first.Lease!));
        coordinator.Current.Routes.Should().ContainSingle().Which.EffectiveOwners.Should().Be(1);
        await coordinator.ReleaseAsync(Release(time, second.Lease!));
        coordinator.Current.Routes.Should().BeEmpty();
    }

    [Fact]
    public async Task Exact_operation_retry_is_stable_and_changed_payload_conflicts()
    {
        var time = new LeaseTime();
        await using var coordinator = await Open(time);
        var request = Acquire(time, "one");
        var first = await coordinator.AcquireAsync(request);
        (await coordinator.AcquireAsync(request)).Should().Be(first);
        (await coordinator.AcquireAsync(request with { Target = new(Ticker("different")) })).Code
            .Should().Be(SubscriptionResultCode.Conflict);
        (await coordinator.AcquireAsync(Acquire(time, "one"))).Code.Should().Be(SubscriptionResultCode.AlreadyOwned);
        coordinator.Current.Leases.Should().ContainSingle();
    }

    [Fact]
    public async Task Expiry_is_monotonic_exact_and_old_incarnation_cannot_release_reacquired_lease()
    {
        var time = new LeaseTime();
        await using var coordinator = await Open(time);
        var original = (await coordinator.AcquireAsync(Acquire(time, "one"))).Lease!;
        time.JumpUtc(TimeSpan.FromDays(2));
        await coordinator.SweepAsync();
        coordinator.Current.Leases.Should().ContainSingle();
        time.Advance(TimeSpan.FromSeconds(119));
        await coordinator.SweepAsync();
        coordinator.Current.Leases.Should().ContainSingle();
        time.Advance(TimeSpan.FromSeconds(1));
        await coordinator.SweepAsync();
        coordinator.Current.Leases.Should().BeEmpty();
        (await coordinator.RenewAsync(Renew(time, original))).Code.Should().Be(SubscriptionResultCode.Expired);
        var replacement = (await coordinator.AcquireAsync(Acquire(time, "one"))).Lease!;
        replacement.Token.LeaseId.Should().NotBe(original.Token.LeaseId);
        (await coordinator.ReleaseAsync(Release(time, original))).Code.Should().Be(SubscriptionResultCode.NotOwned);
        coordinator.Current.Leases.Should().ContainSingle().Which.Token.Should().Be(replacement.Token);
    }

    [Fact]
    public async Task Renew_increments_version_and_late_release_does_not_remove_the_new_version()
    {
        var time = new LeaseTime();
        await using var coordinator = await Open(time);
        var original = (await coordinator.AcquireAsync(Acquire(time, "one"))).Lease!;
        time.Advance(TimeSpan.FromSeconds(30));
        var renewed = (await coordinator.RenewAsync(Renew(time, original))).Lease!;
        renewed.Token.Version.Should().Be(original.Token.Version + 1);
        (await coordinator.ReleaseAsync(Release(time, original))).Code.Should().Be(SubscriptionResultCode.Conflict);
        time.Advance(TimeSpan.FromSeconds(90));
        await coordinator.SweepAsync();
        coordinator.Current.Leases.Should().ContainSingle();
        time.Advance(TimeSpan.FromSeconds(30));
        await coordinator.SweepAsync();
        coordinator.Current.Leases.Should().BeEmpty();
    }

    [Fact]
    public async Task Recovery_rejects_new_acquisition_but_keeps_renew_and_release_available()
    {
        var time = new LeaseTime();
        await using var coordinator = await Open(time);
        var original = (await coordinator.AcquireAsync(Acquire(time, "one"))).Lease!;
        await coordinator.SetAvailabilityAsync(SubscriptionDatasetAvailability.Recovering);
        (await coordinator.AcquireAsync(Acquire(time, "two"))).Code.Should().Be(SubscriptionResultCode.Recovering);
        var renewed = await coordinator.RenewAsync(Renew(time, original));
        renewed.Code.Should().Be(SubscriptionResultCode.DesiredAccepted);
        (await coordinator.ReleaseAsync(Release(time, renewed.Lease!))).Code.Should().Be(SubscriptionResultCode.Released);
        coordinator.Current.Routes.Should().BeEmpty();
    }

    [Fact]
    public async Task Missing_durable_store_fails_closed_without_modifying_ephemeral_intent()
    {
        var time = new LeaseTime();
        await using var coordinator = await Open(time);
        (await coordinator.AcquireAsync(Acquire(time, "one", SubscriptionLeasePurpose.Position))).Code
            .Should().Be(SubscriptionResultCode.PersistenceUnavailable);
        coordinator.Current.Leases.Should().BeEmpty();
    }

    [Fact]
    public async Task Capacity_and_owner_token_mismatch_fail_without_evicting_accepted_intent()
    {
        var time = new LeaseTime();
        await using var coordinator = await Open(time, new() { MaximumLeases = 1 });
        var original = (await coordinator.AcquireAsync(Acquire(time, "one"))).Lease!;
        (await coordinator.AcquireAsync(Acquire(time, "two"))).Code.Should().Be(SubscriptionResultCode.CapacityExceeded);
        (await coordinator.ReleaseAsync(Release(time, original) with { Owner = Owner("attacker") })).Code
            .Should().Be(SubscriptionResultCode.NotOwned);
        coordinator.Current.Leases.Should().ContainSingle().Which.Token.Should().Be(original.Token);
    }

    [Fact]
    public async Task Chain_universe_conflict_is_atomic_and_option_dependency_shares_daily_futures()
    {
        var time = new LeaseTime();
        await using var coordinator = await Open(time);
        var underlying = Ticker("ES");
        var chain = new SubscriptionChainKey(underlying, new(2026, 9, 18), ValueDate, [Ticker("call", true), Ticker("put", true)]);
        var discoveryRequest = Acquire(time, "discovery", SubscriptionLeasePurpose.Discovery) with { Target = new(chain) };
        var discovery = (await coordinator.AcquireAsync(discoveryRequest)).Lease!;
        var daily = (await coordinator.AcquireAsync(Acquire(time, "daily"))).Lease!;
        coordinator.Current.Routes.Single(route => route.Ticker == underlying).EffectiveOwners.Should().Be(2);
        var revision = coordinator.Current.Revision;
        var conflicting = new SubscriptionChainKey(underlying, chain.MaturityDate, ValueDate, [Ticker("other", true)]);
        (await coordinator.AcquireAsync(Acquire(time, "other") with { Target = new(conflicting) })).Code
            .Should().Be(SubscriptionResultCode.Conflict);
        coordinator.Current.Revision.Should().Be(revision);
        await coordinator.ReleaseAsync(Release(time, daily));
        coordinator.Current.Routes.Should().HaveCount(3);
        await coordinator.ReleaseAsync(Release(time, discovery));
        coordinator.Current.Routes.Should().BeEmpty();
    }

    [Fact]
    public async Task Cancelled_and_expired_requests_never_mutate_intent_and_old_host_tokens_are_rejected()
    {
        var time = new LeaseTime();
        await using var coordinator = await Open(time);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        (await coordinator.AcquireAsync(Acquire(time, "one"), cancelled.Token)).Code.Should().Be(SubscriptionResultCode.Cancelled);
        (await coordinator.AcquireAsync(Acquire(time, "one") with { DeadlineUtc = time.GetUtcNow() })).Code
            .Should().Be(SubscriptionResultCode.Timeout);
        coordinator.Current.Leases.Should().BeEmpty();
        var original = (await coordinator.AcquireAsync(Acquire(time, "one"))).Lease!;
        (await coordinator.ReleaseAsync(Release(time, original) with
        {
            Lease = original.Token with { HostEpochId = Guid.NewGuid() }
        })).Code.Should().Be(SubscriptionResultCode.NotOwned);
    }

    [Fact]
    public async Task Concurrent_duplicate_acquires_have_one_lease_and_snapshots_are_immutable()
    {
        var time = new LeaseTime();
        await using var coordinator = await Open(time);
        var request = Acquire(time, "one");
        var empty = coordinator.Current;
        var results = await Task.WhenAll(Enumerable.Range(0, 100).Select(_ => coordinator.AcquireAsync(request)));
        results.Should().OnlyContain(result => result.Code == SubscriptionResultCode.DesiredAccepted);
        results.Select(result => result.Lease!.Token).Distinct().Should().ContainSingle();
        empty.Leases.Should().BeEmpty();
        coordinator.Current.Leases.Should().ContainSingle();
    }

    [Fact]
    public async Task Pruned_operation_cannot_be_reused_with_an_extended_deadline_or_changed_target()
    {
        var time = new LeaseTime();
        await using var coordinator = await Open(time);
        var request = Acquire(time, "one");
        await coordinator.AcquireAsync(request);
        time.Advance(TimeSpan.FromSeconds(11));
        (await coordinator.AcquireAsync(request with
        {
            DeadlineUtc = time.GetUtcNow().AddSeconds(10), Target = new(Ticker("other"))
        })).Code.Should().Be(SubscriptionResultCode.Timeout);
        coordinator.Current.Leases.Should().ContainSingle();
    }

    [Fact]
    public async Task Wall_clock_rewind_does_not_extend_operation_lifetime_or_ephemeral_expiry()
    {
        var time = new LeaseTime();
        await using var coordinator = await Open(time);
        var request = Acquire(time, "one");
        await coordinator.AcquireAsync(request);
        time.Advance(TimeSpan.FromSeconds(11));
        time.JumpUtc(TimeSpan.FromHours(-1));
        (await coordinator.AcquireAsync(request)).Code.Should().Be(SubscriptionResultCode.Timeout);
        time.Advance(TimeSpan.FromSeconds(109));
        await coordinator.SweepAsync();
        coordinator.Current.Leases.Should().BeEmpty();
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public async Task Atomic_selected_batch_commits_one_revision_and_keeps_shared_discovery(int legs)
    {
        var time = new LeaseTime();
        await using var coordinator = await Open(time);
        var options = Enumerable.Range(0, legs).Select(i => Ticker($"option-{i}", true)).ToArray();
        var chain = new SubscriptionChainKey(Ticker("ES"), new(2026, 9, 18), ValueDate, options);
        var discovery = (await coordinator.AcquireAsync(Acquire(time, "one", SubscriptionLeasePurpose.Discovery)
            with { Target = new(chain) })).Lease!;
        var revision = coordinator.Current.Revision;
        var selections = options.Select((option, i) => new SubscriptionLeaseSelection(
            new("account", new TickerStreamOwner("test", "one", i.ToString())), new(option, Ticker("ES")))).ToArray();
        var batch = new SubscriptionAcquireBatchRequest(Guid.CreateVersion7(time.GetUtcNow()), coordinator.HostEpochId,
            Guid.NewGuid(), Owner("one"), selections, SubscriptionLeasePurpose.Composer, time.GetUtcNow().AddSeconds(10));
        var accepted = await coordinator.AcquireBatchAsync(batch);
        accepted.Code.Should().Be(SubscriptionResultCode.DesiredAccepted);
        accepted.SelectedLeases.Should().HaveCount(legs);
        coordinator.Current.Revision.Should().Be(revision + 1);
        (await coordinator.AcquireBatchAsync(new(batch.OperationId, batch.HostEpochId, batch.CorrelationId,
            batch.Owner, selections.ToArray(), batch.Purpose, batch.DeadlineUtc))).Should().Be(accepted);
        await coordinator.ReleaseAsync(Release(time, discovery));
        coordinator.Current.Routes.Should().HaveCount(legs + 1);
        coordinator.Current.Routes.Single(route => route.Ticker.ContractId == "ES").EffectiveOwners.Should().Be(legs);
    }

    [Fact]
    public async Task Failed_batch_does_not_partially_add_or_remove_preexisting_ownership()
    {
        var time = new LeaseTime();
        await using var coordinator = await Open(time, new() { MaximumLeases = 2 });
        var existing = (await coordinator.AcquireAsync(Acquire(time, "one"))).Lease!;
        var selections = new[]
        {
            new SubscriptionLeaseSelection(Owner("one"), new(Ticker("ES"))),
            new SubscriptionLeaseSelection(Owner("one"), new(Ticker("second"))),
            new SubscriptionLeaseSelection(Owner("one"), new(Ticker("third")))
        };
        var batch = new SubscriptionAcquireBatchRequest(Guid.CreateVersion7(time.GetUtcNow()), coordinator.HostEpochId,
            Guid.NewGuid(), Owner("one"), selections, SubscriptionLeasePurpose.Composer, time.GetUtcNow().AddSeconds(10));
        (await coordinator.AcquireBatchAsync(batch)).Code.Should().Be(SubscriptionResultCode.CapacityExceeded);
        coordinator.Current.Leases.Should().ContainSingle().Which.Token.Should().Be(existing.Token);
    }

    [Fact]
    public async Task Owner_query_is_bounded_scoped_and_expires_intent_before_returning_it()
    {
        var time = new LeaseTime();
        await using var coordinator = await Open(time);
        await coordinator.AcquireAsync(Acquire(time, "one"));
        await coordinator.AcquireAsync(Acquire(time, "two"));
        var result = await coordinator.QueryAsync(new(Owner("one")));
        result.SelectedLeases.Should().ContainSingle().Which.Owner.Should().Be(Owner("one"));
        result.RealizedRevision.Should().Be(0);
        (await coordinator.QueryAsync(new(Owner("one"), PageSize: 129))).Code.Should().Be(SubscriptionResultCode.OwnershipUnverified);
        time.Advance(TimeSpan.FromSeconds(120));
        (await coordinator.QueryAsync(new(Owner("one")))).SelectedLeases.Should().BeEmpty();
    }

    [Fact]
    public async Task Api_restart_invalidates_ephemeral_acquisition_epoch_not_just_release_tokens()
    {
        var time = new LeaseTime();
        await using var original = await Open(time);
        var request = Acquire(time, "one");
        await original.AcquireAsync(request);
        await using var replacement = await Open(time);
        (await replacement.AcquireAsync(request)).Code.Should().Be(SubscriptionResultCode.NotOwned);
        replacement.Current.Leases.Should().BeEmpty();
    }

    static async Task<MarketDataSubscriptionCoordinator> Open(LeaseTime time, TickerLeasePolicy? policy = null)
    {
        var coordinator = new MarketDataSubscriptionCoordinator("account", "GLBX.MDP3", ValueDate, policy, time);
        time.HostEpochId = coordinator.HostEpochId;
        (await coordinator.SetAvailabilityAsync(SubscriptionDatasetAvailability.Open)).Should().BeTrue();
        return coordinator;
    }
    static SubscriptionOwnerKey Owner(string id) => new("account", new TickerStreamOwner("test", id, "leg"));
    static SubscriptionTickerKey Ticker(string id, bool option = false) => new("databento", "GLBX.MDP3", id, "mbp-1",
        option ? SubscriptionAssetKind.FuturesOption : SubscriptionAssetKind.Futures);
    static SubscriptionAcquireRequest Acquire(LeaseTime time, string owner,
        SubscriptionLeasePurpose purpose = SubscriptionLeasePurpose.Composer) => new(Guid.CreateVersion7(time.GetUtcNow()), time.HostEpochId, Guid.NewGuid(),
        Owner(owner), new(Ticker("ES")), purpose, time.GetUtcNow().AddSeconds(10));
    static SubscriptionReleaseRequest Release(LeaseTime time, SubscriptionLeaseView lease) => new(Guid.CreateVersion7(time.GetUtcNow()),
        Guid.NewGuid(), lease.Owner, lease.Token, time.GetUtcNow().AddSeconds(10));
    static SubscriptionRenewRequest Renew(LeaseTime time, SubscriptionLeaseView lease) => new(Guid.CreateVersion7(time.GetUtcNow()),
        Guid.NewGuid(), lease.Owner, lease.Token, time.GetUtcNow().AddSeconds(10));

    sealed class LeaseTime : TimeProvider
    {
        public Guid HostEpochId { get; set; }
        long timestamp;
        DateTimeOffset utc = new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => timestamp;
        public override DateTimeOffset GetUtcNow() => utc;
        public void Advance(TimeSpan value) { timestamp += value.Ticks; utc += value; }
        public void JumpUtc(TimeSpan value) => utc += value;
    }
}
