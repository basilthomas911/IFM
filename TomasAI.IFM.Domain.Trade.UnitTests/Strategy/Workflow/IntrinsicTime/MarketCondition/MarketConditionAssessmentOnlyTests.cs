using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Function.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Function.State;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.MarketCondition;

public sealed class MarketConditionAssessmentOnlyTests
{
    [Theory]
    [InlineData(TimeFrameType.Daily)]
    [InlineData(TimeFrameType.Weekly)]
    [InlineData(TimeFrameType.Monthly)]
    public async Task Workflow_start_requires_matching_market_profile_and_frozen_selection_authority(TimeFrameType horizon)
    {
        var selection = await TradeSelection.TradeSelectionFixture.Command(horizon:horizon);
        var assessment = AssessmentFixture.Command(horizon);
        var frozen=selection.SelectionBinding.PortfolioSnapshot with {WorkflowId=assessment.WorkflowId.Value,CorrelationId=assessment.CorrelationId};
        frozen=frozen with {PayloadSha256=TomasAI.IFM.Domain.Portfolio.Workflow.PortfolioCanonicalHash.Compute(frozen with {PayloadSha256=""})};
        var binding=Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection.TradeSelectionContracts.Seal(selection.SelectionBinding with {PortfolioSnapshot=frozen});
        var view = assessment.WorkflowView;
        var start = new ExecuteIntrinsicTimeStrategyWorkflowCommand
        {
            CommandId = Guid.NewGuid(), EntityId = view.EntityId,
            Subject = new(ActorType.Command, ExecuteIntrinsicTimeStrategyWorkflowCommand.Actor,
                ExecuteIntrinsicTimeStrategyWorkflowCommand.Verb, view.EntityId.Format()),
            FundId = 1, TriggerEvent = assessment.TriggerEvent,ProposedWorkflowId=assessment.WorkflowId,RequestedAtUtc=binding.FrozenAtUtc,SelectionBinding=binding,
            RegimeDiscoveryParameterSet = view.RegimeDiscoveryParameterSet,
            RegimeDiscoveryParameterPayloadSha256 = view.RegimeDiscoveryParameterPayloadSha256,
            AssessmentBinding = view.AssessmentBinding
        };
        void Validate(ExecuteIntrinsicTimeStrategyWorkflowCommand command) => typeof(IntrinsicTimeStrategyWorkflowCommandActor)
            .GetMethod("ValidateCommand", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [command]);
        Action valid = () => Validate(start);
        valid.Should().NotThrow("no legacy fund/option/broker parameter set is required");
        Action missing = () => Validate(start with { AssessmentBinding = null });
        missing.Should().Throw<TargetInvocationException>().WithInnerException<ArgumentException>();
        var wrong = assessment.ParameterSet with { HorizonProfile = assessment.ParameterSet.HorizonProfile with { RegimeProfileId = Guid.NewGuid() } };
        Action mismatch = () => Validate(start with { AssessmentBinding = new() { Parameters = wrong, PayloadSha256 = MarketConditionAssessmentHash.Parameters(wrong) } });
        mismatch.Should().Throw<TargetInvocationException>().WithInnerException<ArgumentException>();
    }

