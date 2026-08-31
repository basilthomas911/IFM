using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Command.Model;
using TomasAI.IFM.Domain.Portfolio.Persistence;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence;

public sealed class PortfolioEventStoreIntegrationTests(PortfolioEventStoreFixture fixture)
    : IClassFixture<PortfolioEventStoreFixture>
{
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

        var loaded = await store.LoadPolicyAsync(id);

        loaded.Current.Should().BeEquivalentTo(aggregate.Current);
        loaded.Revision.Should().Be(2);
        (await store.FindCommittedPolicyCommandAsync(id, activated.CommandId)).Should().BeOfType<PortfolioFinancialPolicyActivated>();
        (await store.LoadPolicyHistoryAsync(id)).Should().HaveCount(2);
    }

    [Fact]
    [Trait("Gate", "PF-03")]
    [Trait("Gate", "PF-07")]
    [Trait("Category", "Portfolio")]
    public async Task PostgreSQL_authority_retains_Draft_deletion_tombstone_and_consumed_identity()
    {
        var id = Math.Abs(Guid.NewGuid().GetHashCode()) + 1000;
        var portfolioId = new PortfolioId(id);
        var now = new DateTime(2026, 8, 30, 16, 0, 0, DateTimeKind.Utc);
        var store = new PortfolioEventStore(fixture.EventSourceDb);
        var aggregate = new PortfolioAggregate();
        var created = aggregate.Create(Guid.NewGuid(), Portfolio(id, now), now, "integration");
        await store.AppendPortfolioAsync(portfolioId, created, 0);
        var deleted = aggregate.DeleteDraft(Guid.NewGuid(), 1, "duplicate", now.AddSeconds(1), "integration");
        await store.AppendPortfolioAsync(portfolioId, deleted, 1);

        var loaded = await store.LoadPortfolioAsync(portfolioId);
        var history = await store.LoadPortfolioHistoryAsync(portfolioId);

        loaded.IsDeleted.Should().BeTrue();
        loaded.Current!.PortfolioId.Should().Be(id);
        loaded.Revision.Should().Be(2);
        history.Should().HaveCount(2);
        history[^1].Should().BeOfType<DraftPortfolioDeleted>().Which.Reason.Should().Be("duplicate");
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
        (await store.FindCommittedPortfolioCommandAsync(portfolioId, created.CommandId)).Should().BeOfType<PortfolioCreated>();
        (await store.FindCommittedFundCommandAsync(fundId, mandateCreated.CommandId)).Should().BeOfType<FundMandateCreated>();
        var persisted = await fixture.EventSourceDb.LoadActorEventStreamAsync<TestState>(
            (await fixture.EventSourceDb.GetEventStreamIdFromDbAsync(PortfolioEventStore.PortfolioStream(portfolioId)))!.EventStreamId);
        var persistedCreated = persisted.Select(x => x.ToDomainEvent()).OfType<PortfolioCreated>().Single();
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
        var incompatible = new FundMandateCreated(Guid.NewGuid(), Guid.NewGuid(), 1, now, "integration", Mandate(fundId, now));
        await fixture.EventSourceDb.SaveEventsAsync(
            PortfolioEventStore.PortfolioStream(portfolioId), incompatible.CommandId,
            new DomainEventCollection([incompatible]), 0, CancellationToken.None);

        await FluentActions.Invoking(() => new PortfolioEventStore(fixture.EventSourceDb).LoadPortfolioAsync(portfolioId))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*unknown or incompatible*");
    }

    static PortfolioReadModel Portfolio(int id, DateTime now) => new()
    {
        PortfolioId = id, Name = "Core", PortfolioVersion = 1,
        OperatingState = PortfolioOperatingState.Draft, EffectiveFromUtc = now,
        CreatedOnUtc = now, CreatedBy = "integration"
    };

    static FundMandateReadModel Mandate(PortfolioFundId id, DateTime now) => new()
    {
        PortfolioId = id.PortfolioId, FundId = id.FundId, FundCode = $"F{id.FundId}", Name = "Directional",
        FundMandateVersion = 1, TradingYear = 2026, OperatingState = FundOperatingState.Draft,
        DecisionHorizon = "Daily", Objective = "Directional futures",
        UnderlyingUniverse = ["ES"], EligibleAssetTypes = ["Futures"], PermittedDirections = ["Long", "Short"],
        PermittedConditions = ["Trending"], PermittedTradeFamilies = ["Futures"],
        EffectiveFromUtc = now, CreatedOnUtc = now, CreatedBy = "integration"
    };

    static PortfolioFinancialPolicyReadModel Policy(PortfolioFinancialPolicyId id, long version, DateTime now) => new()
    {
        PortfolioId = id.PortfolioId, PolicyId = id.PolicyId, PolicyVersion = version, Name = "Integration limits",
        OperatingState = PortfolioFinancialPolicyState.Draft, CapitalBase = 1_000_000, MaximumDeployableCapital = 900_000,
        MaximumRiskPerTrade = 10_000, MaximumAggregateRisk = 100_000, MaximumMargin = 500_000,
        MaximumGrossNotional = 5_000_000, MaximumOpenPositions = 100, MaximumDrawdownAmount = 200_000,
        TradeFamilyLimits = [new() { TradeStrategyFamilyId = 1, DefinitionVersion = 1, Enabled = true, MaximumRiskPerTrade = 5_000, MaximumAggregateRisk = 50_000, MaximumMargin = 250_000, MaximumGrossNotional = 2_500_000, MaximumOpenPositions = 50 }],
        EffectiveFromUtc = now.AddMinutes(-1), CreatedOnUtc = now, CreatedBy = "integration"
    };

    sealed class TestState : TomasAI.IFM.Shared.EventModelActor.Contracts.IActorState<TestState>
    {
        public TomasAI.IFM.Shared.EventModelActor.ActorThreadId Id { get; set; }
    }
}
