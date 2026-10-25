using MessagePack;
using NATS.Client.Core;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Queries;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;
/// <summary>Complete state hydration and typed notify fixture; no live strategy or market feed starts.</summary>
internal sealed class StrategyResponder {
 public string Mode{get;set;}="Normal";
 IntrinsicTimeStrategyWorkflowView view=StrategyFixtures.View;
 public async Task<byte[]?> Handle(NatsConnection connection,string subject,byte[] bytes,CancellationToken token){
  var options=MessagePackBinarySerializer.Options;var verb=subject.Split('.')[2];byte[] Result<T>(T value)=>MessagePackSerializer.Serialize(new ServiceResult<T>{Success=true,ErrorMessage="",Value=value},options);
  try{
   if(subject=="Query.NativeCompatibility.StrategyEmit.none"){
    var mode=MessagePackSerializer.Deserialize<string>(bytes,options);
    if(mode=="Signal"||mode=="DuplicateSignal"){
     var signal=mode=="Signal"?StrategyFixtures.Signal with{IntrinsicPrice=6510.5,SequenceId=6}:StrategyFixtures.Signal;
     var route=new ActorSubject(ActorType.Notify,FuturesItiSignalUpdatedNotifyEvent.Actor,FuturesItiSignalUpdatedNotifyEvent.Verb,"fixture");
     await connection.PublishAsync(route.ToString(),MessagePackSerializer.Serialize(new FuturesItiSignalUpdatedNotifyEvent{Subject=route,FuturesItiSignal=signal,Id=Guid.NewGuid(),ReceivedOn=StrategyFixtures.At},options),cancellationToken:token);
    }else{
     var sent=mode switch{"Stale"=>view with{WorkflowRevision=view.WorkflowRevision-1},"Conflict"=>view with{StopReasonCode="conflicting fixture"},_=>view with{WorkflowRevision=view.WorkflowRevision+1,Status=WorkflowStrategyMachineStatus.Completed,Outcome=StrategyWorkflowOutcome.Completed,UpdatedAtUtc=StrategyFixtures.At,MarketCondition=new(){ProcessingStatus=StrategyActorProcessingStatus.Completed,ContinuationDecision=StrategyWorkflowContinuationDecision.Proceed}}};if(mode=="Workflow")view=sent;
     var route=new ActorSubject(ActorType.Notify,IntrinsicTimeStrategyWorkflowUpdatedNotifyEvent.Actor,IntrinsicTimeStrategyWorkflowUpdatedNotifyEvent.Verb,view.EntityId.Format());
     await connection.PublishAsync(route.ToString(),MessagePackSerializer.Serialize(new IntrinsicTimeStrategyWorkflowUpdatedNotifyEvent{Subject=route,State=sent,WorkflowId=sent.WorkflowId,WorkflowRevision=sent.WorkflowRevision,EntityId=sent.EntityId},options),cancellationToken:token);
    }return Result(true);
   }
   if(verb=="GetFuturesItiSignalHistory") {var q=MessagePackSerializer.Deserialize<GetFuturesItiSignalHistoryQuery>(bytes,options);if(q.Subject.ToString()!=subject||q.Symbol!="ES"||q.ValueDate!=new DateOnly(2026,10,26)||q.EntityId.Format()!=$"ES.2026-10-26.{q.TimePeriod}")throw new InvalidDataException("Incorrect ITI history scope");return Result(q.TimePeriod==TimeFrameType.Daily?new[]{StrategyFixtures.Signal}:[]);}
   if(verb=="GetHistoryPage"){
    var q=MessagePackSerializer.Deserialize<GetIntrinsicTimeStrategyWorkflowHistoryPageQuery>(bytes,options);if(q.Subject.ToString()!=subject||q.Symbol!="ES"||q.PageSize!=50||q.PageNumber<=0||q.FromUtc.Kind!=DateTimeKind.Utc||!DesktopFixtureMode.Clock(q.ToUtc,StrategyFixtures.At))throw new InvalidDataException("Incorrect workflow history range/page");
    var items=q.TimePeriod==TimeFrameType.Daily&&q.PageNumber==1?new[]{new IntrinsicTimeStrategyWorkflowHistoryReadModel(view.EntityId.Format(),view.StartedAtUtc,view.WorkflowId,StrategyWorkflowStatus.Running,view.Outcome,view.CurrentStage,view.WorkflowRevision,view.TerminalAtUtc,view.StopReasonCode)}:[];
    return Result(new IntrinsicTimeStrategyWorkflowHistoryPageReadModel(items,q.PageNumber,50,items.Length));
   }
   if(verb is "GetByIds" or "GetById"){
    if(verb=="GetByIds"){var q=MessagePackSerializer.Deserialize<GetIntrinsicTimeStrategyWorkflowsByIdsQuery>(bytes,options);if(q.WorkflowIds is not [var id]||id!=view.WorkflowId||q.MinimumRevisions is not [var revision]||revision>view.WorkflowRevision)throw new InvalidDataException("Incorrect workflow hydration batch");}
    else{var q=MessagePackSerializer.Deserialize<GetIntrinsicTimeStrategyWorkflowByIdQuery>(bytes,options);if(q.WorkflowId!=view.WorkflowId||q.Subject.ToString()!=subject||q.EntityId.Format()!=view.WorkflowId.ToString())throw new InvalidDataException("Incorrect single workflow lookup");}
    var result=new IntrinsicTimeStrategyWorkflowReadModel(view.WorkflowId,view.EntityId.Format(),view.EntityId.WorkflowDefinitionId,view.WorkflowDefinitionVersion,"ESZ6",new(2026,10,26),TimeFrameType.Daily,view.TriggerEventId,view.CorrelationId,StrategyWorkflowStatus.Running,view.Outcome,view.CurrentStage,view.WorkflowRevision,1,1,MessagePackSerializer.Serialize(view,MessagePackBinarySerializer.ContentOptions),view.StopReasonCode,view.StartedAtUtc,view.TerminalAtUtc,view.UpdatedAtUtc);
    if(Mode=="WrongIdentity")result=result with{WorkflowId=new(Guid.NewGuid())};return verb=="GetById"?Result(result):Result(new[]{result});
   }
   throw new InvalidDataException("Unknown strategy fixture query "+verb);
  }catch(Exception error){return MessagePackSerializer.Serialize(new ServiceResult<GuidResult>{Success=false,ErrorCode=3400,ErrorMessage=error.Message},options);}
 }
}
