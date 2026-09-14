using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Application.Storage.TradePlanDb;
using TomasAI.IFM.Application.Storage.TradePlanDb.Schema;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.TradePlanDb;

public sealed class TradePlanStorageFixture
{
    public TradePlanStorageFixture()
    {
        var settings = new DbConnectionSettings().Add(
            TradeDbContext.TradeDbConnection,
            "Contact Points=localhost;Port=9042;Default Keyspace=trade_test_db",
            "System.Data.ScyllaDb");
        var logger = Substitute.For<ILogger<DbProvider>>();
        var schema = new TradePlanSchemaDb(settings, logger);
        schema.CreateAllAsync().GetAwaiter().GetResult();
        // These two schemas are introduced by this test suite. Recreate them in the
        // disposable test keyspace so clustering changes are exercised rather than
        // hidden by a table left behind by an earlier development build.
        schema.RecreateAsync([
            "position_trade_plan_activity_by_date_v1",
            "position_exit_workflow_v1"
        ]).GetAwaiter().GetResult();
        TradePlanDb = new TradePlanDbContext(settings, logger);
    }

    public TradePlanDbContext TradePlanDb { get; }
}

public sealed class TradePlanStorageTests(TradePlanStorageFixture fixture)
    : IClassFixture<TradePlanStorageFixture>
{
    [Fact]
    public async Task Material_revisions_round_trip_in_descending_history_order_and_are_idempotent()
    {
        var suffix = Random.Shared.Next(10_000, 900_000);
        var first = Plan(suffix, 1, 10m, material: true);
        var second = Plan(suffix, 2, 25m, material: true);

        await fixture.TradePlanDb.ProjectMaterialAsync(first);
        await fixture.TradePlanDb.ProjectMaterialAsync(first);
        await fixture.TradePlanDb.ProjectMaterialAsync(second);

        var current = await fixture.TradePlanDb.GetCurrentAsync(
            first.Position.Id, first.Position.StrategyKind, first.ValueDate);
        var history = await fixture.TradePlanDb.GetHistoryAsync(
            first.Position.Id, first.Position.StrategyKind, first.ValueDate, 10);

        current.Should().BeEquivalentTo(second);
        history.Items.Select(plan => plan.PlanRevision).Should().Equal(2, 1);
    }

    [Fact]
    public async Task Non_material_revision_does_not_replace_the_current_projected_plan()
    {
        var suffix = Random.Shared.Next(900_001, 1_800_000);
        var material = Plan(suffix, 1, 10m, material: true);
        var nonMaterial = Plan(suffix, 2, 11m, material: false);

        await fixture.TradePlanDb.ProjectMaterialAsync(material);
        await fixture.TradePlanDb.ProjectMaterialAsync(nonMaterial);

        var current = await fixture.TradePlanDb.GetCurrentAsync(
            material.Position.Id, material.Position.StrategyKind, material.ValueDate);
        var history = await fixture.TradePlanDb.GetHistoryAsync(
            material.Position.Id, material.Position.StrategyKind, material.ValueDate, 10);

        current!.PlanRevision.Should().Be(1);
        history.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task Same_revision_with_a_different_hash_is_rejected()
    {
        var suffix = Random.Shared.Next(1_800_001, 2_600_000);
        var original = Plan(suffix, 1, 10m, material: true);
        await fixture.TradePlanDb.ProjectMaterialAsync(original);

        var conflicting = original with { ContentHash = new string('F', 64) };
        var action = () => fixture.TradePlanDb.ProjectMaterialAsync(conflicting);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*conflicts with a different content hash*");
    }

    [Fact]
    public async Task Invalid_history_page_size_is_rejected_before_database_access()
    {
        var plan = Plan(Random.Shared.Next(2_600_001, 3_400_000), 1, 10m, material: true);
        var action = () => fixture.TradePlanDb.GetHistoryAsync(
            plan.Position.Id, plan.Position.StrategyKind, plan.ValueDate, 0);

        await action.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task Activity_and_exit_workflow_timeline_round_trip_and_replay_idempotently()
    {
        var suffix = Random.Shared.Next(3_400_001, 4_200_000);
        var plan = Plan(suffix, 1, -1_000m, material: true) with
        {
            State = TradePlanState.ExitRequired,
            Action = TradePlanAction.ExitAtMarket,
            RequiresExit = true
        };
        plan = plan with { ContentHash = TradePlanContractIdentity.PlanHash(plan) };
        await fixture.TradePlanDb.ProjectMaterialAsync(plan);
        await fixture.TradePlanDb.ProjectMaterialAsync(plan);

        var activity = await fixture.TradePlanDb.GetActivityAsync(plan.ValueDate, 500);
        activity.Items.Count(item =>
                item.Position.Id == plan.Position.Id && item.ContentHash == plan.ContentHash)
            .Should().Be(1);

        var workflowId = new ExitPositionWorkflowId(plan.Position.Id, plan.ValueDate,
            TradePlanContractIdentity.DeterministicId($"exit-{suffix}"));
        var started = new ExitPositionWorkflowProjection
        {
            WorkflowId = workflowId,
            StrategyKind = plan.Position.StrategyKind,
            State = ExitPositionWorkflowState.Started,
            StageRevision = 1,
            UpdatedAtUtc = plan.CalculatedAtUtc,
            SourcePlanEventId = TradePlanContractIdentity.DeterministicId($"plan-event-{suffix}"),
            ExitPlan = plan
        };
        var composed = started with
        {
            State = ExitPositionWorkflowState.OrderComposed,
            StageRevision = 2,
            UpdatedAtUtc = started.UpdatedAtUtc.AddTicks(1)
        };
        var accepted = composed with
        {
            State = ExitPositionWorkflowState.RiskAccepted,
            StageRevision = 3,
            UpdatedAtUtc = started.UpdatedAtUtc.AddTicks(2),
            RiskDecision = new PortfolioCloseRiskDecision
            {
                ExecuteTradeOrder = true,
                ReasonCode = "Accepted",
                FinancialRevision = 9
            }
        };

        await fixture.TradePlanDb.ProjectExitWorkflowAsync(started);
        await fixture.TradePlanDb.ProjectExitWorkflowAsync(started);
        await fixture.TradePlanDb.ProjectExitWorkflowAsync(composed);
        await fixture.TradePlanDb.ProjectExitWorkflowAsync(accepted);

        var current = await fixture.TradePlanDb.GetCurrentExitWorkflowAsync(
            plan.Position.Id, plan.ValueDate);
        var timeline = await fixture.TradePlanDb.GetExitWorkflowTimelineAsync(
            plan.Position.Id, plan.ValueDate, 20);

        current.Should().BeEquivalentTo(accepted);
        timeline.Items.Where(item => item.WorkflowId == workflowId)
            .Select(item => item.StageRevision).Should().Equal(3, 2, 1);

        var conflicting = accepted with { State = ExitPositionWorkflowState.NoTradeOrders };
        var conflict = () => fixture.TradePlanDb.ProjectExitWorkflowAsync(conflicting);
        await conflict.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*conflicts with a different payload*");
    }

    static StrategyTradePlanSnapshot Plan(int suffix, long revision, decimal pnl, bool material)
    {
        var now = DateTime.UtcNow;
        var position = new StrategyPositionSnapshot
        {
            Id = new(new TradeEntityId(suffix, suffix + 1, suffix + 2, suffix + 3),
                TradePlanContractIdentity.DeterministicId($"position-{suffix}")),
            StrategyKind = TradeStrategyKind.IronCondor,
            Phase = StrategyPositionPhase.MarkToMarket,
            PositionSequence = revision,
            RouteGeneration = 1,
            Legs = Enumerable.Range(1, 4).Select(index => new StrategyPositionLeg
            {
                TradeLegId = TradePlanContractIdentity.DeterministicId($"leg-{suffix}-{index}"),
                ContractId = $"ES-{suffix}-{index}",
                AssetFamily = TradeAssetFamily.FuturesOption,
                SignedQuantity = index % 2 == 0 ? -1 : 1,
                OpeningPrice = 10m + index,
                CurrentPrice = 10.1m + index,
                LastSourceSequence = revision,
                LastPriceAtUtc = now
            }).ToArray(),
            UnrealizedPnl = pnl,
            AsOfUtc = now,
            IsOpen = true
        };
        var plan = new StrategyTradePlanSnapshot
        {
            Position = position,
            ValueDate = DateOnly.FromDateTime(now),
            PlanRevision = revision,
            CurrentValue = 1m,
            TotalPnl = pnl,
            ForwardTradePrice = 1m,
            ForwardPnl = pnl,
            State = TradePlanState.Normal,
            Action = TradePlanAction.Monitor,
            MaterialChange = material,
            ReasonCode = "PLAN.NORMAL",
            Explanation = "integration test",
            Parameters = new(),
            CalculatedAtUtc = now
        };
        return plan with { ContentHash = TradePlanContractIdentity.PlanHash(plan) };
    }
}
