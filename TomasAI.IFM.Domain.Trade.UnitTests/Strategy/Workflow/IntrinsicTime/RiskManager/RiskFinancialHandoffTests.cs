using System.Collections.Immutable;
using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Realtime;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.RiskManager;

[Trait("Category","PortfolioFinancial")]
public sealed class RiskFinancialHandoffTests
{
    [Fact]
    public async Task Reservation_preserves_real_risk_quantity_hashes_and_stable_operation_on_wire()
    {
        var (view,request)=await Input();
        var risk=view.RiskManagement.Result!.RiskResult!;
        request.Body.StrategyUnits.Should().Be(risk.StrategyUnits);
        request.Body.RiskAssessmentHash.Should().Be(RiskContracts.Hash(risk));
        request.Body.SizedOrderHash.Should().Be(risk.SizedOrderHash);
        request.ExpectedFinancialRevision.Should().Be(12);
        var wire=MessagePackBinarySerializer.Shared.Deserialize<ReservePortfolioTradeRiskCommand>(MessagePackBinarySerializer.Shared.Serialize(request))!;
        FinancialCanonicalHash.Request(wire).Should().Be(request.InputSha256);
        wire.OperationId.Should().Be(request.OperationId);
        wire.Body.TradeIds.Should().Equal(request.Body.TradeIds);
        var authorization=RiskFinancialHandoff.Authorize(wire,Grant(wire));
        authorization.Validate();
        authorization.CompositionResultHash.Should().Be(risk.CompositionResultHash);
        authorization.UnitCandidateHash.Should().Be(risk.UnitCandidateHash);
    }

    [Theory]
    [InlineData("quantity")] [InlineData("sized")] [InlineData("composition")]
    [InlineData("operation")] [InlineData("event")] [InlineData("requirements")]
    [InlineData("fund")] [InlineData("trade")]
    public async Task Handoff_rejects_a_receipt_for_different_sized_order_or_operation(string change)
    {
        var (_,request)=await Input(); var grant=Grant(request); var receipt=grant.Receipt;
        receipt=change switch
        {
            "quantity"=>receipt with { StrategyUnits=receipt.StrategyUnits+1 },
            "sized"=>receipt with { SizedOrderHash=new('F',64) },
            "composition"=>receipt with { CompositionResultHash=new('F',64) },
            "operation"=>receipt with { OperationId=Guid.NewGuid() },
            "event"=>receipt with { CompletedEventId=Guid.NewGuid() },
            "requirements"=>receipt with { Requirements=receipt.Requirements with { LossCharge=receipt.Requirements.LossCharge+1 } },
            "fund"=>receipt with { FundId=receipt.FundId+1 },
            _=>receipt with { TradeIds=[int.MaxValue] }
        };
        Action act=()=>RiskFinancialHandoff.Authorize(request,grant with { Receipt=receipt });
        act.Should().Throw<RiskCalculationException>();
    }

    [Fact]
    public async Task Execution_acceptance_is_typed_in_saved_workflow_and_consumption_keeps_original_identity()
    {
        var (view,request)=await Input(); var grant=Grant(request);
        var authorization=RiskFinancialHandoff.Authorize(request,grant);
        view=view with { FinancialHandoff=new() { Phase=RiskFinancialHandoffPhase.FundPending,ReservationRequest=request,
            Reservation=grant,Authorization=authorization } };
        var advance=RiskFinancialHandoff.Advance(view);
        var intent=new CapacityExecutionAcceptance
        {
            ExecutionId=advance.CommandId,ExecutionRevision=1,PortfolioId=request.PortfolioId,FundId=request.Body.FundId,
            OrderId=request.Body.OrderId,ReservationId=request.Body.ReservationId,SizedOrderHash=request.Body.SizedOrderHash,
            RequirementsHash=request.Body.Requirements.ContentHash,Environment="Emulator",ValidUntilUtc=request.Body.ValidUntilUtc
        };
        var consume=RiskFinancialHandoff.Consume(view,intent,13,Guid.NewGuid(),request.RequestedAtUtc);
        consume.Body.ExecutionId.Should().Be(advance.CommandId);
        consume.OperationId.Should().NotBe(request.OperationId);
        consume.ExpiresAtUtc.Should().Be(request.ExpiresAtUtc);
        consume.Body.RemainingUnits.Should().Be(request.Body.StrategyUnits);
        view=view with { FinancialHandoff=view.FinancialHandoff with { Phase=RiskFinancialHandoffPhase.ConsumePending,
            ExecutionAcceptance=intent,ConsumptionRequest=consume } };
        var snapshot=new WorkflowStrategyStateUpdatedEvent { CommandId=advance.CommandId,State=view };
        var wire=MessagePackBinarySerializer.Shared.Deserialize<WorkflowStrategyStateUpdatedEvent>(MessagePackBinarySerializer.Shared.Serialize(snapshot))!;
        ((ICapacityExecutionAcceptedEvent)wire).CapacityAcceptance.Should().Be(intent);
        FinancialCanonicalHash.Request(wire.State.FinancialHandoff!.ConsumptionRequest!).Should().Be(consume.InputSha256);
        RiskFinancialHandoff.Advance(wire.State).CommandId.Should().Be(RiskFinancialHandoff.Advance(view).CommandId);
    }

