using MessagePack;
using System.Reflection;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Workflow;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.TradeSelection;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using NSubstitute;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Realtime;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Runtime;
using TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.OrderComposer;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.UnitTests.RealtimeDelivery;

/// <summary>Regression checks for the reproduced workflow output boundaries.</summary>
public sealed class WorkflowRemainingBoundaryAuditTests
{
    [Fact]
    public async Task Prepared_risk_lost_success_does_not_publish_contradictory_failure()
    {
        var view=await NeutralView();var clock=new Clock(view.UpdatedAtUtc);
        var entity=view.EntityId with{Assignment=new(1,1,"Audit","Future","ES",view.TriggerEvent.EntityId.TimePeriod,default,1,clock.At.AddDays(-1),null)};
        view=view with{EntityId=entity,DecisionRuntimeVersion=1,CurrentStage=StrategyWorkflowStage.RiskManagement,ExpiresAtUtc=clock.At.AddSeconds(30),RiskManagement=new(){StartedAtUtc=clock.At}};
        var context=Substitute.For<IIntrinsicTimeStrategyWorkflowRealtimeContext>();context.TimeProvider.Returns(clock);
        using var journal=new WorkflowPublicationJournal();context.Publications.Returns(journal);
        await using var executor=new WorkflowDecisionExecutor(timeProvider:clock);
        context.DecisionRuntime.Returns(new WorkflowCalculationRuntime(executor,clock,portfolioCalculator:new EmptyCalculator()));
        var financial=new PortfolioDecisionFinancialState(new FinancialBookConfiguration(),1,[],clock.At,clock.At.AddSeconds(30),1);
        var reader=Substitute.For<IPortfolioDecisionStateReader>();context.FinancialDecisionState.Returns(reader);
        reader.TryAcquire(Arg.Any<int>(),Arg.Any<int>(),Arg.Any<DateTime>(),out Arg.Any<PortfolioDecisionFinancialState?>())
            .Returns(call=>{call[3]=financial;return true;});
        CompleteRiskManagementCommand? accepted=null;FailRiskManagementCommand? contradiction=null;
        context.SendAsync<CompleteRiskManagementCommand,IntrinsicTimeStrategyWorkflowEntityId>(Arg.Any<CompleteRiskManagementCommand>(),Arg.Any<IntrinsicTimeStrategyWorkflowEntityId>())
            .Returns(call=>{accepted=call.Arg<CompleteRiskManagementCommand>();return ValueTask.FromException(new IOException("Success accepted but its reply was lost."));});
        context.SendAsync<FailRiskManagementCommand,IntrinsicTimeStrategyWorkflowEntityId>(Arg.Any<FailRiskManagementCommand>(),Arg.Any<IntrinsicTimeStrategyWorkflowEntityId>())
            .Returns(call=>{contradiction=call.Arg<FailRiskManagementCommand>();return ValueTask.CompletedTask;});
        await Assert.ThrowsAsync<IOException>(()=>PreparedPortfolioRisk.EvaluateAsync(view,context).AsTask());
        Assert.NotNull(accepted);Assert.NotNull(accepted.EntryApproval);Assert.Null(contradiction);
        Assert.Same(accepted,Assert.Single(journal.Pending()).Command);
        Assert.True(context.DecisionRuntime!.MatchesApproval(view,accepted.EntryApproval));
        var rehydrated=MessagePackSerializer.Deserialize<CompleteRiskManagementCommand>(MessagePackSerializer.Serialize(accepted));
        Assert.False(context.DecisionRuntime.MatchesApproval(view,rehydrated.EntryApproval!));
    }

