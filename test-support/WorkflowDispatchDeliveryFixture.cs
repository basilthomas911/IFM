using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.EventModelActor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Options;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.TestSupport;

/// <summary>Actual workflow actor/local scheduler; required read and command effects are controlled.</summary>
public sealed class WorkflowDispatchDeliveryFixture : IAsyncDisposable
{
    public sealed class Clock:TimeProvider
    {
        public DateTime At=new(2026,11,20,15,0,0,DateTimeKind.Utc);
        public override DateTimeOffset GetUtcNow()=>new(At);
    }
    public readonly Clock Time=new();
    public readonly IIntrinsicTimeStrategyWorkflowRealtimeContext Context=Substitute.For<IIntrinsicTimeStrategyWorkflowRealtimeContext>();
    public readonly ActorThreadPoolV2 Pool;
    public readonly ActorMailbox Queue;
    public readonly IActor Actor;
    public readonly List<ICommand> Commands=[];
    public readonly List<IntrinsicTimeStrategyWorkflowEntityId> Reads=[];
    public Func<IntrinsicTimeStrategyWorkflowEntityId,Task<IntrinsicTimeStrategyWorkflowView?>> ReadCurrent=_=>Task.FromResult<IntrinsicTimeStrategyWorkflowView?>(null);
    public Func<ICommand,ValueTask> Send=_=>ValueTask.CompletedTask;
    public bool TypedRejected;
    readonly IActorSupervisor supervisor;
    public WorkflowDispatchDeliveryFixture(bool metrics=false,Func<IIntrinsicTimeStrategyWorkflowRealtimeContext,IActor>? factory=null,WorkflowInitializationTerminals? terminals=null,WorkflowCompositionFailures? compositionFailures=null,WorkflowMarketConditionFailures? marketConditionFailures=null,WorkflowReservationFailures? reservationFailures=null,WorkflowCompositionFunctionTerminals? compositionFunctionTerminals=null,WorkflowSelectionFunctionFailures? selectionFunctionFailures=null,WorkflowAssessmentFunctionFailures? assessmentFunctionFailures=null,WorkflowRegimeFunctionFailures? regimeFunctionFailures=null,WorkflowPublicationJournal? publications=null,IWorkflowPublicationOwner? publicationOwner=null)
    {
        Context.Publications.Returns(publications);Context.PublicationOwner.Returns(publicationOwner);
        Context.InitializationTerminals.Returns(terminals);
        Context.CompositionFailures.Returns(compositionFailures);
        Context.MarketConditionFailures.Returns(marketConditionFailures);
        Context.ReservationFailures.Returns(reservationFailures);
        Context.CompositionFunctionTerminals.Returns(compositionFunctionTerminals);
        Context.SelectionFunctionFailures.Returns(selectionFunctionFailures);
        Context.AssessmentFunctionFailures.Returns(assessmentFunctionFailures);
        Context.RegimeFunctionFailures.Returns(regimeFunctionFailures);
        Context.PortfolioCommands.ReserveCompositionAsync(Arg.Any<ReserveFundOrderCompositionRequest>(),Arg.Any<PortfolioFundStrategySnapshot>())
            .Returns(new ServiceFailed<FundCompositionReservationResult>(9441,"Synthetic reservation rejected."));
        var id=new ActorMailboxId(ActorType.Realtime,IntrinsicTimeStrategyWorkflowRealtimeActor.ActorName);
        Context.ActorId.Returns(id);Context.Logger.Returns(NullLogger<IntrinsicTimeStrategyWorkflowRealtimeActor>.Instance);
        Context.TimeProvider.Returns(Time);Context.RegimeDiscoveryExecutionOptions.Returns(new RegimeDiscoveryExecutionOptions());
        Context.ReadActiveWorkflowAsync(Arg.Any<IntrinsicTimeStrategyWorkflowEntityId>(),Arg.Any<CancellationToken>())
            .Returns(call=>{var entity=call.Arg<IntrinsicTimeStrategyWorkflowEntityId>();Reads.Add(entity);return ReadCurrent(entity);});
        Context.SendAsync<CompleteMarketConditionCommand,IntrinsicTimeStrategyWorkflowEntityId>(Arg.Any<CompleteMarketConditionCommand>(),Arg.Any<IntrinsicTimeStrategyWorkflowEntityId>())
            .Returns(call=>Forward(call.Arg<CompleteMarketConditionCommand>()));
        Context.SendAsync<CompleteRegimeDiscoveryCommand,IntrinsicTimeStrategyWorkflowEntityId>(Arg.Any<CompleteRegimeDiscoveryCommand>(),Arg.Any<IntrinsicTimeStrategyWorkflowEntityId>())
            .Returns(call=>Forward(call.Arg<CompleteRegimeDiscoveryCommand>()));
        Context.SendAsync<FailRegimeDiscoveryCommand,IntrinsicTimeStrategyWorkflowEntityId>(Arg.Any<FailRegimeDiscoveryCommand>(),Arg.Any<IntrinsicTimeStrategyWorkflowEntityId>())
            .Returns(call=>Forward(call.Arg<FailRegimeDiscoveryCommand>()));
        Context.SendAsync<FailOrderCompositionCommand,IntrinsicTimeStrategyWorkflowEntityId>(Arg.Any<FailOrderCompositionCommand>(),Arg.Any<IntrinsicTimeStrategyWorkflowEntityId>())
            .Returns(call=>Forward(call.Arg<FailOrderCompositionCommand>()));
        Context.SendAsync<FailMarketConditionCommand,IntrinsicTimeStrategyWorkflowEntityId>(Arg.Any<FailMarketConditionCommand>(),Arg.Any<IntrinsicTimeStrategyWorkflowEntityId>())
            .Returns(call=>Forward(call.Arg<FailMarketConditionCommand>()));
        Context.SendAsync<FailTradeSelectionCommand,IntrinsicTimeStrategyWorkflowEntityId>(Arg.Any<FailTradeSelectionCommand>(),Arg.Any<IntrinsicTimeStrategyWorkflowEntityId>())
            .Returns(call=>Forward(call.Arg<FailTradeSelectionCommand>()));
        Context.SendAsync<TimeoutOrderCompositionCommand,IntrinsicTimeStrategyWorkflowEntityId>(Arg.Any<TimeoutOrderCompositionCommand>(),Arg.Any<IntrinsicTimeStrategyWorkflowEntityId>())
            .Returns(call=>Forward(call.Arg<TimeoutOrderCompositionCommand>()));
        supervisor=Substitute.For<IActorSupervisor>();var container=Substitute.For<IContainerInstance>();
        container.Resolve<IActorThreadQueue>().Returns(_=>new ActorThreadQueueV2(16));supervisor.Container.Returns(container);
        var producer=Substitute.For<IActorProducer>();
        producer.RequestAsync<CompleteMarketConditionCommand,IntrinsicTimeStrategyWorkflowEntityId,GuidResult>(Arg.Any<ActorSubject>(),Arg.Any<CompleteMarketConditionCommand>(),Arg.Any<IntrinsicTimeStrategyWorkflowEntityId>())
            .Returns(call=>TypedRejected?new ServiceFailed<GuidResult>(9342,"Injected typed workflow rejection."):new ServiceOk<GuidResult>(new GuidResult(call.Arg<CompleteMarketConditionCommand>().CommandId)));
        producer.RequestAsync<CompleteRegimeDiscoveryCommand,IntrinsicTimeStrategyWorkflowEntityId,GuidResult>(Arg.Any<ActorSubject>(),Arg.Any<CompleteRegimeDiscoveryCommand>(),Arg.Any<IntrinsicTimeStrategyWorkflowEntityId>())
            .Returns(call=>TypedRejected?new ServiceFailed<GuidResult>(9342,"Injected typed workflow rejection."):new ServiceOk<GuidResult>(new GuidResult(call.Arg<CompleteRegimeDiscoveryCommand>().CommandId)));
        producer.RequestAsync<FailRegimeDiscoveryCommand,IntrinsicTimeStrategyWorkflowEntityId,GuidResult>(Arg.Any<ActorSubject>(),Arg.Any<FailRegimeDiscoveryCommand>(),Arg.Any<IntrinsicTimeStrategyWorkflowEntityId>())
            .Returns(call=>TypedRejected?new ServiceFailed<GuidResult>(9342,"Injected typed workflow rejection."):new ServiceOk<GuidResult>(new GuidResult(call.Arg<FailRegimeDiscoveryCommand>().CommandId)));
        producer.RequestAsync<FailOrderCompositionCommand,IntrinsicTimeStrategyWorkflowEntityId,GuidResult>(Arg.Any<ActorSubject>(),Arg.Any<FailOrderCompositionCommand>(),Arg.Any<IntrinsicTimeStrategyWorkflowEntityId>())
            .Returns(call=>TypedRejected?new ServiceFailed<GuidResult>(9342,"Injected typed workflow rejection."):new ServiceOk<GuidResult>(new GuidResult(call.Arg<FailOrderCompositionCommand>().CommandId)));
        producer.RequestAsync<FailMarketConditionCommand,IntrinsicTimeStrategyWorkflowEntityId,GuidResult>(Arg.Any<ActorSubject>(),Arg.Any<FailMarketConditionCommand>(),Arg.Any<IntrinsicTimeStrategyWorkflowEntityId>())
            .Returns(call=>TypedRejected?new ServiceFailed<GuidResult>(9342,"Injected typed workflow rejection."):new ServiceOk<GuidResult>(new GuidResult(call.Arg<FailMarketConditionCommand>().CommandId)));
        producer.RequestAsync<FailTradeSelectionCommand,IntrinsicTimeStrategyWorkflowEntityId,GuidResult>(Arg.Any<ActorSubject>(),Arg.Any<FailTradeSelectionCommand>(),Arg.Any<IntrinsicTimeStrategyWorkflowEntityId>())
            .Returns(call=>TypedRejected?new ServiceFailed<GuidResult>(9342,"Injected typed workflow rejection."):new ServiceOk<GuidResult>(new GuidResult(call.Arg<FailTradeSelectionCommand>().CommandId)));
        producer.RequestAsync<TimeoutOrderCompositionCommand,IntrinsicTimeStrategyWorkflowEntityId,GuidResult>(Arg.Any<ActorSubject>(),Arg.Any<TimeoutOrderCompositionCommand>(),Arg.Any<IntrinsicTimeStrategyWorkflowEntityId>())
            .Returns(call=>TypedRejected?new ServiceFailed<GuidResult>(9342,"Injected typed workflow rejection."):new ServiceOk<GuidResult>(new GuidResult(call.Arg<TimeoutOrderCompositionCommand>().CommandId)));
        supervisor.GetProducer(Arg.Any<ActorMailboxId>()).Returns(producer);
        terminalContext=new(supervisor,id);
        Pool=new(supervisor,NullLogger.Instance);Pool.Initialize(1);supervisor.ThreadPool.Returns(Pool);
        supervisor.GetThread(Arg.Any<ActorThreadId>()).Returns(call=>Pool.GetThread(call.Arg<ActorThreadId>()));
        supervisor.GetThreadAsync(Arg.Any<ActorThreadId>(),Arg.Any<CancellationToken>()).Returns(call=>Pool.GetThreadAsync(call.Arg<ActorThreadId>(),call.Arg<CancellationToken>()));
        Queue=new(supervisor,id);var mailbox=Substitute.For<IActorMailbox>();mailbox.ThreadQueues.Returns(Queue.ThreadQueues);mailbox.Metrics.Returns((IActorMetricsStore)null!);
        supervisor.CreateMailbox(id).Returns(metrics?Queue:mailbox);Actor=factory?.Invoke(Context)??new IntrinsicTimeStrategyWorkflowRealtimeActor(Context);
        supervisor.Children.Returns(new Dictionary<ActorMailboxId,IActor>{[id]=Actor});
    }
    readonly EventActorContext terminalContext;
    async ValueTask Forward(ICommand command)
    {
        Commands.Add(command);await Send(command);
        if(command is CompleteMarketConditionCommand completedMarket)
            await terminalContext.SendAsync<CompleteMarketConditionCommand,IntrinsicTimeStrategyWorkflowEntityId>(completedMarket,completedMarket.EntityId);
        else if(command is CompleteRegimeDiscoveryCommand completedRegime)
            await terminalContext.SendAsync<CompleteRegimeDiscoveryCommand,IntrinsicTimeStrategyWorkflowEntityId>(completedRegime,completedRegime.EntityId);
        else if(command is FailRegimeDiscoveryCommand regime)
            await terminalContext.SendAsync<FailRegimeDiscoveryCommand,IntrinsicTimeStrategyWorkflowEntityId>(regime,regime.EntityId);
        else if(command is FailOrderCompositionCommand composition)
            await terminalContext.SendAsync<FailOrderCompositionCommand,IntrinsicTimeStrategyWorkflowEntityId>(composition,composition.EntityId);
        else if(command is FailMarketConditionCommand market)
            await terminalContext.SendAsync<FailMarketConditionCommand,IntrinsicTimeStrategyWorkflowEntityId>(market,market.EntityId);
        else if(command is FailTradeSelectionCommand selection)
            await terminalContext.SendAsync<FailTradeSelectionCommand,IntrinsicTimeStrategyWorkflowEntityId>(selection,selection.EntityId);
        else if(command is TimeoutOrderCompositionCommand timeout)
            await terminalContext.SendAsync<TimeoutOrderCompositionCommand,IntrinsicTimeStrategyWorkflowEntityId>(timeout,timeout.EntityId);
    }
    public static WorkflowStrategyStateUpdatedEvent Input(int sequence=1,StrategyWorkflowStage stage=StrategyWorkflowStage.RegimeDiscovery)
    {
        var at=new DateTime(2026,11,20,15,0,0,DateTimeKind.Utc);
        var entity=IntrinsicTimeStrategyWorkflowEntityId.Create(new FuturesItiSignalEntityId("ESZ6",new(2026,11,20),TimeFrameType.Daily));
        var view=new IntrinsicTimeStrategyWorkflowView
        {
            EntityId=entity,WorkflowId=new(new Guid(sequence+100,0,0,new byte[8])),WorkflowRevision=1,
            CurrentStage=stage,Status=WorkflowStrategyMachineStatus.Started,StartedAtUtc=at,UpdatedAtUtc=at,ExpiresAtUtc=at.AddMinutes(2),
            TriggerEventId=new Guid(sequence+200,0,0,new byte[8]),
            TriggerEvent=new FuturesItiSignalGeneratedEvent{EntityId=entity.ItiSignalEntityId,CreatedOn=at.AddMinutes(-1),ReceivedOn=at.AddMinutes(-1)}
        };
        return new(){Id=new Guid(sequence,0,0,new byte[8]),CommandId=new Guid(sequence+300,0,0,new byte[8]),EntityId=entity,
            WorkflowId=view.WorkflowId,WorkflowRevision=1,ReceivedOn=at,UpdatedAtUtc=at,State=view,DispatchRegimeDiscovery=stage==StrategyWorkflowStage.RegimeDiscovery,
            Subject=new(ActorType.Realtime,IntrinsicTimeStrategyWorkflowRealtimeActor.ActorName,WorkflowStrategyStateUpdatedEvent.Verb,"same")};
    }
    public ValueTask Start()=>Actor.StartAsync(supervisor);
    public static WorkflowStrategyStateUpdatedEvent CompositionInput(int sequence=1)
    {
        var source=Input(sequence,StrategyWorkflowStage.OrderComposition);
        return source with{State=source.State with{DecisionRuntimeVersion=1}};
    }
    public static WorkflowStrategyStateUpdatedEvent MarketConditionInput(int sequence=1)=>Input(sequence,StrategyWorkflowStage.MarketCondition);
    public int RegimeConfigurationReads {get;private set;}
    public int RegimeCaptures {get;private set;}
    TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Runtime.WorkflowDecisionExecutor? completionExecutor;
    public void BindRegimeCompletionInputs(WorkflowStrategyStateUpdatedEvent source)
    {
        BindRegimeInputs(source);
        if(completionExecutor is null)
        {
            completionExecutor=new(timeProvider:Time);
            Context.DecisionRuntime.Returns(new TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Runtime.WorkflowCalculationRuntime(completionExecutor,Time));
        }
    }
    public void BindRegimeInputs(WorkflowStrategyStateUpdatedEvent source)
    {
        var parameters=source.State.RegimeDiscoveryParameterSet.ParameterSetId != Guid.Empty ? source.State.RegimeDiscoveryParameterSet : TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery.RegimeDiscoveryParameterSet.CreateDefault(
            new Guid("00000000-0000-0000-0000-000000000001"),new Guid("00000000-0000-0000-0000-000000000002"),TimeFrameType.Daily);
        var resolved=new TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery.ResolvedRegimeDiscoveryParameterSet(parameters,
            TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery.RegimeDiscoveryParameterPayload.Serialize(parameters),
            TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery.RegimeDiscoveryParameterPayload.ComputeSha256(parameters),source.UpdatedAtUtc);
        var captured=new TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery.RegimeDiscoveryMarketSignalSnapshotResult
        {IsSuccess=true,Snapshot=new(){SnapshotId=new Guid("00000000-0000-0000-0000-000000000003"),CacheRevision=1,CapturedAtUtc=source.UpdatedAtUtc,MarketDataAsOfUtc=source.UpdatedAtUtc,TargetHorizon=TimeFrameType.Daily}};
        var config=Substitute.For<TomasAI.IFM.Application.Storage.ConfigurationDb.IConfigurationDbContext>();Context.ConfigurationDb.Returns(config);
        config.GetEffectiveRegimeDiscoveryAsync(Arg.Any<DateTime>(),Arg.Any<TimeFrameType>(),Arg.Any<CancellationToken>())
            .Returns(_=>{RegimeConfigurationReads++;return Task.FromResult<TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery.ResolvedRegimeDiscoveryParameterSet?>(resolved);});
        var provider=Substitute.For<TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery.IRegimeDiscoveryMarketSignalSnapshotProvider>();Context.RegimeDiscoverySnapshotProvider.Returns(provider);
        provider.CaptureAsync(Arg.Any<TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery.RegimeDiscoveryMarketSignalSnapshotRequest>(),Arg.Any<CancellationToken>())
            .Returns(_=>{RegimeCaptures++;return ValueTask.FromResult(captured);});
    }
    public static WorkflowStrategyStateUpdatedEvent RegimeFunctionInput(int sequence=1,bool representative=false)
    {
        var source=Input(sequence) with{DispatchRegimeDiscovery=false};
        var view=source.State with{DecisionRuntimeVersion=1};
        if(representative)
        {
            var parameters=TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery.RegimeDiscoveryParameterSet.CreateDefault(
                new Guid("00000000-0000-0000-0000-000000000001"),new Guid("00000000-0000-0000-0000-000000000002"),TimeFrameType.Daily);
            view=view with{RegimeDiscoveryParameterSet=parameters,RegimeDiscoveryParameterPayloadSha256=TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery.RegimeDiscoveryParameterPayload.ComputeSha256(parameters)};
        }
        return source with{State=view};
    }
    public static WorkflowStrategyStateUpdatedEvent AssessmentFunctionInput(int sequence=1)
    {
        var source=MarketConditionInput(sequence);
        var parameters=TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment.MarketConditionAssessmentParameterSet.CreateDefault(
            "ES.Standard",TimeFrameType.Daily,new Guid(sequence+800,0,0,new byte[8]),new Guid(sequence+900,0,0,new byte[8]),1);
        var binding=new TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment.MarketConditionAssessmentBinding
        {Parameters=parameters,PayloadSha256=TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment.MarketConditionAssessmentHash.Parameters(parameters)};
        var regime=StrategyStageResultEnvelope.CreateRegime(new TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model.RegimeDiscoveryResult
        {ResultId=new Guid(sequence+1000,0,0,new byte[8]),EntityId=source.EntityId,WorkflowId=source.WorkflowId,ProducedAtUtc=source.UpdatedAtUtc});
        return source with{State=source.State with{DecisionRuntimeVersion=1,AssessmentBinding=binding,RegimeDiscovery=source.State.RegimeDiscovery with{Result=regime}}};
    }
    public static WorkflowStrategyStateUpdatedEvent SelectionFunctionInput(int sequence=1)
    {
        var source=Input(sequence,StrategyWorkflowStage.TradeSelection);
        var view=source.State with{DecisionRuntimeVersion=1};
        var execute=new ExecuteTradeSelectionPipelineCommand
        {
            CommandId=new Guid(sequence+700,0,0,new byte[8]),EntityId=new(source.EntityId,source.WorkflowId,source.WorkflowRevision),
            InputWorkflowRevision=source.WorkflowRevision,
            WorkflowView=view,TriggerEvent=view.TriggerEvent,RequestedAtUtc=view.UpdatedAtUtc,EvaluatedAtUtc=view.UpdatedAtUtc,ExpiresAtUtc=view.ExpiresAtUtc,
            SelectionBinding=new(){PayloadSha256=new string('B',64)}
        };
        return source with{State=view with{SelectionDispatch=execute}};
    }
    public static WorkflowStrategyStateUpdatedEvent CompositionFunctionInput(int sequence=1)
    {
        var source=CompositionInput(sequence);
        var execute=new ExecuteOrderCompositionPipelineCommand
        {
            CommandId=new Guid(sequence+600,0,0,new byte[8]),EntityId=new(source.EntityId,source.WorkflowId,source.WorkflowRevision),
            InputWorkflowRevision=source.WorkflowRevision,WorkflowView=source.State,TriggerEvent=source.State.TriggerEvent,
            InputSha256=new string('A',64),RequestedAtUtc=source.State.UpdatedAtUtc,EvaluatedAtUtc=source.State.UpdatedAtUtc,ExpiresAtUtc=source.State.ExpiresAtUtc
        };
        return source with{State=source.State with{CompositionExecution=execute}};
    }
    public static WorkflowStrategyStateUpdatedEvent ReservationInput(int sequence=1)
    {
        var source=Input(sequence,StrategyWorkflowStage.TradeSelection);
        return source with{State=source.State with{SelectionBinding=new(),CompositionHandoff=new()
        {
            Status=CompositionHandoffStatus.ReservationPending,SelectionSourceEventId=source.Id,AcceptedSelectionRevision=1,UpdatedAtUtc=source.State.UpdatedAtUtc,
            Request=new(){WorkflowId=source.WorkflowId.Value,WorkflowRevision=1,ExpiresAtUtc=source.State.UpdatedAtUtc.AddMinutes(10)}
        }}};
    }
    public async Task<bool> Deliver(WorkflowStrategyStateUpdatedEvent source)
    {
        var delivery=new Delivery(source);
        if(!await Queue.ThreadQueues.WriteAsync(delivery))throw new IOException("Workflow source rejected by fixture queue.");
        await delivery.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(10));return await delivery.Settled.Task;
    }
    public void Reset(){Reads.Clear();Commands.Clear();Context.ClearReceivedCalls();}
    public async ValueTask DisposeAsync(){await Actor.StopAsync();await Pool.DisposeAsync();if(completionExecutor is not null)await completionExecutor.DisposeAsync();}
    sealed class Delivery(IEvent source):IActorMessage,IActorDeliveryCompletion
    {
        public ActorSubject Subject=>source.Subject;public ActorSubject ReplySubject{get;set;}
        public readonly TaskCompletionSource<bool> Settled=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Disposed=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TEvent? AsEvent<TEvent>()where TEvent:class,IEvent=>source as TEvent;
        public TCommand? AsCommand<TCommand>()where TCommand:class,ICommand=>null;
        public TQuery? AsQuery<TQuery,TResult>()where TQuery:class,IQuery<TResult>where TResult:class=>null;
        public ValueTask ReplyAsync<TResult>(TResult result)where TResult:class=>ValueTask.CompletedTask;
        public void ReleasePayload(){}public void Dispose()=>Disposed.TrySetResult();
        public ValueTask CompleteDeliveryAsync(bool succeeded){Settled.TrySetResult(succeeded);return ValueTask.CompletedTask;}
    }
}