    [Fact]
    public async Task Fund_authorization_finishes_pipeline_with_unconsumed_intent_and_no_emulator_submission()
    {
        var (view,request)=await Input(); var grant=Grant(request);
        var authorization=RiskFinancialHandoff.Authorize(request,grant);
        var fundCommand=Guid.NewGuid();
        view=view with { FinancialHandoff=new() { Phase=RiskFinancialHandoffPhase.FundPending,
            ReservationRequest=request,Reservation=grant,Authorization=authorization,FundCommandId=fundCommand } };
        var state=new IntrinsicTimeStrategyWorkflowCommandState();
        state.Apply(new WorkflowStrategyStateUpdatedEvent { State=view,EntityId=view.EntityId,
            WorkflowId=view.WorkflowId,WorkflowRevision=view.WorkflowRevision },false).Should().BeTrue();
        var api=Substitute.For<IPortfolioFinancialApi>();
        var accepted=new FundRiskAuthorizationEvidence(fundCommand,Guid.NewGuid(),authorization);
        api.GetFundRiskAuthorizationAsync(Arg.Any<FinancialReadScope>(),Arg.Any<GetFundRiskAuthorizationRequest>(),Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<FinancialRead<FundRiskAuthorizationEvidence>>(new(FinancialReadStatus.Found,accepted,14,request.RequestedAtUtc)));
        var context=Substitute.For<IIntrinsicTimeStrategyWorkflowCommandContext>();
        context.FinancialApi.Returns(api); context.TimeProvider.Returns(new FixedClock(request.RequestedAtUtc));
        var advance=RiskFinancialHandoff.Advance(view);
        await advance.ExecuteAsync(context,state);
        var completed=state.CurrentView!;
        completed.Status.Should().Be(WorkflowStrategyMachineStatus.Completed);
        completed.FinancialHandoff!.Phase.Should().Be(RiskFinancialHandoffPhase.Authorized);
        completed.FinancialHandoff.FundAcceptance.Should().Be(accepted);
        completed.FinancialHandoff.ExecutionAcceptance!.ReservationId.Should().Be(authorization.ReservationId);
        completed.FinancialHandoff.ConsumptionRequest.Should().BeNull();
        completed.FinancialHandoff.Consumption.Should().BeNull();
        completed.FinancialHandoff.SubmissionRequest.Should().BeNull();
        completed.FinancialHandoff.Submission.Should().BeNull();
        var saved=MessagePackBinarySerializer.Shared.Serialize(completed);
        await advance.ExecuteAsync(context,state);
        MessagePackBinarySerializer.Shared.Serialize(state.CurrentView).Should().Equal(saved);
        var wire=MessagePackBinarySerializer.Shared.Deserialize<IntrinsicTimeStrategyWorkflowView>(saved)!;
        wire.FinancialHandoff!.Phase.Should().Be(RiskFinancialHandoffPhase.Authorized);
    }

