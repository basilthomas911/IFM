using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using FluentAssertions;
using System.Diagnostics;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Fund.Command;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Financial.Repository;
using TomasAI.IFM.Domain.Portfolio.Shared.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.FinancialPolicy.Events;
using TomasAI.IFM.Domain.Portfolio.Persistence;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.EventSourcing;
using Xunit.Abstractions;
using static TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence.GeneralLedgerPostingIntegrationTests;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence;

public sealed class PortfolioEventStoreIntegrationTests(PortfolioEventStoreFixture fixture, ITestOutputHelper output)
    : IClassFixture<PortfolioEventStoreFixture>
{
    [Fact]
    public async Task PostgreSQL_reloads_an_edited_manual_trade_from_latest_Fund_snapshot()
    {
        var value = Random.Shared.Next(100_000, 1_000_000_000);
        var id = new PortfolioFundId(value, value + 1);
        var now = new DateTime(2026, 11, 11, 14, 0, 0, DateTimeKind.Utc);
        var day = DateOnly.FromDateTime(now);
        var store = new PortfolioEventStore(fixture.EventSourceDb);
        var fund = new PortfolioFundAggregate();
        var created = fund.Create(Guid.NewGuid(), Mandate(id, now), now, "integration");
        await store.AppendFundAsync(id, created, 0);
        var activated = fund.ChangeState(Guid.NewGuid(), 1, FundOperatingState.Active, "activate",
            new(true, 1, true, true), now, "integration");
        await store.AppendFundAsync(id, activated, 1);
        var order = fund.CreateManualOrder(Guid.NewGuid(), new()
        {
            PortfolioId = id.PortfolioId, FundId = id.FundId, PortfolioVersion = 1, FundMandateVersion = 1,
            IdempotencyKey = Guid.NewGuid(), RequestedAtUtc = now, ExpiresAtUtc = now.AddDays(1), Reference = "Edit draft",
        }, 4001, now, "integration");
        await store.AppendFundAsync(id, order, 2);
        var added = fund.AddManualTrade(Guid.NewGuid(), new()
        {
            PortfolioId = id.PortfolioId, FundId = id.FundId, OrderId = 4001, TradeId = 4301,
            ExpectedOrderVersion = 1, BaseContractId = "ES20311219", BaseContractSymbol = "ES",
            TradeType = "ShortIronCondor", TradeAction = "Sell", TradeState = "NewTrade",
            PrimaryTrade = true, EffectiveDate = day, RequestedAtUtc = now, Reference = "Original",
        }, now, "integration");
        await store.AppendFundAsync(id, added, 3);
        await store.SaveFundSnapshotAsync(id, fund, now, "integration");
        var reloaded = await store.LoadFundAsync(id);
        var prior = reloaded.Composition(4001);
        var command = new ChangeManualFundOrderTradeCommand(new()
        {
            PortfolioId = id.PortfolioId, FundId = id.FundId, OrderId = 4001, TradeId = 4301,
            ExpectedOrderVersion = prior.AggregateVersion, ExpectedTradeVersion = prior.Trades[0].AggregateVersion,
            BaseContractId = "ES20261218", BaseContractSymbol = "ES", TradeType = "ShortIronCondor",
            Reference = "Changed", EffectiveDate = day,
        }) { CommandId = Guid.NewGuid(), EntityId = id };
        command.Execute(reloaded, now.AddSeconds(1), "integration").Success.Should().BeTrue();
        await store.AppendFundAsync(id, reloaded.PendingEvent!, 4);
        var replayed = await store.LoadFundAsync(id);
        await store.SaveFundSnapshotAsync(id, replayed, now.AddSeconds(2), "integration");
        var trade = (await store.LoadFundAsync(id)).Composition(4001).Trades.Should().ContainSingle().Which;
        trade.Should().BeEquivalentTo(replayed.Composition(4001).Trades.Single());
        trade.TradeId.Should().Be(4301);
        trade.BaseContractId.Should().Be("ES20261218");
        trade.TradeState.Should().Be("NewTrade");
        trade.DraftRevision.Should().Be(prior.AggregateVersion + 1);
        (await store.FindCommittedFundCommandAsync(id, command.CommandId)).Should().BeOfType<FundManualOrderChangedEvent>();
    }

    [Fact]
    [Trait("Gate", "PF-24")]
    [Trait("Gate", "PF-25")]
    [Trait("Category", "Portfolio")]
    public async Task PostgreSQL_policy_history_replays_immutable_versions_and_idempotent_command_lookup()
    {
        var value = Math.Abs(Guid.NewGuid().GetHashCode()) + 1000;
        var id = new PortfolioFinancialPolicyId(value, value + 1);
        var now = new DateTime(2026, 8, 30, 18, 0, 0, DateTimeKind.Utc);
        var store = new PortfolioEventStore(fixture.EventSourceDb);
        var aggregate = new PortfolioFinancialPolicyAggregate();
        var created = aggregate.Create(Guid.NewGuid(), Guid.NewGuid(), Policy(id, 1, now), now, "integration");
        await store.AppendPolicyAsync(id, created, 0);
        var activated = aggregate.Activate(Guid.NewGuid(), 1, 1, now.AddSeconds(1), "integration");
        await store.AppendPolicyAsync(id, activated, 1);
        await store.SavePolicySnapshotAsync(id, aggregate, now.AddSeconds(1), "integration");
        var draft = aggregate.AddVersion(Guid.NewGuid(), 2, Policy(id, 2, now), now.AddSeconds(2), "integration");
        await store.AppendPolicyAsync(id, draft, 2);

        var loaded = await store.LoadPolicyAsync(id);

        loaded.Current.Should().BeEquivalentTo(aggregate.Current);
        loaded.Revision.Should().Be(3);
        loaded.Versions.Should().BeEquivalentTo(aggregate.Versions);
        FluentActions.Invoking(() => loaded.DeleteDraft(Guid.NewGuid(), 3, "duplicate", false, now.AddSeconds(3), "integration"))
            .Should().Throw<InvalidOperationException>().WithMessage("*never-active*");
        (await store.FindCommittedPolicyCommandAsync(id, activated.CommandId)).Should().BeOfType<PortfolioFinancialPolicyActivatedEvent>();
        (await store.LoadPolicyHistoryAsync(id)).Should().HaveCount(3);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Gate", "PF-03")]
    [Trait("Gate", "PF-07")]
    [Trait("Category", "Portfolio")]
    public async Task PostgreSQL_authority_retains_Draft_deletion_tombstone_and_consumed_identity(bool legacyCheckpoint)
    {
        var id = Random.Shared.Next(100_000, 2_000_000_000);
        var portfolioId = new PortfolioId(id);
        var now = new DateTime(2026, 8, 30, 16, 0, 0, DateTimeKind.Utc);
        IPortfolioAuthorityFence? fence = null;
        if (!legacyCheckpoint)
        {
            var transactions = Transactions();
            await new PortfolioFinancialSchema(transactions).InitializeAsync();
            fence = new PortfolioAuthorityFence(transactions);
        }
        var store = new PortfolioEventStore(fixture.EventSourceDb, fence);
        var aggregate = new PortfolioAggregate();
        var created = aggregate.Create(Guid.NewGuid(), Portfolio(id, now), now, "integration");
        await store.AppendPortfolioAsync(portfolioId, created, 0);
        var deleted = aggregate.DeleteDraft(Guid.NewGuid(), 1, "duplicate", now.AddSeconds(1), "integration");
        var sidecar = PortfolioEventStore.PortfolioSnapshotStream(portfolioId);
        if (legacyCheckpoint)
        {
            await store.AppendPortfolioAsync(portfolioId, deleted, 1);
            var legacy = new PortfolioSnapshotCaptured(Guid.NewGuid(), Guid.NewGuid(), 2,
                aggregate.CaptureSnapshot() with { IsDeleted = null }, now, "integration");
            await fixture.EventSourceDb.SaveEventsAsync(sidecar, legacy.CommandId, new DomainEventCollection([legacy]), 0,
                CancellationToken.None);
        }
        else
            await store.AppendPortfolioWithStateAsync(portfolioId, deleted, 1, aggregate);

        var loaded = await store.LoadPortfolioAsync(portfolioId);
        var history = await store.LoadPortfolioHistoryAsync(portfolioId);

        loaded.IsDeleted.Should().BeTrue();
        loaded.Current!.PortfolioId.Should().Be(id);
        loaded.Revision.Should().Be(2);
        history.Should().HaveCount(2);
        history[^1].Should().BeOfType<DraftPortfolioDeletedEvent>().Which.Reason.Should().Be("duplicate");
        var window = await fixture.EventSourceDb.ReadAggregateCheckpointWindowAsync(PortfolioEventStore.PortfolioStream(portfolioId),
            sidecar, CancellationToken.None);
        window.Rows.Should().ContainSingle().Which.Kind.Should().Be(legacyCheckpoint
            ? AggregateCheckpointRowKind.LegacyCheckpoint : AggregateCheckpointRowKind.IndexedCheckpoint);
    }

    [Fact]
    [Trait("Gate", "PF-07")]
    [Trait("Category", "Portfolio")]
    public async Task PostgreSQL_streams_rebuild_Portfolio_and_Fund_and_reject_stale_revision()
    {
        var suffix = Math.Abs(Guid.NewGuid().GetHashCode()) + 1000;
        var portfolioId = new PortfolioId(suffix);
        var fundId = new PortfolioFundId(suffix, suffix + 1);
        var now = new DateTime(2026, 8, 29, 16, 0, 0, DateTimeKind.Utc);
        var store = new PortfolioEventStore(fixture.EventSourceDb);
        var portfolio = new PortfolioAggregate();
        var created = portfolio.Create(Guid.NewGuid(), Portfolio(suffix, now), now, "integration");
        await store.AppendPortfolioAsync(portfolioId, created, 0);
        var fundAdded = portfolio.AddFund(Guid.NewGuid(), 1, fundId, now.AddSeconds(1), "integration");
        await store.AppendPortfolioAsync(portfolioId, fundAdded, 1);

        var fund = new PortfolioFundAggregate();
        var mandateCreated = fund.Create(Guid.NewGuid(), Mandate(fundId, now), now, "integration");
        await store.AppendFundAsync(fundId, mandateCreated, 0);
        await store.SavePortfolioSnapshotAsync(portfolioId, portfolio, now.AddSeconds(2), "integration");
        await store.SaveFundSnapshotAsync(fundId, fund, now.AddSeconds(2), "integration");

        var loadedPortfolio = await store.LoadPortfolioAsync(portfolioId);
        var loadedFund = await store.LoadFundAsync(fundId);

        loadedPortfolio.Revision.Should().Be(2);
        loadedPortfolio.FundIds.Should().Contain(fundId.FundId);
        loadedFund.Revision.Should().Be(1);
        loadedFund.Current.Should().BeEquivalentTo(fund.Current);
        (await store.FindCommittedPortfolioCommandAsync(portfolioId, created.CommandId)).Should().BeOfType<PortfolioCreatedEvent>();
        (await store.FindCommittedFundCommandAsync(fundId, mandateCreated.CommandId)).Should().BeOfType<FundMandateCreatedEvent>();
        var persisted = await fixture.EventSourceDb.LoadActorEventStreamAsync<TestState>(
            (await fixture.EventSourceDb.GetEventStreamIdFromDbAsync(PortfolioEventStore.PortfolioStream(portfolioId)))!.EventStreamId);
        var persistedCreated = persisted.Select(x => x.ToDomainEvent()).OfType<PortfolioCreatedEvent>().Single();
        persistedCreated.CorrelationId.Should().Be(created.CommandId);
        persistedCreated.CausationId.Should().Be(created.Id);
        await FluentActions.Invoking(() => store.AppendPortfolioAsync(portfolioId, fundAdded, 1))
            .Should().ThrowAsync<ConcurrencyException>();
    }

    [Fact]
    [Trait("Gate", "PF-07")]
    [Trait("Category", "Portfolio")]
    public async Task Incompatible_event_in_authority_stream_is_rejected_as_corruption()
    {
        var id = Math.Abs(Guid.NewGuid().GetHashCode()) + 1000;
        var portfolioId = new PortfolioId(id);
        var fundId = new PortfolioFundId(id, id + 1);
        var now = new DateTime(2026, 8, 29, 18, 0, 0, DateTimeKind.Utc);
        var incompatible = new FundMandateCreatedEvent(Guid.NewGuid(), Guid.NewGuid(), 1, now, "integration", Mandate(fundId, now));
        await fixture.EventSourceDb.SaveEventsAsync(
            PortfolioEventStore.PortfolioStream(portfolioId), incompatible.CommandId,
            new DomainEventCollection([incompatible]), 0, CancellationToken.None);

        await FluentActions.Invoking(() => new PortfolioEventStore(fixture.EventSourceDb).LoadPortfolioAsync(portfolioId))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*unknown or incompatible*");
    }

    [Fact]
    [Trait("Category", "Portfolio")]
    public async Task PostgreSQL_latest_full_checkpoints_restore_only_the_tail_and_match_full_replay_with_stream_resolution()
    {
        const int latest = 128;
        const int head = 131;
        var identity = Random.Shared.Next(100_000, 2_000_000_000);
        var portfolioId = new PortfolioId(identity);
        var fundId = new PortfolioFundId(identity, identity + 1);
        var policyId = new PortfolioFinancialPolicyId(identity, identity + 2);
        var now = new DateTime(2026, 8, 29, 18, 0, 0, DateTimeKind.Utc);
        var store = new PortfolioEventStore(fixture.EventSourceDb);
        var portfolio = new PortfolioAggregate();
        var fund = new PortfolioFundAggregate();
        var policy = new PortfolioFinancialPolicyAggregate();
        await store.AppendPortfolioAsync(portfolioId, portfolio.Create(Guid.NewGuid(), Portfolio(identity, now), now, "integration"), 0);
        await store.AppendFundAsync(fundId, fund.Create(Guid.NewGuid(), Mandate(fundId, now), now, "integration"), 0);
        await store.AppendPolicyAsync(policyId, policy.Create(Guid.NewGuid(), Guid.NewGuid(), Policy(policyId, 1, now), now, "integration"), 0);
        (await store.LoadPortfolioAsync(portfolioId)).Revision.Should().Be(1);
        (await store.LoadFundAsync(fundId)).Revision.Should().Be(1);
        (await store.LoadPolicyAsync(policyId)).Revision.Should().Be(1);
        for (var revision = 2; revision <= head; revision++)
        {
            var time = now.AddSeconds(revision);
            var portfolioEvent = portfolio.AddFund(Guid.NewGuid(), revision - 1, new PortfolioFundId(identity, revision), time, "integration");
            var fundEvent = new FundMandateVersionAddedEvent(Guid.NewGuid(), Guid.NewGuid(), revision, time, "integration",
                Mandate(fundId, now) with { FundMandateVersion = revision, Name = $"Mandate {revision}" });
            fund.Replay([fundEvent]);
            var policyEvent = policy.AddVersion(Guid.NewGuid(), revision - 1, Policy(policyId, revision, now), time, "integration");
            await store.AppendPortfolioWithStateAsync(portfolioId, portfolioEvent, revision - 1, portfolio);
            await store.AppendFundWithStateAsync(fundId, fundEvent, revision - 1, fund);
            await store.AppendPolicyWithStateAsync(policyId, policyEvent, revision - 1, policy);
            if (revision != 64) continue;
            await store.SavePortfolioSnapshotAsync(portfolioId, portfolio, time, "integration");
            await store.SaveFundSnapshotAsync(fundId, fund, time, "integration");
            await store.SavePolicySnapshotAsync(policyId, policy, time, "integration");
        }

        await CompareAsync("Portfolio", PortfolioEventStore.PortfolioStream(portfolioId), PortfolioEventStore.PortfolioSnapshotStream(portfolioId),
            async () =>
            {
                var history = await store.LoadPortfolioHistoryAsync(portfolioId);
                var checkpoint = await LoadPriorSidecarAsync<PortfolioSnapshotCaptured>(
                    PortfolioEventStore.PortfolioSnapshotStream(portfolioId), item => item.SourceRevision);
                var result = new PortfolioAggregate();
                if (checkpoint is not null) result.RestoreSnapshot(checkpoint.State);
                result.Replay(history.Where(item => item.Revision > result.Revision));
                return result.CaptureSnapshot();
            },
            async () => (await store.LoadPortfolioAsync(portfolioId)).CaptureSnapshot(), portfolio.CaptureSnapshot(), head + 2);
        await CompareAsync("Fund", PortfolioEventStore.FundStream(fundId), PortfolioEventStore.FundSnapshotStream(fundId),
            async () =>
            {
                var history = await store.LoadFundHistoryAsync(fundId);
                var checkpoint = await LoadPriorSidecarAsync<PortfolioFundSnapshotCaptured>(
                    PortfolioEventStore.FundSnapshotStream(fundId), item => item.SourceRevision);
                var result = new PortfolioFundAggregate();
                if (checkpoint is not null) result.RestoreSnapshot(checkpoint.State);
                result.Replay(history.Where(item => item.Revision > result.Revision));
                return result.CaptureSnapshot();
            },
            async () => (await store.LoadFundAsync(fundId)).CaptureSnapshot(), fund.CaptureSnapshot(), head + 2);
        await CompareAsync("Financial Policy", PortfolioEventStore.PolicyStream(policyId), PortfolioEventStore.PolicySnapshotStream(policyId),
            async () =>
            {
                var result = new PortfolioFinancialPolicyAggregate();
                result.Replay(await store.LoadPolicyHistoryAsync(policyId));
                return result.CaptureSnapshot();
            },
            async () => (await store.LoadPolicyAsync(policyId)).CaptureSnapshot(), policy.CaptureSnapshot(), head);

        async Task<T?> LoadPriorSidecarAsync<T>(string sidecar, Func<T, long> revision) where T : class, IEvent
        {
            var stream = await fixture.EventSourceDb.GetEventStreamIdFromDbAsync(sidecar);
            if (stream is null) return null;
            var rows = await fixture.EventSourceDb.LoadActorEventStreamAsync<TestState>(stream.EventStreamId);
            return rows.Select(row => row.ToDomainEvent()).OfType<T>().Where(item => revision(item) <= head)
                .OrderByDescending(revision).FirstOrDefault();
        }

        async Task CompareAsync<T>(string owner, string stream, string sidecar, Func<Task<T>> before, Func<Task<T>> after,
            T expected, int beforeRows)
        {
            var window = await fixture.EventSourceDb.ReadAggregateCheckpointWindowAsync(stream, sidecar, CancellationToken.None);
            window.Head.Should().Be(head);
            window.Rows.Should().HaveCount(head - latest + 1);
            window.Rows.Single(row => row.Kind == AggregateCheckpointRowKind.IndexedCheckpoint).SourceRevision.Should().Be(latest);
            (await before()).Should().BeEquivalentTo(expected);
            (await after()).Should().BeEquivalentTo(expected);
            var baseline = new double[20];
            var candidate = new double[20];
            for (var sample = 0; sample < baseline.Length; sample++)
            {
                if (sample % 2 == 0)
                {
                    baseline[sample] = await TimeAsync(before);
                    candidate[sample] = await TimeAsync(after);
                }
                else
                {
                    candidate[sample] = await TimeAsync(after);
                    baseline[sample] = await TimeAsync(before);
                }
            }
            Array.Sort(baseline);
            Array.Sort(candidate);
            output.WriteLine($"{owner}: {beforeRows} -> {window.Rows.Count} rows, median {Median(baseline):F2} -> {Median(candidate):F2} ms, p95 {baseline[18]:F2} -> {candidate[18]:F2} ms (stream resolution included).");
        }

        static double Median(double[] samples) => (samples[9] + samples[10]) / 2;
        static async Task<double> TimeAsync<T>(Func<Task<T>> run)
        {
            var start = Stopwatch.GetTimestamp();
            await run();
            return Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        }
    }

    [Fact]
    [Trait("Category", "Portfolio")]
    public async Task PostgreSQL_rejects_invalid_checkpoint_and_recovers_from_compatible_legacy_sidecar()
    {
        var identity = Random.Shared.Next(100_000, 2_000_000_000);
        var id = new PortfolioId(identity);
        var stream = PortfolioEventStore.PortfolioStream(id);
        var sidecar = PortfolioEventStore.PortfolioSnapshotStream(id);
        var now = new DateTime(2026, 8, 29, 18, 0, 0, DateTimeKind.Utc);
        var store = new PortfolioEventStore(fixture.EventSourceDb);
        var source = new PortfolioAggregate();
        await store.AppendPortfolioAsync(id, source.Create(Guid.NewGuid(), Portfolio(identity, now), now, "integration"), 0);
        await store.AppendPortfolioAsync(id, source.AddFund(Guid.NewGuid(), 1, new PortfolioFundId(identity, 201), now, "integration"), 1);
        var legacy = new PortfolioSnapshotCaptured(Guid.NewGuid(), Guid.NewGuid(), 2, source.CaptureSnapshot(), now, "integration");
        await fixture.EventSourceDb.SaveEventsAsync(sidecar, legacy.CommandId, new DomainEventCollection([legacy]), 0, CancellationToken.None);
        await store.AppendPortfolioAsync(id, source.AddFund(Guid.NewGuid(), 2, new PortfolioFundId(identity, 202), now, "integration"), 2);
        var latest = await fixture.EventSourceDb.ReadAggregateCheckpointWindowAsync(stream, sidecar, CancellationToken.None);
        latest.Rows.Should().ContainSingle().Which.Kind.Should().Be(AggregateCheckpointRowKind.LegacyCheckpoint);
        var invalid = new PortfolioSnapshotCaptured(Guid.NewGuid(), Guid.NewGuid(), 3, legacy.State, now, "integration");
        await fixture.EventSourceDb.SaveAggregateCheckpointAsync(stream, sidecar, 3, invalid, CancellationToken.None);

        var restored = await store.LoadPortfolioAsync(id);

        restored.CaptureSnapshot().Should().BeEquivalentTo(source.CaptureSnapshot());
        await FluentActions.Invoking(() => fixture.EventSourceDb.SaveAggregateCheckpointAsync(
                stream, sidecar, 4, invalid, CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*committed source revision*");
    }

    static PortfolioReadModel Portfolio(int id, DateTime now) => new()
    {
        PortfolioId = id,
        Name = "Core",
        PortfolioVersion = 1,
        OperatingState = PortfolioOperatingState.Draft,
        EffectiveFromUtc = now,
        CreatedOnUtc = now,
        CreatedBy = "integration"
    };

    static FundMandateReadModel Mandate(PortfolioFundId id, DateTime now) => new()
    {
        PortfolioId = id.PortfolioId,
        FundId = id.FundId,
        FundCode = $"F{id.FundId}",
        Name = "Directional",
        FundMandateVersion = 1,
        TradingYear = 2026,
        OperatingState = FundOperatingState.Draft,
        DecisionHorizon = "Daily",
        Objective = "Directional futures",
        UnderlyingUniverse = ["ES"],
        EligibleAssetTypes = ["Futures"],
        PermittedDirections = ["Long", "Short"],
        PermittedConditions = ["Trending"],
        PermittedTradeFamilies = ["Futures"],
        EffectiveFromUtc = now,
        CreatedOnUtc = now,
        CreatedBy = "integration"
    };

    static PortfolioFinancialPolicyReadModel Policy(PortfolioFinancialPolicyId id, long version, DateTime now) => new()
    {
        PortfolioId = id.PortfolioId,
        PolicyId = id.PolicyId,
        PolicyVersion = version,
        Name = "Integration limits",
        OperatingState = PortfolioFinancialPolicyState.Draft,
        CapitalBase = 1_000_000,
        MaximumDeployableCapital = 900_000,
        MaximumRiskPerTrade = 10_000,
        MaximumAggregateRisk = 100_000,
        MaximumMargin = 500_000,
        MaximumGrossNotional = 5_000_000,
        MaximumOpenPositions = 100,
        MaximumDrawdownAmount = 200_000,
        TradeFamilyLimits = [new() { TradeStrategyFamilyId = 1, DefinitionVersion = 1, Enabled = true, MaximumRiskPerTrade = 5_000, MaximumAggregateRisk = 50_000, MaximumMargin = 250_000, MaximumGrossNotional = 2_500_000, MaximumOpenPositions = 50 }],
        EffectiveFromUtc = now.AddMinutes(-1),
        CreatedOnUtc = now,
        CreatedBy = "integration"
    };

    sealed class TestState : TomasAI.IFM.Shared.EventModelActor.Contracts.IActorState<TestState>
    {
        public TomasAI.IFM.Shared.EventModelActor.ActorThreadId Id { get; set; }
    }
}
