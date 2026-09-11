using System.Collections.Concurrent;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.State;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.Extensions;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Shared.Domain;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Command.Actor;
public sealed class ParameterStartupCommandActor(ICommandActorContext<ParameterStartupCommandActor> context)
 : BaseEventSourceCommandActor<ParameterStartupCommandActor>(context,((IParameterStartupCommandContext)context).Logger)
{
 readonly ConcurrentDictionary<(string Stream,Guid Command),IAsyncDisposable> leases=new();
 public const string ActorName="ParameterStartupCommand";
 IParameterStartupCommandContext Services { get; } = (IParameterStartupCommandContext)context;
 static readonly IReadOnlyDictionary<string,Func<IActorMessage,ICommand>> _parseMap=new Dictionary<string,Func<IActorMessage,ICommand>>(StringComparer.Ordinal)
 {
 [ApplySignalStartupPlanCommand.Verb]=m=>m.AsCommand<ApplySignalStartupPlanCommand>()!,
 [ReleaseSignalStartupPlanCommand.Verb]=m=>m.AsCommand<ReleaseSignalStartupPlanCommand>()!,
 [RecordSignalStartupReportCommand.Verb]=m=>m.AsCommand<RecordSignalStartupReportCommand>()!,
 };
 static readonly IReadOnlyDictionary<Type,Func<ICommand,List<ValidationError>>> _validationMap=new Dictionary<Type,Func<ICommand,List<ValidationError>>>
 {
 [typeof(ApplySignalStartupPlanCommand)]=c=>new List<ValidationError>().ValidateCommandId(c.CommandId,c.CommandName).CaptureCommandValidation(()=>ValidateIdentity((IParameterStartupMutation)c)),
 [typeof(ReleaseSignalStartupPlanCommand)]=c=>new List<ValidationError>().ValidateCommandId(c.CommandId,c.CommandName).CaptureCommandValidation(()=>ValidateIdentity((IParameterStartupMutation)c)),
 [typeof(RecordSignalStartupReportCommand)]=c=>new List<ValidationError>().ValidateCommandId(c.CommandId,c.CommandName).CaptureCommandValidation(()=>ValidateIdentity((IParameterStartupMutation)c)),
 };
 static readonly IReadOnlyDictionary<Type,Func<ICommand,IParameterStartupCommandContext,ParameterStartupCommandState,Task<ServiceResult<GuidResult>>>> _receiveMap=new Dictionary<Type,Func<ICommand,IParameterStartupCommandContext,ParameterStartupCommandState,Task<ServiceResult<GuidResult>>>>
 {
 [typeof(ApplySignalStartupPlanCommand)]=(c,ctx,state)=>((ApplySignalStartupPlanCommand)c).ExecuteAsync(ctx,state,ctx.Logger),
 [typeof(ReleaseSignalStartupPlanCommand)]=(c,ctx,state)=>((ReleaseSignalStartupPlanCommand)c).ExecuteAsync(ctx,state,ctx.Logger),
 [typeof(RecordSignalStartupReportCommand)]=(c,ctx,state)=>((RecordSignalStartupReportCommand)c).ExecuteAsync(ctx,state,ctx.Logger),
 };
 static void ValidateIdentity(IParameterStartupMutation c)
 {
  if(c.EntityId!=ParameterStartupEntityId.Registry||c.RunId==Guid.Empty)throw new ArgumentException("PARAM.STARTUP_ID_INVALID");
  if(c is ApplySignalStartupPlanCommand&&c.CommandId!=c.RunId)throw new ArgumentException("PARAM.STARTUP_OPERATION_ID_INVALID");
 }
 protected override async ValueTask<bool> ShouldProcessDuplicateAsync(ICommandActorContext<ParameterStartupCommandActor> ctx,ICommand cmd,CancellationToken token)
 {
  var previous=await Services.DbEventSource.GetCommandLogAsync(cmd.CommandId).WaitAsync(token)
   ??throw new InvalidOperationException("PARAM.OPERATION_RESERVATION_NOT_READY");
  TomasAI.IFM.Domain.Reference.ParameterSets.Model.ParameterStartupOperationModel.ValidateDuplicate((IParameterStartupMutation)cmd,previous.CommandName,previous.StreamId,previous.CommandData);
  // A committed Apply can never reactivate a released run. An audited but uncommitted attempt can be retried explicitly.
  return !await Services.DbEventSource.HasEventForCommandAsync(cmd.CommandId).WaitAsync(token);
 }
 protected override ValueTask OnCommandFinishedAsync(ICommandActorContext<ParameterStartupCommandActor> ctx,ICommand? cmd)=>cmd is null?ValueTask.CompletedTask:ReleaseLease(cmd);
 protected override ICommand ParseMessage(ICommandActorContext<ParameterStartupCommandActor> ctx,IActorMessage msg)=>ParseMappedCommand(ctx,msg,_parseMap);
 protected override ValueTask OnValidateAsync(ICommandActorContext<ParameterStartupCommandActor> ctx,ActorThreadId id,ICommand cmd){ValidateMappedCommand(cmd,_validationMap);return ValueTask.CompletedTask;}
 protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext<ParameterStartupCommandActor> ctx,ActorThreadId id,ICommand cmd)
 {
  var lease=await Services.ConfigurationDb.AcquireParameterWriteLeaseAsync();
  if(!leases.TryAdd((cmd.StreamId,cmd.CommandId),lease)){await lease.DisposeAsync();throw new InvalidOperationException("PARAM.OPERATION_ALREADY_RUNNING");}
  try{return await Services.StateRepository.LoadStateAsync(cmd);}
  catch{await ReleaseLease(cmd);throw;}
 }
 protected override async ValueTask OnSaveStateAsync(ICommandActorContext<ParameterStartupCommandActor> ctx,ActorThreadId id,IActorState state,ICommand cmd)
 {try{await Services.StateRepository.SaveStateAsync(ctx,(ParameterStartupCommandState)state,cmd);}finally{await ReleaseLease(cmd);}}
 async ValueTask ReleaseLease(ICommand? cmd){if(cmd is not null&&leases.TryRemove((cmd.StreamId,cmd.CommandId),out var lease))await lease.DisposeAsync();}
 protected override async ValueTask OnStartup(ICommandActorContext<ParameterStartupCommandActor> ctx)=>await Services.EventProjector.StartAsync(ctx);
 protected override async ValueTask OnShutdown(ICommandActorContext<ParameterStartupCommandActor> ctx)=>await Services.EventProjector.StopAsync();
 protected override async ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<ParameterStartupCommandActor> ctx,IActorState state,ICommand cmd)=>await ResolveMappedCommandHandler(cmd,_receiveMap)(cmd,Services,(ParameterStartupCommandState)state);
 protected override async ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(ICommandActorContext<ParameterStartupCommandActor> ctx,ActorThreadId id,ICommand cmd,Exception ex){await ReleaseLease(cmd);return new ServiceFailed<GuidResult>(cmd?.ErrorCode??33010,ex.Message);}
}
