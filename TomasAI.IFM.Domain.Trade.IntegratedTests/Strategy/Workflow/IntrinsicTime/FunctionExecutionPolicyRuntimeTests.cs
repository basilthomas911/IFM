using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TomasAI.IFM.Application.Storage.TradeDb.Schema;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Function.State;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Function.State;
using TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.MarketCondition;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.IntegratedTests.Strategy.Workflow.IntrinsicTime;

public sealed partial class TradeSelectionRuntimeTests
{
    [Fact]
    public async Task Mapped_assessment_policy_commits_and_replays_over_NATS_without_recapturing()
    {
        var source = new PolicyAssessmentSource();
        await using var factory = Host(services =>
        {
            services.RemoveAll<IMarketConditionAssessmentSnapshotProvider>();
            services.AddSingleton<IMarketConditionAssessmentSnapshotProvider>(source);
            var container = (SimpleInjector.Container)services.Single(x => x.ServiceType == typeof(SimpleInjector.Container)).ImplementationInstance!;
            container.RegisterInstance<IMarketConditionAssessmentSnapshotProvider>(source);
        });
        _ = factory.CreateClient();
        var supervisor = factory.Services.GetRequiredService<IActorSupervisor>();
        var producer = factory.Services.GetRequiredService<IActorProducer>();
        await producer.StartAsync(new(ActorType.Realtime, "AssessmentPolicyVerification"));
        try
        {
            await factory.Services.GetRequiredService<TradeSchemaDb>().CreateAllAsync();
            var command = AssessmentFixture.Command(atUtc: DateTime.UtcNow);
            var first = await producer.RequestFunctionAsync<ExecuteMarketConditionAssessmentCommand, MarketConditionAssessmentExecutionId,
                FunctionResult<MarketConditionAssessmentCompletedEvent, MarketConditionAssessmentFailedEvent>>(command.Subject, command, command.EntityId);
            first.Success.Should().BeTrue(first.ErrorMessage);
            var completed = first.Value!.Completed!;
            completed.Result.AssessmentResult.Should().NotBeNull();
            var state = await factory.Services.GetRequiredService<SimpleInjector.Container>()
                .GetInstance<IEventSourceFunctionStateRepository<MarketConditionAssessmentState, ExecuteMarketConditionAssessmentCommand>>()
                .LoadStateAsync(command);
            state.IsCompleted.Should().BeTrue();
            var replay = await producer.RequestFunctionAsync<ExecuteMarketConditionAssessmentCommand, MarketConditionAssessmentExecutionId,
                FunctionResult<MarketConditionAssessmentCompletedEvent, MarketConditionAssessmentFailedEvent>>(command.Subject, command, command.EntityId);
            replay.Success.Should().BeTrue(replay.ErrorMessage);
            replay.Value!.Completed!.Id.Should().Be(completed.Id);
            replay.Value.Completed.Result.PayloadSha256.Should().Be(completed.Result.PayloadSha256);
            source.Calls.Should().Be(1);
        }
        finally { await supervisor.ShutdownAsync(); await producer.StopAsync(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Mapped_expired_policy_returns_timeout_over_NATS_without_completed_state(bool regime)
    {
        await using var factory = Host();
        _ = factory.CreateClient();
        var supervisor = factory.Services.GetRequiredService<IActorSupervisor>();
        var producer = factory.Services.GetRequiredService<IActorProducer>();
        await producer.StartAsync(new(ActorType.Realtime, "ExpiredPolicyVerification"));
        try
        {
            var assessment = AssessmentFixture.Command(atUtc: DateTime.UtcNow.AddMinutes(-2));
            var container = factory.Services.GetRequiredService<SimpleInjector.Container>();
            if (regime)
            {
                var workflowId = new StrategyWorkflowId(Guid.CreateVersion7());
                var entity = RegimeDiscoveryExecutionEntityId.Create(assessment.WorkflowEntityId, workflowId);
                var command = new ExecuteRegimeDiscoveryPipelineCommand
                {
                    CommandId = Guid.NewGuid(), EntityId = entity,
                    Subject = new(ActorType.Function, ExecuteRegimeDiscoveryPipelineCommand.Actor, ExecuteRegimeDiscoveryPipelineCommand.Verb, entity.Format()),
                    InputWorkflowRevision = 1,
                    WorkflowView = assessment.WorkflowView with { WorkflowId = workflowId, CurrentStage = StrategyWorkflowStage.RegimeDiscovery, WorkflowRevision = 1 },
                    TriggerEvent = assessment.TriggerEvent, CorrelationId = assessment.CorrelationId, CausationId = assessment.CausationId,
                    RequestedAtUtc = assessment.RequestedAtUtc, ExpiresAtUtc = assessment.ExpiresAtUtc,
                    ParameterSet = assessment.WorkflowView.RegimeDiscoveryParameterSet!,
                    ParameterPayloadSha256 = assessment.WorkflowView.RegimeDiscoveryParameterPayloadSha256,
                    TargetHorizon = assessment.TargetHorizon
                };
                var reply = await producer.RequestFunctionAsync<ExecuteRegimeDiscoveryPipelineCommand, RegimeDiscoveryExecutionEntityId,
                    FunctionResult<RegimeDiscoveryPipelineCompletedEvent, RegimeDiscoveryPipelineFailedEvent>>(command.Subject, command, entity);
                reply.Success.Should().BeFalse();
                reply.Value!.Failed!.ErrorCode.Should().Be(23103, reply.ErrorMessage);
                (await container.GetInstance<IEventSourceFunctionStateRepository<RegimeDiscoveryFunctionState, ExecuteRegimeDiscoveryPipelineCommand>>()
                    .LoadStateAsync(command)).IsCompleted.Should().BeFalse();
            }
            else
            {
                var reply = await producer.RequestFunctionAsync<ExecuteMarketConditionAssessmentCommand, MarketConditionAssessmentExecutionId,
                    FunctionResult<MarketConditionAssessmentCompletedEvent, MarketConditionAssessmentFailedEvent>>(assessment.Subject, assessment, assessment.EntityId);
                reply.Success.Should().BeFalse();
                reply.Value!.Failed!.FailureCategory.Should().Be(MarketConditionFailureCategory.Timeout, reply.ErrorMessage);
                (await container.GetInstance<IEventSourceFunctionStateRepository<MarketConditionAssessmentState, ExecuteMarketConditionAssessmentCommand>>()
                    .LoadStateAsync(assessment)).IsCompleted.Should().BeFalse();
            }
        }
        finally { await supervisor.ShutdownAsync(); await producer.StopAsync(); }
    }

    sealed class PolicyAssessmentSource : IMarketConditionAssessmentSnapshotProvider
    {
        public int Calls { get; private set; }
        public ValueTask<MarketConditionAssessmentSnapshot> CaptureAsync(MarketConditionAssessmentParameterSet p, DateTime at, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            return ValueTask.FromResult(new MarketConditionAssessmentSnapshot
            {
                SnapshotId = Guid.NewGuid(), MarketProfileId = p.MarketProfileId, InstrumentRoot = p.InstrumentRoot,
                TargetHorizon = p.TargetHorizon, ReferenceInstrumentId = "ES-Policy", EvaluatedAtUtc = at,
                Quote = new(5000, 5000.25m, 10, 10), SessionState = MarketSessionStatus.Open, EventContext = AssessmentEventContext.Clear,
                Observations = p.Sources.Select(x => new AssessmentObservation { SourceId = x.SourceId, ObservedAtUtc = at,
                    ReceivedAtUtc = at, Sequence = 10, Value = 0m, Availability = MarketSourceAvailability.Available,
                    Validity = MarketSourceValidity.Valid }).ToArray(),
                CalendarEvidence = new() { CheckedAtUtc = at, CoverageConfirmed = true, ValidUntilUtc = at.AddHours(1), Reason = "Controlled policy fixture" }
            }.Seal());
        }
    }
}
