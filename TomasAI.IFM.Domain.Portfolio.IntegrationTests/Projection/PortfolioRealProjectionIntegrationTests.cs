using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Command.Model;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence;
using TomasAI.IFM.Domain.Portfolio.Persistence;
using TomasAI.IFM.Domain.Portfolio.Projection;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TomasAI.IFM.Application.Storage.PortfolioDb;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Projection;

[Collection(PortfolioPersistenceCollection.Name)]
public sealed class PortfolioRealProjectionIntegrationTests(
    PortfolioEventStoreFixture eventSource,
    PortfolioDbFixture projections)
    : IClassFixture<PortfolioEventStoreFixture>, IClassFixture<PortfolioDbFixture>
{
    [Fact]
    [Trait("Gate", "PF-09")]
    [Trait("Category", "Portfolio")]
    public async Task PostgreSQL_history_rebuilds_into_empty_Scylla_projection_and_duplicate_delivery_is_idempotent()
    {
        var value = Math.Abs(Guid.NewGuid().GetHashCode()) + 1000;
        var id = new PortfolioId(value);
        var now = new DateTime(2026, 8, 30, 12, 0, 0, DateTimeKind.Utc);
        var aggregate = new PortfolioAggregate();
        var store = new PortfolioEventStore(eventSource.EventSourceDb);
        var created = aggregate.Create(Guid.NewGuid(), new PortfolioReadModel
        {
            PortfolioId = value,
            Name = "Projection rebuild",
            PortfolioVersion = 1,
            OperatingState = PortfolioOperatingState.Draft,
            ActivePolicyId = 9001,
            ActivePolicyVersion = 1,
            EffectiveFromUtc = now,
            CreatedOnUtc = now,
            CreatedBy = "integration",
        }, now, "integration");
        await store.AppendPortfolioAsync(id, created, 0);
        var activated = aggregate.ChangeState(Guid.NewGuid(), 1, PortfolioOperatingState.Active, "approved", now.AddSeconds(1), "integration");
        await store.AppendPortfolioAsync(id, activated, 1);
        var handler = new PortfolioProjectionHandler(store, projections.Db);

        (await projections.Db.GetPortfolioAsync(value)).Should().BeNull();
        await handler.ApplyAsync(created);
        await handler.ApplyAsync(activated);
        await handler.ApplyAsync(created);
        await handler.ApplyAsync(activated);

        var rebuiltAuthority = await store.LoadPortfolioAsync(id);
        var projected = await projections.Db.GetPortfolioAsync(value);
        projected.Should().BeEquivalentTo(rebuiltAuthority.Current);
        projected!.OperatingState.Should().Be(PortfolioOperatingState.Active);
        (await projections.Db.GetPortfoliosByStateAsync(
            PortfolioOperatingState.Active,
            PortfolioProjectionHandler.StateBucket(value),
            0,
            100)).Count(x => x.PortfolioId == value).Should().Be(1);
    }

    [Fact]
    [Trait("Gate", "PF-09")]
    [Trait("Gate", "PF-25")]
    [Trait("Category", "Portfolio")]
    public async Task Representative_authority_catalog_rebuilds_from_empty_Scylla_with_identical_query_hash()
    {
        var value = Math.Abs(Guid.NewGuid().GetHashCode()) + 100_000;
        var portfolioId = new PortfolioId(value);
        var fundId = new PortfolioFundId(value, value + 1);
        var policyId = new PortfolioFinancialPolicyId(value, value + 10);
        var now = new DateTime(2026, 8, 30, 13, 0, 0, DateTimeKind.Utc);
        var workflowId = Guid.NewGuid();
        var portfolio = new PortfolioReadModel { PortfolioId = value, Name = "Representative", PortfolioVersion = 1, OperatingState = PortfolioOperatingState.Active, ActivePolicyId = policyId.PolicyId, ActivePolicyVersion = 1, EffectiveFromUtc = now, CreatedOnUtc = now, CreatedBy = "rebuild" };
        var policy = new PortfolioFinancialPolicyReadModel { PortfolioId = value, PolicyId = policyId.PolicyId, PolicyVersion = 1, Name = "Representative limits", OperatingState = PortfolioFinancialPolicyState.Draft, BaseCurrency = "USD", CapitalBase = 1_000_000, MaximumDeployableCapital = 900_000, MaximumRiskPerTrade = 10_000, MaximumAggregateRisk = 100_000, MaximumMargin = 500_000, MaximumGrossNotional = 5_000_000, MaximumOpenPositions = 100, MaximumDrawdownAmount = 200_000, TradeFamilyLimits = [new() { TradeStrategyFamilyId = 1, DefinitionVersion = 1, Enabled = true, MaximumRiskPerTrade = 5_000, MaximumAggregateRisk = 50_000, MaximumMargin = 250_000, MaximumGrossNotional = 2_500_000, MaximumOpenPositions = 50 }], EffectiveFromUtc = now, CreatedOnUtc = now, CreatedBy = "rebuild" };
        var fund = new FundMandateReadModel { PortfolioId = value, FundId = value + 1, FundCode = $"F{value}", Name = "Daily", FundMandateVersion = 1, TradingYear = 2026, OperatingState = FundOperatingState.Active, EffectiveFromUtc = now, DecisionHorizon = "Daily", Objective = "Directional", UnderlyingUniverse = ["ES"], EligibleAssetTypes = ["Futures"], PermittedTradeFamilies = ["Futures"], CreatedOnUtc = now, CreatedBy = "rebuild" };
        var assignment = new FundTradeTemplateAssignmentReadModel { PortfolioId = value, PortfolioVersion = 1, FundId = value + 1, FundMandateVersion = 1, AssignmentVersion = 1, TradeTemplateId = Guid.NewGuid(), TradeTemplateVersion = 1, Enabled = true, DecisionHorizon = "Daily", UnderlyingUniverse = ["ES"], AssetType = "Futures", TradeFamily = "Futures", Priority = 1, EffectiveFromUtc = now, TradeSelectionHintProfileId = Guid.NewGuid(), TradeSelectionHintProfileVersion = 1, OrderCompositionProfileId = Guid.NewGuid(), OrderCompositionProfileVersion = 1, CreatedOnUtc = now, CreatedBy = "rebuild" };
        var allocation = new FundAllocationReadModel { PortfolioId = value, PortfolioVersion = 1, FundId = value + 1, FundMandateVersion = 1, AllocationVersion = 1, TargetWeight = .5m, MaximumWeight = 1, AllocatedCapital = 100000, Currency = "USD", EffectiveFromUtc = now, SourcePolicyId = policyId.PolicyId, SourcePolicyVersion = 1, CreatedOnUtc = now, CreatedBy = "rebuild" };
        var envelope = new FundRiskEnvelopeReadModel { PortfolioId = value, PortfolioVersion = 1, FundId = value + 1, FundMandateVersion = 1, EnvelopeId = Guid.NewGuid(), EnvelopeVersion = 1, CapacityState = FundCapacityState.Available, Currency = "USD", AllocatedCapital = 100000, AvailableCapital = 90000, MaximumRiskPerTrade = 1000, MaximumAggregateRisk = 5000, MaximumMargin = 50000, MaximumGrossNotional = 500000, MaximumContracts = 10, MaximumOpenPositions = 5, RemainingLossBudget = 10000, EffectiveFromUtc = now, ExpiresAtUtc = now.AddDays(30), SourcePolicyId = policyId.PolicyId, SourcePolicyVersion = 1, CreatedOnUtc = now, CreatedBy = "rebuild" };
        var idempotency = Guid.NewGuid();
        var reservation = new FundCompositionReservationResult
        {
            Order = new() { PortfolioId = value, FundId = value + 1, OrderId = value + 2, WorkflowId = workflowId, IdempotencyKey = idempotency, Status = FundCompositionState.TemplateSelected.ToString(), CreatedOnUtc = now, CreatedBy = "rebuild", AggregateVersion = 3, CanonicalRequestHash = new string('a', 64) },
            Trades = [new() { PortfolioId = value, FundId = value + 1, OrderId = value + 2, TradeId = value + 3, TradeFamily = "Futures", InstructionReference = "ES", LegOrdinal = 1, AggregateVersion = 3 }],
            AggregateVersion = 3, CommittedOnUtc = now, Disposition = ReservationDisposition.Committed, CanonicalRequestSha256 = new string('a', 64),
        };
        var store = new PortfolioEventStore(eventSource.EventSourceDb);
        PortfolioDomainEvent[] portfolioHistory =
        [
            new PortfolioCreated(Guid.NewGuid(), Guid.NewGuid(), 1, now, "rebuild", portfolio),
            new FundAddedToPortfolio(Guid.NewGuid(), Guid.NewGuid(), 2, now.AddSeconds(1), "rebuild", fundId),
            new FundAllocationDelegated(Guid.NewGuid(), Guid.NewGuid(), 3, now.AddSeconds(2), "rebuild", allocation),
            new FundRiskEnvelopeDelegated(Guid.NewGuid(), Guid.NewGuid(), 4, now.AddSeconds(3), "rebuild", envelope),
        ];
        PortfolioFundDomainEvent[] fundHistory =
        [
            new FundMandateCreated(Guid.NewGuid(), Guid.NewGuid(), 1, now, "rebuild", fund),
            new FundTradeTemplateAssigned(Guid.NewGuid(), Guid.NewGuid(), 2, now.AddSeconds(1), "rebuild", assignment),
            new FundCompositionReserved(Guid.NewGuid(), Guid.NewGuid(), 3, now.AddSeconds(2), "rebuild", reservation),
        ];
        for (var index = 0; index < portfolioHistory.Length; index++) await store.AppendPortfolioAsync(portfolioId, portfolioHistory[index], index);
        for (var index = 0; index < fundHistory.Length; index++) await store.AppendFundAsync(fundId, fundHistory[index], index);
        var policyAggregate = new PortfolioFinancialPolicyAggregate();
        var policyCreated = policyAggregate.Create(Guid.NewGuid(), Guid.NewGuid(), policy, now, "rebuild");
        await store.AppendPolicyAsync(policyId, policyCreated, 0);
        var policyActivated = policyAggregate.Activate(Guid.NewGuid(), 1, 1, now.AddSeconds(1), "rebuild");
        await store.AppendPolicyAsync(policyId, policyActivated, 1);
        var rebuilder = new PortfolioProjectionRebuilder(store, projections.Db);
        var request = new PortfolioProjectionRebuildRequest([portfolioId], [fundId], [policyId]);

        await projections.ResetAsync();
        var firstReport = await rebuilder.RebuildAsync(request);
        var firstHash = await CatalogHashAsync(projections.Db, portfolio, policy, fund, reservation);
        await projections.ResetAsync();
        var secondReport = await rebuilder.RebuildAsync(request);
        var secondHash = await CatalogHashAsync(projections.Db, portfolio, policy, fund, reservation);

        firstReport.Should().BeEquivalentTo(secondReport);
        firstReport.EventCount.Should().Be(9);
        firstHash.Should().Be(secondHash);
    }

    static async Task<string> CatalogHashAsync(PortfolioDbContext db, PortfolioReadModel portfolio, PortfolioFinancialPolicyReadModel policy, FundMandateReadModel fund, FundCompositionReservationResult reservation)
    {
        object?[] catalog =
        [
            await db.GetPortfolioAsync(portfolio.PortfolioId),
            await db.GetPortfoliosByStateAsync(portfolio.OperatingState, PortfolioProjectionHandler.StateBucket(portfolio.PortfolioId), 0, 100),
            await db.GetPolicyAsync(policy.PolicyId, policy.PolicyVersion), await db.GetPoliciesAsync(portfolio.PortfolioId, 100), await db.GetActivePolicyAsync(portfolio.PortfolioId),
            await db.GetFundAsync(fund.FundId), await db.GetFundsByPortfolioAsync(portfolio.PortfolioId, 0, 100),
            await db.GetActiveFundsAsync(portfolio.PortfolioId, fund.TradingYear, fund.DecisionHorizon, fund.EffectiveFromUtc.AddSeconds(1), 100),
            await db.GetAssignmentsAsync(portfolio.PortfolioId, fund.FundId, fund.FundMandateVersion, 100),
            await db.GetCurrentAllocationAsync(portfolio.PortfolioId, fund.FundId), await db.GetCurrentRiskEnvelopeAsync(portfolio.PortfolioId, fund.FundId),
            await db.GetOrdersAsync(portfolio.PortfolioId, fund.FundId, new DateOnly(reservation.Order.CreatedOnUtc.Year, reservation.Order.CreatedOnUtc.Month, 1), DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc), 100),
            await db.GetOrderAsync(reservation.Order.OrderId), await db.GetOrderTradesAsync(reservation.Order.OrderId, 100),
            await db.GetTradeAsync(reservation.Trades[0].TradeId), await db.GetCompositionsAsync(reservation.Order.WorkflowId, 100),
        ];
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(catalog)))).ToLowerInvariant();
    }
}