    [Fact]
    public async Task Entry_commit_confirmation_preserves_bytes_under_same_identity_on_retry()
    {
        var view=await NeutralView();var clock=new Clock(view.UpdatedAtUtc);
        var approval=new PortfolioEntryApproval{Request=new(){OperationId=Guid.NewGuid()},ProducedAtUtc=clock.At,ValidUntilUtc=clock.At.AddMinutes(1)};
        view=view with{EntryCommit=new(approval,WorkflowEntryCommitPhase.Approved,clock.At,"ENTRY.COMMIT.PENDING")};
        var context=Substitute.For<IIntrinsicTimeStrategyWorkflowRealtimeContext>();context.TimeProvider.Returns(clock);
        var service=Substitute.For<IPortfolioEntryCommitApi>();context.EntryCommits.Returns(service);
        service.CommitOnceAsync(approval,Arg.Any<CancellationToken>()).Returns(Task.FromResult(new PortfolioEntryCommitResult(PortfolioEntryCommitOutcome.Rejected,null,"Same refusal")));
        var captured=new List<CompleteRiskManagementCommand>();
        context.SendAsync<CompleteRiskManagementCommand,IntrinsicTimeStrategyWorkflowEntityId>(Arg.Any<CompleteRiskManagementCommand>(),Arg.Any<IntrinsicTimeStrategyWorkflowEntityId>())
            .Returns(call=>{captured.Add(call.Arg<CompleteRiskManagementCommand>());return ValueTask.FromException(new IOException("Outcome accepted but reply lost."));});
        await Assert.ThrowsAsync<IOException>(()=>PreparedPortfolioRisk.CommitAsync(view,context).AsTask());clock.At=clock.At.AddSeconds(1);
        await Assert.ThrowsAsync<IOException>(()=>PreparedPortfolioRisk.CommitAsync(view,context).AsTask());
        Assert.Equal(2,captured.Count);Assert.Equal(captured[0].CommandId,captured[1].CommandId);Assert.Equal(captured[0].CompletedAtUtc,captured[1].CompletedAtUtc);
        Assert.Equal(captured[0].EntryCommit!.UpdatedAtUtc,captured[1].EntryCommit!.UpdatedAtUtc);
        Assert.True(MessagePackSerializer.Serialize(captured[0]).AsSpan().SequenceEqual(MessagePackSerializer.Serialize(captured[1])));
    }

    [Fact]
    public async Task Frozen_portfolio_retry_preserves_original_hash_and_request_time()
    {
        var view=await NeutralView();using var journal=new WorkflowPublicationJournal();var key=WorkflowPublicationDelivery.Key(view,"portfolio-request");var digest=WorkflowPublicationDelivery.Digest(view);
        var first=journal.FreezeInput(key,digest,()=>PortfolioOrderCompositionMapper.CreateRequest(view,1,view.UpdatedAtUtc));
        var retry=journal.FreezeInput(key,digest,()=>PortfolioOrderCompositionMapper.CreateRequest(view,1,view.UpdatedAtUtc.AddSeconds(1)));
        Assert.Equal(first.OperationId,retry.OperationId);Assert.Equal(first.CommandId,retry.CommandId);Assert.Equal(first.InputSha256,retry.InputSha256);
        Assert.Equal(MessagePackSerializer.Serialize(first.Body),MessagePackSerializer.Serialize(retry.Body));
    }

    [Fact]
    public async Task Legacy_financial_transport_refusal_remains_unknown_and_retries_the_same_frozen_operation()
    {
        var view=(await NeutralView()) with{DecisionRuntimeVersion=0,CurrentStage=StrategyWorkflowStage.RiskManagement};
        var clock=new Clock(view.UpdatedAtUtc);using var journal=new WorkflowPublicationJournal();
        var context=Substitute.For<IIntrinsicTimeStrategyWorkflowRealtimeContext>();context.TimeProvider.Returns(clock);context.Publications.Returns(journal);
        context.Options.Returns(new IntrinsicTimeStrategyWorkflowOptions());context.ReadActiveWorkflowAsync(view.EntityId).Returns(Task.FromResult<IntrinsicTimeStrategyWorkflowView?>(view));
        var requests=new List<EvaluatePortfolioOrderCompositionCommand>();
        context.PortfolioOrderComposition.EvaluateAsync(Arg.Any<EvaluatePortfolioOrderCompositionCommand>(),Arg.Any<CancellationToken>())
            .Returns(call=>
            {
                requests.Add(call.Arg<EvaluatePortfolioOrderCompositionCommand>());
                return ValueTask.FromResult<ServiceResult<FunctionResult<PortfolioOrderCompositionCompletedEvent,PortfolioOrderCompositionFailedEvent>>>(
                    new ServiceFailed<FunctionResult<PortfolioOrderCompositionCompletedEvent,PortfolioOrderCompositionFailedEvent>>(1234,"Reply missing after possible commit"));
            });
        var source=new WorkflowStrategyStateUpdatedEvent{EntityId=view.EntityId,WorkflowId=view.WorkflowId,WorkflowRevision=view.WorkflowRevision,State=view};
        await Assert.ThrowsAsync<IOException>(()=>ExecuteRiskManagement.ExecuteAsync(source,context).AsTask());clock.At=clock.At.AddSeconds(1);
        await Assert.ThrowsAsync<IOException>(()=>ExecuteRiskManagement.ExecuteAsync(source,context).AsTask());
        Assert.Equal(2,requests.Count);Assert.Equal(MessagePackSerializer.Serialize(requests[0]),MessagePackSerializer.Serialize(requests[1]));
        await context.DidNotReceive().SendAsync<FailRiskManagementCommand,IntrinsicTimeStrategyWorkflowEntityId>(Arg.Any<FailRiskManagementCommand>(),Arg.Any<IntrinsicTimeStrategyWorkflowEntityId>());
    }

