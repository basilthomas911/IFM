using TomasAI.IFM.Domain.Trade.Shared;
using System;
using System.Threading.Tasks;
using TomasAI.IFM.Application.Storage.TradeDb.Schema;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ViewModels;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.TradeDb;

public sealed class IntrinsicTimeStrategyWorkflowStorageContractTests
{
    [Fact]
    public void WorkflowSchemas_DefineSixUnversionedQueryDrivenTables()
    {
        var schema = TradeSchemaSql.Create;
        string[] tables =
        [
            "intrinsic_time_strategy_workflow",
            "intrinsic_time_strategy_workflow_active",
            "intrinsic_time_strategy_workflow_start_attempt",
            "intrinsic_time_strategy_workflow_timeline",
            "intrinsic_time_strategy_workflow_by_entity",
            "intrinsic_time_strategy_workflow_by_status_day"
        ];
        Assert.All(tables, table => Assert.Contains("CREATE TABLE IF NOT EXISTS trade." + table, schema, StringComparison.Ordinal));
        Assert.DoesNotContain("ALLOW FILTERING", schema, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch(@"intrinsic_time_strategy_workflow[^\s(]*_v\d+", schema);
        Assert.Contains("PRIMARY KEY (workflowEntityId, requestedAtUtc, requestedWorkflowId)", schema, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PRIMARY KEY (workflowId, eventId)", schema, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PRIMARY KEY (workflowEntityId, startedAtUtc, workflowId)", schema, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PRIMARY KEY (status, startedDate, startedAtUtc, workflowId)", schema, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WorkflowQueries_UsePartitionKeysAndBoundedPaging()
    {
        Assert.Contains("workflowEntityId = $1", TradeDbSql.NativeGetIntrinsicTimeStrategyWorkflowStartAttempts, StringComparison.Ordinal);
        Assert.Contains("requestedAtUtc < $2", TradeDbSql.NativeGetIntrinsicTimeStrategyWorkflowStartAttempts, StringComparison.Ordinal);
        Assert.Contains("eventId > $2", TradeDbSql.NativeGetIntrinsicTimeStrategyWorkflowTimeline, StringComparison.Ordinal);
        Assert.Contains("startedAtUtc < $2", TradeDbSql.NativeGetIntrinsicTimeStrategyWorkflowsByEntity, StringComparison.Ordinal);
        Assert.Contains("status = $1 AND startedDate = $2", TradeDbSql.NativeGetIntrinsicTimeStrategyWorkflowsByStatusDay, StringComparison.Ordinal);
        Assert.All(
            new[]
            {
                TradeDbSql.NativeGetIntrinsicTimeStrategyWorkflowStartAttempts,
                TradeDbSql.NativeGetIntrinsicTimeStrategyWorkflowTimeline,
                TradeDbSql.NativeGetIntrinsicTimeStrategyWorkflowsByEntity,
                TradeDbSql.NativeGetIntrinsicTimeStrategyWorkflowsByStatusDay
            },
            static query => Assert.Contains("LIMIT $3", query, StringComparison.Ordinal));
    }
}

public sealed class IntrinsicTimeStrategyWorkflowStorageTests(TradeDbFixture fixture)
    : IClassFixture<TradeDbFixture>
{
    [Fact]
    public async Task WorkflowReadModels_RoundTripAcrossAllSixTables()
    {
        var workflowId = new StrategyWorkflowId(Guid.CreateVersion7());
        var requestedWorkflowId = new StrategyWorkflowId(Guid.CreateVersion7());
        var entityId = $"IntrinsicTimeStrategy.ES.{Guid.NewGuid():N}";
        var startedAtUtc = DateTime.UtcNow.AddMinutes(-1);
        var updatedAtUtc = startedAtUtc.AddSeconds(30);
        var statePayload = new byte[] { 1, 2, 3 };
        var workflow = new IntrinsicTimeStrategyWorkflowReadModel(
            workflowId,
            entityId,
            "IntrinsicTimeStrategy",
            1,
            "ES",
            DateOnly.FromDateTime(startedAtUtc),
            TimeFrameType.Daily,
            Guid.NewGuid(),
            Guid.NewGuid(),
            StrategyWorkflowStatus.Running,
            StrategyWorkflowOutcome.None,
            StrategyWorkflowStage.RegimeDiscovery,
            2,
            101,
            1,
            statePayload,
            string.Empty,
            startedAtUtc,
            null,
            updatedAtUtc);
        var active = new ActiveIntrinsicTimeStrategyWorkflowReadModel(
            entityId,
            workflowId,
            "ES",
            DateOnly.FromDateTime(startedAtUtc),
            TimeFrameType.Daily,
            StrategyWorkflowStage.RegimeDiscovery,
            2,
            101,
            1,
            statePayload,
            startedAtUtc,
            updatedAtUtc);
        var attempt = new IntrinsicTimeStrategyWorkflowStartAttemptReadModel(
            entityId,
            startedAtUtc.AddSeconds(-1),
            requestedWorkflowId,
            StrategyWorkflowStartDecision.Rejected,
            workflowId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            StrategyWorkflowStage.RegimeDiscovery,
            "AlreadyActive",
            100);
        var timeline = new IntrinsicTimeStrategyWorkflowTimelineReadModel(
            workflowId,
            101,
            entityId,
            2,
            StrategyWorkflowStage.RegimeDiscovery,
            "MarketConditionPipelineCompletedEvent",
            1,
            new byte[] { 4, 5, 6 },
            updatedAtUtc);
        var history = new IntrinsicTimeStrategyWorkflowHistoryReadModel(
            entityId,
            startedAtUtc,
            workflowId,
            StrategyWorkflowStatus.Running,
            StrategyWorkflowOutcome.None,
            StrategyWorkflowStage.RegimeDiscovery,
            2,
            null,
            string.Empty);

        await fixture.TradeDb.UpsertIntrinsicTimeStrategyWorkflowAsync(workflow);
        await fixture.TradeDb.UpsertActiveIntrinsicTimeStrategyWorkflowAsync(active);
        await fixture.TradeDb.InsertIntrinsicTimeStrategyWorkflowStartAttemptAsync(attempt);
        await fixture.TradeDb.InsertIntrinsicTimeStrategyWorkflowTimelineAsync(timeline);
        await fixture.TradeDb.UpsertIntrinsicTimeStrategyWorkflowByEntityAsync(history);
        await fixture.TradeDb.UpsertIntrinsicTimeStrategyWorkflowByStatusDayAsync(history);

        var actualWorkflow = await fixture.TradeDb.GetIntrinsicTimeStrategyWorkflowAsync(workflowId);
        var actualActive = await fixture.TradeDb.GetActiveIntrinsicTimeStrategyWorkflowAsync(entityId);
        var actualAttempts = await fixture.TradeDb.GetIntrinsicTimeStrategyWorkflowStartAttemptsAsync(entityId, DateTime.MaxValue, 10);
        var actualTimeline = await fixture.TradeDb.GetIntrinsicTimeStrategyWorkflowTimelineAsync(workflowId, 0, 10);
        var actualEntityHistory = await fixture.TradeDb.GetIntrinsicTimeStrategyWorkflowsByEntityAsync(entityId, DateTime.MaxValue, 10);
        var actualStatusHistory = await fixture.TradeDb.GetIntrinsicTimeStrategyWorkflowsByStatusAsync(
            StrategyWorkflowStatus.Running,
            DateOnly.FromDateTime(startedAtUtc),
            DateOnly.FromDateTime(startedAtUtc),
            100);

        Assert.Equal(workflowId, actualWorkflow?.WorkflowId);
        Assert.Equal(statePayload, actualWorkflow?.StatePayload.ToArray());
        Assert.Equal(workflowId, actualActive?.WorkflowId);
        Assert.Contains(actualAttempts, value => value.RequestedWorkflowId == requestedWorkflowId);
        Assert.Contains(actualTimeline, value => value.EventId == timeline.EventId);
        Assert.Contains(actualEntityHistory, value => value.WorkflowId == workflowId);
        Assert.Contains(actualStatusHistory, value => value.WorkflowId == workflowId);

        await fixture.TradeDb.DeleteActiveIntrinsicTimeStrategyWorkflowAsync(entityId);
        Assert.Null(await fixture.TradeDb.GetActiveIntrinsicTimeStrategyWorkflowAsync(entityId));
    }
}
