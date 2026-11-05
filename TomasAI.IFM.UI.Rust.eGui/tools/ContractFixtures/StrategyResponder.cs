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
   if(verb=="GetFuturesItiSignalHistory") {var q=MessagePackSerializer.Deserialize<GetFuturesItiSignalHistoryQuery>(bytes,options);if(q.Subject.ToString()!=subject||q.Symbol!="ES"||q.ValueDate!=new DateOnly(2026,10,26)||q.EntityId.Format()!=$"ES.2026-10-26.{q.TimePeriod}")throw new InvalidDataException("Incorrect ITI history scope");return Result(q.TimePeriod==TimeFrameType.Daily||Mode=="WeeklyWorkflowTimeout"?new[]{StrategyFixtures.Signal with{TimePeriod=q.TimePeriod}}:[]);}
   if (verb == "GetStrategyUpdatesGrid")
   {
       var q = MessagePackSerializer.Deserialize<GetIntrinsicTimeStrategyUpdatesGridQuery>(bytes, options);
       if (q.Subject.ToString() != subject || q.Symbol != "ES" || q.PageSize != 50 || q.PageNumber <= 0
           || q.FromUtc.Kind != DateTimeKind.Utc || !DesktopFixtureMode.Clock(q.ToUtc, StrategyFixtures.At))
           throw new InvalidDataException("Incorrect Strategy Updates scope");
       if (Mode == "WeeklyWorkflowTimeout" && q.TimePeriod == TimeFrameType.Weekly)
           return null;

       IntrinsicTimeStrategyUpdatesGridRow[] items = [];
       if (q.PageNumber == 1 && (q.TimePeriod == TimeFrameType.Daily || Mode == "AllFrames"))
       {
           var row = IntrinsicTimeStrategyUpdatesGridRow.FromView(view);
           var id = q.TimePeriod switch
           {
               TimeFrameType.Weekly => new StrategyWorkflowId(Guid.Parse("5fcae8d2-8fa4-47fb-85b3-3ee4afe4e65d")),
               TimeFrameType.Monthly => new StrategyWorkflowId(Guid.Parse("36e1aa9f-802b-4631-a721-fdf75f825639")),
               _ => row.WorkflowId
           };
           items = [row with { WorkflowId = id }];
       }

       return Result(new IntrinsicTimeStrategyUpdatesGridPage(items, q.PageNumber, 50, items.Length));
   }
   if (verb == "GetById")
   {
       var q = MessagePackSerializer.Deserialize<GetIntrinsicTimeStrategyWorkflowByIdQuery>(bytes, options);
       if (Mode == "AllFrames" || q.WorkflowId != view.WorkflowId || q.Subject.ToString() != subject
           || q.EntityId.Format() != view.WorkflowId.ToString() || q.MinimumWorkflowRevision > view.WorkflowRevision)
           throw new InvalidDataException("Unexpected strategy detail request");
       var payload = Mode == "WrongIdentity" ? view with { WorkflowId = new(Guid.NewGuid()) } : view;
       return Result(new IntrinsicTimeStrategyWorkflowReadModel(view.WorkflowId, view.EntityId.Format(),
           view.EntityId.WorkflowDefinitionId, view.WorkflowDefinitionVersion, "ESZ6", new(2026,10,26),
           TimeFrameType.Daily, view.TriggerEventId, view.CorrelationId, StrategyWorkflowStatus.Running,
           view.Outcome, view.CurrentStage, view.WorkflowRevision, 1, 1,
           MessagePackSerializer.Serialize(payload, MessagePackBinarySerializer.ContentOptions),
           view.StopReasonCode, view.StartedAtUtc, view.TerminalAtUtc, view.UpdatedAtUtc));
   }
   throw new InvalidDataException("Unknown strategy fixture query "+verb);
  }catch(Exception error){return MessagePackSerializer.Serialize(new ServiceResult<GuidResult>{Success=false,ErrorCode=3400,ErrorMessage=error.Message},options);}
 }

}
