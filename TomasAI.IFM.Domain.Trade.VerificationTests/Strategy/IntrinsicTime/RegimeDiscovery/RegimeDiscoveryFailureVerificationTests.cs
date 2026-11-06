using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Function.State;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Options;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.VerificationTests.Strategy.IntrinsicTime.RegimeDiscovery;

[Trait("Category", "Verification")]
[Collection(RegimeDiscoveryVerificationCollection.Name)]
public sealed class RegimeDiscoveryFailureVerificationTests(TomasAI.IFM.IntegrationTesting.KestrelWebApplicationFactory<TomasAI.IFM.Application.Api.Server.ApiServerEntryPoint> sourceFactory)
    : IClassFixture<TomasAI.IFM.IntegrationTesting.KestrelWebApplicationFactory<TomasAI.IFM.Application.Api.Server.ApiServerEntryPoint>>
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
                    observation.SignalKey.TimeFrame == TimeFrameType.Daily
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
                Enabled = true
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
                Enabled = true
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
    public async Task Concurrent_native_completion_returns_the_original_event_and_rejects_divergent_input()
    {
        var services = sourceFactory.Services;
        var repository = (RegimeDiscoveryFunctionStateRepository)services.GetRequiredService<SimpleInjector.Container>()
            .GetInstance<IEventSourceFunctionStateRepository<RegimeDiscoveryFunctionState, ExecuteRegimeDiscoveryPipelineCommand>>();
        var input = RegimeDiscoveryScenarioDataBuilder.CreateInput(
            RegimeDiscoveryScenarioCatalog.TrendingUp, TimeFrameType.Daily)
            with { WorkflowId = StrategyWorkflowId.New(TimeProvider.System) };
        var entity = RegimeDiscoveryExecutionEntityId.Create(input.EntityId, input.WorkflowId);
        var request = new ExecuteRegimeDiscoveryPipelineCommand
        {
            CommandId = Guid.NewGuid(),
            Subject = new(ActorType.Function, ExecuteRegimeDiscoveryPipelineCommand.Actor,
                ExecuteRegimeDiscoveryPipelineCommand.Verb, entity.Format()),
            EntityId = entity,
            InputWorkflowRevision = 1,
            TriggerEvent = input.TriggerEvent,
            CorrelationId = Guid.NewGuid(),
            CausationId = input.TriggerEventId,
            RequestedAtUtc = input.ProducedAtUtc,
            ExpiresAtUtc = input.ProducedAtUtc.AddMinutes(2),
            ParameterSet = input.ParameterSet,
            ParameterPayloadSha256 = RegimeDiscoveryParameterPayload.ComputeSha256(input.ParameterSet),
            TargetHorizon = TimeFrameType.Daily,
            Snapshot = input.Snapshot
        };
        var result = await new RegimeDiscoveryCalculationModel().CalculateAsync(input);
        var candidate = new RegimeDiscoveryPipelineCompletedEvent
        {
            Subject = new(ActorType.Function, ExecuteRegimeDiscoveryPipelineCommand.Actor,
                RegimeDiscoveryPipelineCompletedEvent.Verb, entity.Format()),
            Id = Guid.NewGuid(),
            EntityId = request.WorkflowEntityId,
            CommandId = request.CommandId,
            AggregateId = entity.Format(),
            EventSource = request.EventSource,
            ReceivedOn = result.ProducedAtUtc,
            WorkflowId = request.WorkflowId,
            InputWorkflowRevision = request.InputWorkflowRevision,
            CorrelationId = request.CorrelationId,
            CausationId = request.CausationId,
            PipelineStage = StrategyWorkflowStage.RegimeDiscovery,
            Result = StrategyStageResultEnvelope.CreateRegime(result),
            CompletedAtUtc = result.ProducedAtUtc,
            ExpiresAtUtc = request.ExpiresAtUtc,
            ParameterPayloadSha256 = request.ParameterPayloadSha256,
            SignalSnapshotId = request.Snapshot.SnapshotId,
            ParameterSet = request.ParameterSet
        };
        var context = Substitute.For<IFunctionActorContext>();

        var returned = await Task.WhenAll(
            repository.CommitAsync(context, request, candidate).AsTask(),
            repository.CommitAsync(context, request, candidate with { Id = Guid.NewGuid() }).AsTask());

        returned[0].EventId.Should().BeGreaterThan(0);
        returned[1].Should().BeEquivalentTo(returned[0]);
        var stored = await repository.LoadStateAsync(request);
        stored.CompletedEvent.Should().BeEquivalentTo(returned[0]);
        var projection = await services.GetRequiredService<IDbContextFactory>().TradeDb
            .GetRegimeDiscoveryAsync(request.WorkflowId);
        projection!.SourceEventId.Should().Be(returned[0].Id);

        var conflict = async () => await repository.CommitAsync(context,
            request with { RequestedAtUtc = request.RequestedAtUtc.AddTicks(1) }, candidate with { Id = Guid.NewGuid() });
        await conflict.Should().ThrowAsync<CommandAuditPayloadConflictException>();
        var source = services.GetRequiredService<IEventSourceActorDbContext>();
        var stream = await source.GetEventStreamIdFromDbAsync(request.StreamId);
        (await source.GetCommittedStreamVersionAsync(stream!.EventStreamId)).Should().Be(1);
    }

    [Fact]
    public async Task Projection_write_failure_rolls_back_and_cannot_advance_workflow()
    {
        await using var fixture = await RegimeDiscoveryVerificationFixture.StartAsync(sourceFactory);
        var execution = RegimeDiscoveryVerificationFixture.Execution(
            RegimeDiscoveryScenarioCatalog.TrendingUp, TimeFrameType.Daily, "PROJECTOR");
        await fixture.PrepareAsync([execution]);

        var transactions = fixture.Services.GetRequiredService<IPostgresEventTransaction>();
        await transactions.ExecuteAsync((db, token) => db.ExecuteAsync("""
            ALTER TABLE trade.regime_discovery ADD CONSTRAINT verification_reject_regime_projection
            CHECK (position('-PROJECTOR-' in workflowEntityId) = 0) NOT VALID;
            """, [], token));
        try
        {
            await fixture.PublishAsync(execution.EntityId);
            var terminal = await fixture.WaitForTerminalAsync(
                execution.EntityId, StrategyWorkflowOutcome.PipelineFailed);

            (await fixture.Database.TradeDb.GetRegimeDiscoveryAsync(terminal.WorkflowId)).Should().BeNull();
            var invocationCount = await transactions.ExecuteAsync((db, token) => db.ScalarAsync(
                "SELECT count(*) FROM trade.regime_discovery_invocation WHERE workflow_id=$1;",
                [terminal.WorkflowId.Value], token));
            invocationCount.Should().Be(0L);
            fixture.Probe.Count(execution.EntityId).Should().Be(0);
        }
        finally
        {
            await transactions.ExecuteAsync((db, token) => db.ExecuteAsync(
                "ALTER TABLE trade.regime_discovery DROP CONSTRAINT verification_reject_regime_projection;",
                [], token));
        }
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
}