    [Theory]
    [InlineData("Assess", true)]
    [InlineData("Execute", false)]
    [InlineData("Unknown", false)]
    public async Task Function_mailbox_accepts_only_assessment_and_releases_every_payload(string verb, bool expected)
    {
        var command = AssessmentFixture.Command();
        var context = Substitute.For<IMarketConditionFunctionContext>();
        context.CalculationModel.Returns(new MarketConditionAssessmentCalculator());
        var provider = Substitute.For<IMarketConditionAssessmentSnapshotProvider>();
        var repository = Substitute.For<IEventSourceFunctionStateRepository<MarketConditionAssessmentState, ExecuteMarketConditionAssessmentCommand>>();
        var projector = Substitute.For<IFunctionProjector<MarketConditionAssessmentCompletedEvent>>();
        provider.CaptureAsync(Arg.Any<MarketConditionAssessmentParameterSet>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(MarketConditionAssessmentCalculationTests.Snapshot(command).Seal());
        repository.LoadStateAsync(command, Arg.Any<CancellationToken>()).Returns(new MarketConditionAssessmentState());
        context.ActorId.Returns(new ActorMailboxId(ActorType.Function, MarketConditionFunctionActor.ActorName));
        context.StateRepository.Returns(repository); context.FunctionProjector.Returns(projector);
        context.SnapshotProvider.Returns(provider); context.TimeProvider.Returns(new Clock(command.RequestedAtUtc));
        context.Logger.Returns(Substitute.For<ILogger<MarketConditionFunctionActor>>());
        var message = Substitute.For<IActorMessage>();
        message.Subject.Returns(new ActorSubject(ActorType.Function, ExecuteMarketConditionAssessmentCommand.Actor, verb, command.EntityId.Format()));
        message.AsCommand<ExecuteMarketConditionAssessmentCommand>().Returns(command);
        ServiceResult<FunctionResult<MarketConditionAssessmentCompletedEvent, MarketConditionAssessmentFailedEvent>>? reply = null;
        message.ReplyAsync(Arg.Do<ServiceResult<FunctionResult<MarketConditionAssessmentCompletedEvent, MarketConditionAssessmentFailedEvent>>>(x => reply = x))
            .Returns(ValueTask.CompletedTask);
        await new MarketConditionFunctionActor(context).HandleMessageAsync(message);
        message.Received(1).ReleasePayload();
        reply.Should().NotBeNull();
        reply!.Value!.IsCompleted.Should().Be(expected);
        if (!expected)
        {
            reply.Value.Failed!.FailureCategory.Should().Be(MarketConditionFailureCategory.ContractInvalid);
            provider.ReceivedCalls().Should().BeEmpty();
            repository.ReceivedCalls().Should().BeEmpty();
            projector.ReceivedCalls().Should().BeEmpty();
            message.DidNotReceive().AsCommand<ExecuteMarketConditionAssessmentCommand>();
        }
        else
        {
            await projector.Received(1).ProjectAsync(Arg.Any<MarketConditionAssessmentCompletedEvent>(), Arg.Any<CancellationToken>());
            await repository.Received(1).SaveCompletedStateAsync(context, Arg.Any<MarketConditionAssessmentState>(), command, Arg.Any<CancellationToken>());
        }
    }

    [Theory]
    [InlineData("actor-type")]
    [InlineData("actor-name")]
    [InlineData("entity")]
    [InlineData("null-payload")]
    [InlineData("malformed-payload")]
    [InlineData("invalid-command")]
    public async Task Invalid_ingress_is_rejected_before_state_or_market_access(string scenario)
    {
        var command=AssessmentFixture.Command();
        if(scenario=="invalid-command") command=command with { CommandId=Guid.Empty };
        var context=Substitute.For<IMarketConditionFunctionContext>();
        context.ActorId.Returns(new ActorMailboxId(ActorType.Function,MarketConditionFunctionActor.ActorName));
        context.Logger.Returns(Substitute.For<ILogger<MarketConditionFunctionActor>>());
        context.TimeProvider.Returns(new Clock(command.RequestedAtUtc));
        var repository=Substitute.For<IEventSourceFunctionStateRepository<MarketConditionAssessmentState,ExecuteMarketConditionAssessmentCommand>>();
        var provider=Substitute.For<IMarketConditionAssessmentSnapshotProvider>();
        var projector=Substitute.For<IFunctionProjector<MarketConditionAssessmentCompletedEvent>>();
        context.StateRepository.Returns(repository); context.SnapshotProvider.Returns(provider); context.FunctionProjector.Returns(projector);
        var message=Substitute.For<IActorMessage>();
        message.Subject.Returns(new ActorSubject(scenario=="actor-type"?ActorType.Command:ActorType.Function,
            scenario=="actor-name"?"WrongFunction":MarketConditionFunctionActor.ActorName,
            ExecuteMarketConditionAssessmentCommand.Verb, scenario=="entity"?"wrong-entity":command.EntityId.Format()));
        if(scenario=="malformed-payload") message.AsCommand<ExecuteMarketConditionAssessmentCommand>().Returns(_=>throw new InvalidOperationException("bad payload"));
        else message.AsCommand<ExecuteMarketConditionAssessmentCommand>().Returns(scenario=="null-payload"?null:command);
        ServiceResult<FunctionResult<MarketConditionAssessmentCompletedEvent,MarketConditionAssessmentFailedEvent>>? reply=null;
        message.ReplyAsync(Arg.Do<ServiceResult<FunctionResult<MarketConditionAssessmentCompletedEvent,MarketConditionAssessmentFailedEvent>>>(value=>reply=value))
            .Returns(ValueTask.CompletedTask);
        await new MarketConditionFunctionActor(context).HandleMessageAsync(message);
        reply!.Value!.Failed!.FailureCategory.Should().Be(MarketConditionFailureCategory.ContractInvalid);
        message.Received(1).ReleasePayload();
        repository.ReceivedCalls().Should().BeEmpty(); provider.ReceivedCalls().Should().BeEmpty(); projector.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public void Different_workflow_funds_do_not_change_the_market_assessment()
    {
        var command = AssessmentFixture.Command();
        var snapshot = MarketConditionAssessmentCalculationTests.Snapshot(command);
        var first = MarketConditionAssessmentCalculationTests.Calculate(command, snapshot);
        var other = command with { WorkflowView = command.WorkflowView with { FundId = 9876 } };
        var second = MarketConditionAssessmentCalculationTests.Calculate(other, snapshot);
        second.Assessment.Should().BeEquivalentTo(first.Assessment);
        second.MarketProfileId.Should().Be(first.MarketProfileId);
    }

    [Fact]
    public async Task Function_host_starts_and_stops_its_producer_once()
    {
        var context = Substitute.For<IMarketConditionFunctionContext>();
        context.CalculationModel.Returns(new MarketConditionAssessmentCalculator());
        var id = new ActorMailboxId(ActorType.Function, MarketConditionFunctionActor.ActorName);
        context.ActorId.Returns(id);
        var supervisor = Substitute.For<IActorSupervisor>();
        var producer = Substitute.For<IActorProducer>();
        supervisor.GetProducer(id).Returns(producer);
        supervisor.CreateMailbox(id).Returns(Substitute.For<IActorMailbox>());
        var actor = new MarketConditionFunctionActor(context);
        await actor.StartAsync(supervisor); await actor.StartAsync(supervisor);
        actor.IsRunning.Should().BeTrue(); actor.Id.Should().Be(id);
        await producer.Received(1).StartAsync(id, Arg.Any<CancellationToken>());
        await actor.StopAsync(); await actor.StopAsync();
        actor.IsRunning.Should().BeFalse();
        await producer.Received(1).StopAsync(Arg.Any<CancellationToken>());
    }

    sealed class Clock(DateTime at) : TimeProvider { public override DateTimeOffset GetUtcNow() => new(at); }
}
