using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Options;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.VerificationTests.Strategy.IntrinsicTime.RegimeDiscovery;

[Trait("Category", "Verification")]
[Collection(RegimeDiscoveryVerificationCollection.Name)]
public sealed class RegimeDiscoveryFailureVerificationTests(WebApplicationFactory<Program> sourceFactory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public void Incomplete_specialist_makes_fusion_explicitly_incomplete()
    {
        var fusion = new MarketRegimeFusionModel().Calculate(
            new TrendRegimeResult { IsComplete = false },
            new VolatilityRegimeResult { IsComplete = true },
            new MarketStructureRegimeResult { IsComplete = true },
            new MarketRegimeFusionConfiguration());

        fusion.IsComplete.Should().BeFalse();
        fusion.Reasons.Should().ContainSingle(reason =>
            reason.Code == RegimeDiscoveryReasonCodes.FusionFailed &&
            reason.Severity == RegimeReasonSeverity.Failure);
    }

    [Theory]
    [InlineData(RegimeDiscoverySignalAvailability.Missing)]
    [InlineData(RegimeDiscoverySignalAvailability.Stale)]
    [InlineData(RegimeDiscoverySignalAvailability.NotWarm)]
    [InlineData(RegimeDiscoverySignalAvailability.Invalid)]
    [InlineData(RegimeDiscoverySignalAvailability.SchemaUnsupported)]
    [InlineData(RegimeDiscoverySignalAvailability.CalculationVersionMismatch)]
    public async Task Required_signal_quality_failure_makes_specialists_and_fusion_incomplete(
        RegimeDiscoverySignalAvailability availability)
    {
        var input = RegimeDiscoveryScenarioDataBuilder.CreateInput(
            RegimeDiscoveryScenarioCatalog.TrendingUp,
            TimeFrameType.Daily);
        input = input with
        {
            Snapshot = input.Snapshot with
            {
                Observations = input.Snapshot.Observations.Select(observation =>
                    observation.Metric == RegimeDiscoverySignalMetric.Ema200 &&
                    observation.SignalKey.TimeFrame == TimeFrameType.FifteenMinutes
                        ? observation with
                        {
                            Availability = availability,
                            IsWarm = availability != RegimeDiscoverySignalAvailability.NotWarm,
                            IsValid = availability != RegimeDiscoverySignalAvailability.Invalid
                        }
                        : observation).ToArray()
            }
        };

        var result = await new RegimeDiscoveryCalculationModel().CalculateAsync(input);

        result.Trend.IsComplete.Should().BeFalse();
        result.Decision.IsComplete.Should().BeFalse();
        result.Reasons.Should().Contain(reason =>
            reason.Code == RegimeDiscoveryReasonCodes.RequiredDataMissing &&
            reason.Severity == RegimeReasonSeverity.Failure);
    }

    [Fact]
    public async Task Missing_required_signal_fails_workflow_without_projection_or_market_condition()
    {
        await using var fixture = await RegimeDiscoveryVerificationFixture.StartAsync(sourceFactory, services =>
            services.AddSingleton(new IntrinsicTimeStrategyWorkflowOptions
            {
                Enabled = true,
                RequireWarmRegimeDiscoverySignals = false
            }));
        var scenario = RegimeDiscoveryScenarioCatalog.TrendingUp with
        {
            Name = "MissingRequiredEma200",
            OmittedMetrics = new HashSet<RegimeDiscoverySignalMetric>
            {
                RegimeDiscoverySignalMetric.Ema200
            }
        };
        var execution = RegimeDiscoveryVerificationFixture.Execution(
            scenario, TimeFrameType.Daily, "MISSING");
        await fixture.PrepareAsync([execution]);

        await fixture.PublishAsync(execution.EntityId);
        var terminal = await fixture.WaitForTerminalAsync(
            execution.EntityId, StrategyWorkflowOutcome.PipelineFailed);

        (await fixture.Database.TradeDb.GetRegimeDiscoveryAsync(terminal.WorkflowId)).Should().BeNull();
        fixture.Probe.Count(execution.EntityId).Should().Be(0);
        var state = await fixture.LoadStateAsync(execution.EntityId);
        state.CurrentView!.Status.Should().Be(WorkflowStrategyMachineStatus.Failed);
        state.CurrentView.RegimeDiscovery.ProcessingStatus.Should().Be(StrategyActorProcessingStatus.Failed);
    }

    [Fact]
    public async Task Fixed_timeout_fences_late_worker_and_never_selects_market_condition()
    {
        await using var fixture = await RegimeDiscoveryVerificationFixture.StartAsync(sourceFactory, services =>
        {
            services.AddSingleton(new IntrinsicTimeStrategyWorkflowOptions
            {
                Enabled = true,
                RequireWarmRegimeDiscoverySignals = false
            });
            services.RemoveAll<RegimeDiscoveryExecutionOptions>();
            services.AddSingleton(new RegimeDiscoveryExecutionOptions
            {
                MaximumExecutionDuration = TimeSpan.FromSeconds(1)
            });
            services.RemoveAll<IRegimeDiscoveryMarketSignalSnapshotProvider>();
            services.AddSingleton<IRegimeDiscoveryMarketSignalSnapshotProvider>(new BlockingSnapshotProvider());
        });
        var execution = RegimeDiscoveryVerificationFixture.Execution(
            RegimeDiscoveryScenarioCatalog.TrendingUp, TimeFrameType.Daily, "TIMEOUT");
        await fixture.PrepareAsync([execution]);

        await fixture.PublishAsync(execution.EntityId);
        var terminal = await fixture.WaitForTerminalAsync(
            execution.EntityId, StrategyWorkflowOutcome.TimedOut);
        await Task.Delay(250);

        (await fixture.Database.TradeDb.GetRegimeDiscoveryAsync(terminal.WorkflowId)).Should().BeNull();
        fixture.Probe.Count(execution.EntityId).Should().Be(0);
    }

    [Fact]
    public async Task Projector_exception_is_translated_to_failure_and_cannot_advance_workflow()
    {
        await using var fixture = await RegimeDiscoveryVerificationFixture.StartAsync(sourceFactory, services =>
        {
            services.AddSingleton(new IntrinsicTimeStrategyWorkflowOptions
            {
                Enabled = true,
                RequireWarmRegimeDiscoverySignals = false
            });
            services.RemoveAll<IFunctionProjector<RegimeDiscoveryPipelineCompletedEvent>>();
            services.AddSingleton<IFunctionProjector<RegimeDiscoveryPipelineCompletedEvent>, ThrowingProjector>();
        });
        var execution = RegimeDiscoveryVerificationFixture.Execution(
            RegimeDiscoveryScenarioCatalog.TrendingUp, TimeFrameType.Daily, "PROJECTOR");
        await fixture.PrepareAsync([execution]);

        await fixture.PublishAsync(execution.EntityId);
        var terminal = await fixture.WaitForTerminalAsync(
            execution.EntityId, StrategyWorkflowOutcome.PipelineFailed);

        (await fixture.Database.TradeDb.GetRegimeDiscoveryAsync(terminal.WorkflowId)).Should().BeNull();
        fixture.Probe.Count(execution.EntityId).Should().Be(0);
    }

    sealed class BlockingSnapshotProvider : IRegimeDiscoveryMarketSignalSnapshotProvider
    {
        public async ValueTask<RegimeDiscoveryMarketSignalSnapshotResult> CaptureAsync(
            RegimeDiscoveryMarketSignalSnapshotRequest request,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("The timeout owner should cancel this worker.");
        }
    }

    sealed class ThrowingProjector : IFunctionProjector<RegimeDiscoveryPipelineCompletedEvent>
    {
        public ValueTask ProjectAsync(
            RegimeDiscoveryPipelineCompletedEvent completedEvent,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromException(new InvalidOperationException("Injected verification projector failure."));
    }
}
