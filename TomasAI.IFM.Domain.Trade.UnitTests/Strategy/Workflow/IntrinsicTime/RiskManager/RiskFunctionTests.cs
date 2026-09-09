using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Function.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Function.State;
using TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.OrderComposer;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.RiskManager;

[Trait("Category","PortfolioFinancial")]
public sealed class RiskFunctionTests
{
    [Fact]
    public async Task Typed_request_and_result_roundtrip_and_preserve_financial_evidence_hashes()
    {
        var command = await Command();
        new List<ValidationError>().ValidateRiskFields(command).Should().BeEmpty();
        var restored = MessagePackBinarySerializer.Shared.Deserialize<ExecuteRiskManagementPipelineCommand>(MessagePackBinarySerializer.Shared.Serialize(command))!;
        restored.Fingerprint().Should().Be(command.InputSha256);
        new List<ValidationError>().ValidateRiskFields(restored).Should().BeEmpty();
        var result = new RiskEvaluator().Calculate(restored);
        result.Outcome.Should().Be(RiskAssessmentOutcome.Approved);
        var clone = MessagePackBinarySerializer.Shared.Deserialize<RiskAssessmentResult>(MessagePackBinarySerializer.Shared.Serialize(result))!;
        RiskContracts.Hash(clone).Should().Be(RiskContracts.Hash(result));
        clone.ToCapacityAssessment().Requirements.ContentHash.Should().Be(FinancialCanonicalHash.Requirements(result.Requirements!));
        var envelope=StrategyStageResultEnvelope.CreateRisk(clone);
        var wire=MessagePackBinarySerializer.Shared.Deserialize<StrategyStageResultEnvelope>(MessagePackBinarySerializer.Shared.Serialize(envelope))!;
        wire.HasContent.Should().BeTrue(); wire.Payload.IsEmpty.Should().BeTrue(); wire.HasValidPayloadSha256().Should().BeTrue();
        wire.ReadRiskResult().SizedOrderHash.Should().Be(result.SizedOrderHash);
        var owned=wire.RiskResult!.Requirements!; owned.Exposures[0]=owned.Exposures[0] with { Amount=999 };
        wire.RiskResult.Requirements!.ContentHash.Should().Be(FinancialCanonicalHash.Requirements(wire.RiskResult.Requirements));
        wire.HasValidPayloadSha256().Should().BeTrue();
    }

    [Fact]
    public async Task Function_maps_complete_persist_replay_and_conflicting_duplicate()
    {
        var command = await Command();
        var context = Substitute.For<IRiskManagementFunctionContext>();
        var repository = Substitute.For<IEventSourceFunctionStateRepository<RiskManagementFunctionState,ExecuteRiskManagementPipelineCommand>>();
        RiskManagementFunctionCompletedEvent? saved = null;
        repository.LoadStateAsync(Arg.Any<ExecuteRiskManagementPipelineCommand>(),Arg.Any<CancellationToken>()).Returns(_ =>
        {
            var state = new RiskManagementFunctionState();
            if(saved is not null) state.TryComplete(saved,command);
            return ValueTask.FromResult(state);
        });
        repository.SaveCompletedStateAsync(Arg.Any<IFunctionActorContext>(),Arg.Any<RiskManagementFunctionState>(),
            Arg.Any<ExecuteRiskManagementPipelineCommand>(),Arg.Any<CancellationToken>()).Returns(call =>
            { saved=call.Arg<RiskManagementFunctionState>().CompletedEvent; return ValueTask.CompletedTask; });
        context.ActorId.Returns(new ActorMailboxId(ActorType.Function,RiskManagementFunctionActor.ActorName));
        context.StateRepository.Returns(repository); context.TimeProvider.Returns(new CompositionTestClock(command.EvaluatedAtUtc));
        context.Logger.Returns(NullLogger<RiskManagementFunctionActor>.Instance); context.CalculationModel.Returns(new RiskEvaluator());
        var actor = new RiskManagementFunctionActor(context);
        var completed = await RiskManagementFunctionTestDriver.ExecuteAsync(actor,command);
        saved.Should().NotBeNull();
        saved!.Result.Outcome.Should().Be(RiskAssessmentOutcome.Approved);
        var firstHash=RiskContracts.Hash(saved);
        await RiskManagementFunctionTestDriver.ExecuteAsync(actor,command);
        RiskContracts.Hash(saved).Should().Be(firstHash);
        await repository.Received(1).SaveCompletedStateAsync(Arg.Any<IFunctionActorContext>(),Arg.Any<RiskManagementFunctionState>(),
            Arg.Any<ExecuteRiskManagementPipelineCommand>(),Arg.Any<CancellationToken>());
        var changed=command with { IncrementalLossReserve=1 }; changed=changed with { InputSha256=changed.Fingerprint() };
        var conflict=await RiskManagementFunctionTestDriver.ExecuteAsync(actor,changed);
        conflict.Failed!.ReasonCode.Should().Be("RM.INPUT.CONFLICTING_DUPLICATE");
    }

    [Fact]
    public async Task Rehashed_cross_workflow_upstream_is_rejected_by_lineage_validation()
    {
        var command=await Command();
        command=command with { EntityId=command.EntityId with { WorkflowId=new(Guid.NewGuid()) } };
        command=command with { Subject=new(ActorType.Function,ExecuteRiskManagementPipelineCommand.Actor,ExecuteRiskManagementPipelineCommand.Verb,command.EntityId.Format()) };
        command=command with { InputSha256=command.Fingerprint() };
        new List<ValidationError>().ValidateRiskFields(command).Should().NotBeEmpty();
    }

    internal static Task<ExecuteRiskManagementPipelineCommand> Command(string variant = "LongFuture") => RiskFixture.Command(variant);
}
