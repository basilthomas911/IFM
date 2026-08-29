using FluentAssertions;
using MessagePack;
using Microsoft.AspNetCore.Mvc.Testing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.ViewModels;

namespace TomasAI.IFM.Domain.Trade.VerificationTests.Strategy.IntrinsicTime.RegimeDiscovery;

[Trait("Category", "Verification")]
[Collection(RegimeDiscoveryVerificationCollection.Name)]
public sealed class RegimeDiscoveryWorkflowVerificationTests(WebApplicationFactory<Program> sourceFactory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Trending_up_from_futures_iti_signal_calculates_and_selects_market_condition_for_all_horizons()
    {
        await using var fixture = await RegimeDiscoveryVerificationFixture.StartAsync(sourceFactory);
        var executions = new[]
        {
            RegimeDiscoveryVerificationFixture.Execution(
                RegimeDiscoveryScenarioCatalog.TrendingUp, TimeFrameType.Daily, "D"),
            RegimeDiscoveryVerificationFixture.Execution(
                RegimeDiscoveryScenarioCatalog.TrendingUp, TimeFrameType.Weekly, "W"),
            RegimeDiscoveryVerificationFixture.Execution(
                RegimeDiscoveryScenarioCatalog.TrendingUp, TimeFrameType.Monthly, "M")
        };
        await fixture.PrepareAsync(executions);

        var triggers = await Task.WhenAll(executions.Select(async execution =>
            (execution, trigger: await fixture.PublishAsync(execution.EntityId, execution.Scenario))));
        foreach (var (execution, trigger) in triggers)
        {
            await AssertSuccessfulExecutionAsync(fixture, execution, trigger.Id);
        }

        var daily = executions.Single(value =>
            value.EntityId.ItiSignalEntityId.TimePeriod == TimeFrameType.Daily);
        await fixture.PublishAsync(daily.EntityId, daily.Scenario);
        await Task.Delay(250);
        fixture.Probe.Count(daily.EntityId).Should().Be(1,
            "an active workflow must not redispatch Market Condition for a duplicate trigger");
    }

    [Fact]
    public async Task Extended_completed_regimes_preserve_business_outcome_and_select_market_condition_once()
    {
        await using var fixture = await RegimeDiscoveryVerificationFixture.StartAsync(sourceFactory);
        var scenarios = new[]
        {
            RegimeDiscoveryScenarioCatalog.TrendingDown,
            RegimeDiscoveryScenarioCatalog.RangeBound,
            RegimeDiscoveryScenarioCatalog.BullishBreakout,
            RegimeDiscoveryScenarioCatalog.BearishBreakout,
            RegimeDiscoveryScenarioCatalog.Compressing,
            RegimeDiscoveryScenarioCatalog.ExpandingUp,
            RegimeDiscoveryScenarioCatalog.Transitioning,
            RegimeDiscoveryScenarioCatalog.ExtremeVolatility,
            RegimeDiscoveryScenarioCatalog.DirectionConflict,
            RegimeDiscoveryScenarioCatalog.OptionalEvidenceMissing
        };
        var executions = scenarios.Select(scenario => RegimeDiscoveryVerificationFixture.Execution(
            scenario, TimeFrameType.Daily, scenario.Name)).ToArray();
        await fixture.PrepareAsync(executions);

        foreach (var execution in executions)
        {
            var trigger = await fixture.PublishAsync(execution.EntityId, execution.Scenario);
            await AssertSuccessfulExecutionAsync(fixture, execution, trigger.Id);
        }
    }

    static async Task AssertSuccessfulExecutionAsync(
        RegimeDiscoveryVerificationFixture fixture,
        VerificationExecution execution,
        Guid triggerEventId)
    {
        var command = await fixture.Probe.WaitAsync(
            execution.EntityId, RegimeDiscoveryVerificationFixture.ScenarioTimeout);
        var advanced = await fixture.WaitForRevisionAsync(
            execution.EntityId, 2, StrategyWorkflowStage.MarketCondition);
        var projected = await fixture.Database.TradeDb.GetRegimeDiscoveryAsync(advanced.WorkflowId);
        projected.Should().NotBeNull();
        projected!.Status.Should().Be("Completed");
        projected.InputWorkflowRevision.Should().Be(1,
            "Regime Discovery must execute only from the committed revision-1 Started snapshot");
        projected.ResultPayload.Should().NotBeEmpty();
        projected.ResultPayloadSha256.Should().NotBeNullOrWhiteSpace();

        var result = MessagePackSerializer.Deserialize<RegimeDiscoveryResult>(projected.ResultPayload);
        result.TargetHorizon.Should().Be(execution.EntityId.ItiSignalEntityId.TimePeriod);
        result.WorkflowId.Should().Be(advanced.WorkflowId);
        result.EntityId.Should().Be(execution.EntityId);
        result.TriggerEventId.Should().Be(triggerEventId);
        result.MatchScenario(execution.Scenario, assertRuntimeConfidence: true, assertGoldenScores: false);

        advanced.WorkflowRevision.Should().Be(2);
        advanced.CurrentStage.Should().Be(StrategyWorkflowStage.MarketCondition);
        var state = await fixture.LoadStateAsync(execution.EntityId);
        state.CurrentView.Should().NotBeNull();
        state.CurrentView!.Status.Should().Be(WorkflowStrategyMachineStatus.Started);
        state.CurrentView.WorkflowRevision.Should().Be(2);
        state.CurrentView.CurrentStage.Should().Be(StrategyWorkflowStage.MarketCondition);
        state.CurrentView.RegimeDiscovery.ProcessingStatus.Should().Be(StrategyActorProcessingStatus.Completed);
        state.CurrentView.MarketCondition.ProcessingStatus.Should().Be(StrategyActorProcessingStatus.Processing);
        state.CurrentView.RegimeDiscovery.Result.Should().NotBeNull();
        state.CurrentView.RegimeDiscovery.Result!.Payload.ToArray().Should()
            .Equal(projected.ResultPayload.ToArray());
        state.CurrentView.RegimeDiscovery.Result.PayloadSha256.Should().Be(projected.ResultPayloadSha256);

        var functionState = await fixture.LoadFunctionStateAsync(state.CurrentView);
        functionState.IsCompleted.Should().BeTrue();
        functionState.WorkflowId.Should().Be(advanced.WorkflowId);
        functionState.InputWorkflowRevision.Should().Be(1);
        functionState.CompletedEvent!.Result.Payload.ToArray().Should().Equal(projected.ResultPayload.ToArray());

        var queried = await fixture.QueryWorkflowAsync(advanced.WorkflowId, 2);
        queried.Should().NotBeNull();
        queried!.WorkflowRevision.Should().Be(2);
        queried.CurrentStage.Should().Be(StrategyWorkflowStage.MarketCondition);

        command.WorkflowId.Should().Be(advanced.WorkflowId);
        command.WorkflowEntityId.Should().Be(execution.EntityId);
        command.InputWorkflowRevision.Should().Be(2);
        command.WorkflowView.CurrentStage.Should().Be(StrategyWorkflowStage.MarketCondition);
        command.WorkflowView.WorkflowRevision.Should().Be(2);
        command.WorkflowView.RegimeDiscovery.ProcessingStatus.Should().Be(StrategyActorProcessingStatus.Completed);
        command.WorkflowView.RegimeDiscovery.Result!.PayloadSha256.Should().Be(projected.ResultPayloadSha256);
        command.WorkflowView.RegimeDiscoveryParameterSet.ParameterSetId.Should()
            .Be(result.RegimeDiscoveryParameterSetId);
        command.WorkflowView.RegimeDiscoveryParameterSet.Version.Should()
            .Be(result.RegimeDiscoveryParameterSetVersion);
        fixture.Probe.Count(execution.EntityId).Should().Be(1);
    }
}