    [Theory]
    [InlineData(RiskFinancialHandoffPhase.Authorized)]
    [InlineData(RiskFinancialHandoffPhase.ConsumePending)]
    [InlineData(RiskFinancialHandoffPhase.Consumed)]
    [InlineData(RiskFinancialHandoffPhase.Submitted)]
    public async Task Current_or_historical_execution_checkpoint_never_dispatches_consumption_or_emulator(RiskFinancialHandoffPhase phase)
    {
        var context=Substitute.For<IIntrinsicTimeStrategyWorkflowRealtimeContext>();
        var api=Substitute.For<IPortfolioFinancialApi>(); context.FinancialApi.Returns(api);
        var view=new IntrinsicTimeStrategyWorkflowView { FinancialHandoff=new() { Phase=phase } };
        await view.ExecuteFinancialHandoffAsync(context);
        api.ReceivedCalls().Should().BeEmpty();
        context.ReceivedCalls().Where(x=>x.GetMethodInfo().Name=="SendAsync").Should().BeEmpty();
    }

    sealed class FixedClock(DateTime now):TimeProvider
    { public override DateTimeOffset GetUtcNow()=>new(now); }

    static async Task<(IntrinsicTimeStrategyWorkflowView,ReservePortfolioTradeRiskCommand)> Input()
    {
        var request=await RiskFixture.Command();
        request=request with { SizingAuthority=request.SizingAuthority with { Environment="Emulator" },
            Funding=request.Funding.Select(x=>x with { Evidence=x.Evidence with { Environment="Emulator" } }).ToImmutableArray() };
        request=request with { InputSha256=request.Fingerprint() };
        var result=new RiskEvaluator().Calculate(request);
        result.Outcome.Should().Be(RiskAssessmentOutcome.Approved);
        var view=new IntrinsicTimeStrategyWorkflowView
        {
            EntityId=request.WorkflowEntityId,WorkflowId=request.WorkflowId,WorkflowRevision=request.InputWorkflowRevision+1,
            CorrelationId=request.CorrelationId,ExpiresAtUtc=request.ExpiresAtUtc,RiskExecution=request,
            Status=WorkflowStrategyMachineStatus.Started,CurrentStage=StrategyWorkflowStage.RiskManagement,
            RiskManagement=new() { ProcessingStatus=StrategyActorProcessingStatus.Completed,Result=StrategyStageResultEnvelope.CreateRisk(result),SourceEventId=request.CommandId }
        };
        var snapshot=new FinancialAdmissionSnapshot(42,result.PortfolioId,result.FundId,"Active",true,true,result.Authority,
            1000000000,[],[],"Emulator","emulator/account");
        return (view,RiskFinancialHandoff.Reserve(view,result,new(FinancialReadStatus.Found,snapshot,12,request.EvaluatedAtUtc),request.EvaluatedAtUtc));
    }

    static CapacityReservationCompletedEvent Grant(ReservePortfolioTradeRiskCommand request)
    {
        var b=request.Body; var eventId=Guid.NewGuid();
        return new()
        {
            Id=eventId,CommandId=request.CommandId,OperationId=request.OperationId,PortfolioId=request.PortfolioId,InputHash=request.InputSha256,
            Receipt=new()
            {
                OperationId=request.OperationId,ReservationId=b.ReservationId,PortfolioId=request.PortfolioId,FundId=b.FundId,BookId=b.BookId,
                OrderId=b.OrderId,TradeIds=b.TradeIds,RiskResultId=b.RiskResultId,RiskAssessmentHash=b.RiskAssessmentHash,
                CompositionResultHash=b.CompositionResultHash,UnitCandidateHash=b.UnitCandidateHash,SizedOrderHash=b.SizedOrderHash,
                StrategyUnits=b.StrategyUnits,Requirements=b.Requirements,AuthorityEpoch=b.Authority.AuthorityEpoch,FinancialRevision=13,
                GrantedAtUtc=request.RequestedAtUtc,ValidUntilUtc=b.ValidUntilUtc,ExecutionEnvironment=b.ExecutionEnvironment,
                CompletedEventId=eventId,InputHash=request.InputSha256
            }
        };
    }
}
