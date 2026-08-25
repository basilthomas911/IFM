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
        var schemas = new[]
        {
            TradeSchemaCql.CreateIntrinsicTimeStrategyWorkflowTable,
            TradeSchemaCql.CreateIntrinsicTimeStrategyWorkflowActiveByEntityTable,
            TradeSchemaCql.CreateIntrinsicTimeStrategyWorkflowStartAttemptByEntityTable,
            TradeSchemaCql.CreateIntrinsicTimeStrategyWorkflowTimelineTable,
            TradeSchemaCql.CreateIntrinsicTimeStrategyWorkflowByEntityTable,
            TradeSchemaCql.CreateIntrinsicTimeStrategyWorkflowByStatusDayTable
        };

        Assert.All(schemas, static schema =>
        {
            Assert.DoesNotContain("ALLOW FILTERING", schema, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotMatch(@"intrinsic_time_strategy_workflow[^\s(]*_v\d+", schema);
        });
        Assert.Contains("workflowId uuid PRIMARY KEY", schemas[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("workflowEntityId text PRIMARY KEY", schemas[1], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PRIMARY KEY (workflowEntityId, requestedAtUtc, requestedWorkflowId)", schemas[2], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PRIMARY KEY (workflowId, eventId)", schemas[3], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PRIMARY KEY (workflowEntityId, startedAtUtc, workflowId)", schemas[4], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PRIMARY KEY ((status, startedDate), startedAtUtc, workflowId)", schemas[5], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WorkflowQueries_UsePartitionKeysAndBoundedPaging()
    {
        Assert.Contains("workflowEntityId = :workflowEntityId", TradeDbCql.GetIntrinsicTimeStrategyWorkflowStartAttempts, StringComparison.Ordinal);
        Assert.Contains("requestedAtUtc < :beforeUtc", TradeDbCql.GetIntrinsicTimeStrategyWorkflowStartAttempts, StringComparison.Ordinal);
        Assert.Contains("eventId > :afterEventId", TradeDbCql.GetIntrinsicTimeStrategyWorkflowTimeline, StringComparison.Ordinal);
        Assert.Contains("startedAtUtc < :beforeUtc", TradeDbCql.GetIntrinsicTimeStrategyWorkflowsByEntity, StringComparison.Ordinal);
        Assert.Contains("status = :status AND startedDate = :startedDate", TradeDbCql.GetIntrinsicTimeStrategyWorkflowsByStatusDay, StringComparison.Ordinal);
        Assert.All(
            new[]
            {
                TradeDbCql.GetIntrinsicTimeStrategyWorkflowStartAttempts,
                TradeDbCql.GetIntrinsicTimeStrategyWorkflowTimeline,
                TradeDbCql.GetIntrinsicTimeStrategyWorkflowsByEntity,
                TradeDbCql.GetIntrinsicTimeStrategyWorkflowsByStatusDay
            },
            static query => Assert.Contains("LIMIT :pageSize", query, StringComparison.Ordinal));
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
            "RegimeDiscoveryPipelineProcessingEvent",
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