    [Fact]
    public async Task Idempotent_reservation_receipt_preserves_workflow_completion_bytes()
    {
        var fixture=await TradeSelectionHandoffTests.Fixture.Create();fixture.Accept();
        var view=fixture.State.CurrentView!;var pending=view.CompositionHandoff!;
        var aggregate=new PortfolioFundCompositionAggregate();
        var original=aggregate.Reserve(pending.Request,fixture.Command.SelectionBinding.PortfolioSnapshot,7001,[8001],fixture.Clock.Now,"audit");
        var restored=new PortfolioFundCompositionAggregate();restored.Restore(aggregate.CaptureState());
        var replay=restored.Reserve(pending.Request,fixture.Command.SelectionBinding.PortfolioSnapshot,7999,[8999],fixture.Clock.Now.AddSeconds(1),"audit");
        Assert.Equal(ReservationDisposition.Committed,original.Disposition);Assert.Equal(ReservationDisposition.IdempotentReplay,replay.Disposition);
        Assert.Equal(original.Order.OrderId,replay.Order.OrderId);Assert.Equal(original.CommittedOnUtc,replay.CommittedOnUtc);
        var source=new WorkflowStrategyStateUpdatedEvent{Id=Guid.NewGuid(),EntityId=view.EntityId,WorkflowId=view.WorkflowId,WorkflowRevision=view.WorkflowRevision,State=view};
        var context=Substitute.For<IIntrinsicTimeStrategyWorkflowRealtimeContext>();var captured=new List<CompleteTradeSelectionReservationCommand>();
        context.SendAsync<CompleteTradeSelectionReservationCommand,IntrinsicTimeStrategyWorkflowEntityId>(Arg.Any<CompleteTradeSelectionReservationCommand>(),Arg.Any<IntrinsicTimeStrategyWorkflowEntityId>())
            .Returns(call=>{captured.Add(call.Arg<CompleteTradeSelectionReservationCommand>());return ValueTask.CompletedTask;});
        var method=typeof(IntrinsicTimeStrategyWorkflowRealtimeActor).GetMethod("CompleteReservationAsync",BindingFlags.Static|BindingFlags.NonPublic)!;
        var map=method.CreateDelegate<Func<IEventActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor>,WorkflowStrategyStateUpdatedEvent,FundCompositionReservationResult,ValueTask>>();
        await map(context,source,original);await map(context,source,replay);
        Assert.Equal(2,captured.Count);Assert.Equal(captured[0].CommandId,captured[1].CommandId);Assert.Equal(captured[0].CompletedAtUtc,captured[1].CompletedAtUtc);
        Assert.True(MessagePackSerializer.Serialize(captured[0]).AsSpan().SequenceEqual(MessagePackSerializer.Serialize(captured[1])));
    }

    static async Task<IntrinsicTimeStrategyWorkflowView> NeutralView()
    {
        var command=await CompositionFixture.Command("LongFuture");
        var result=new OrderComposer(new Black76ComposerPricer()).Calculate(command);
        result=result with{Candidate=result.Candidate! with{PortfolioId=0,FundId=0,OrderId=0,PrimaryTradeId=0},DecisionContext=result.DecisionContext with{PortfolioId=0,FundId=0}};
        return command.WorkflowView with{WorkflowRevision=6,UpdatedAtUtc=command.EvaluatedAtUtc,SelectionBinding=command.SelectionBinding with{SchemaVersion=2},CompositionExecution=command,
            OrderComposition=command.WorkflowView.OrderComposition with{ProcessingStatus=StrategyActorProcessingStatus.Completed,SourceEventId=result.ResultId,Result=StrategyStageResultEnvelope.CreateComposition(result)}};
    }
    sealed class Clock(DateTime at):TimeProvider{public DateTime At=at;public override DateTimeOffset GetUtcNow()=>new(At);}
    sealed class EmptyCalculator:IPortfolioEntryDecisionCalculator
    {public PortfolioFundEntryAssessment[] Calculate(EvaluatePortfolioOrderCompositionCommand request,PortfolioDecisionFinancialState state,CancellationToken token)=>[];}
}
