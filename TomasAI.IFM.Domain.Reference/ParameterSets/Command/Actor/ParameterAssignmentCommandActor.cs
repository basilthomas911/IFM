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
public sealed class ParameterAssignmentCommandActor(ICommandActorContext<ParameterAssignmentCommandActor> context)
 : BaseEventSourceCommandActor<ParameterAssignmentCommandActor>(context,((IParameterAssignmentCommandContext)context).Logger)
{
 readonly ConcurrentDictionary<(string Stream,Guid Command),IAsyncDisposable> leases=new();
 public const string ActorName="ParameterAssignmentCommand";
 IParameterAssignmentCommandContext Services { get; } = (IParameterAssignmentCommandContext)context;
 static readonly IReadOnlyDictionary<string,Func<IActorMessage,ICommand>> _parseMap=new Dictionary<string,Func<IActorMessage,ICommand>>(StringComparer.Ordinal)
 {
 [AssignParameterVersionCommand.Verb]=m=>m.AsCommand<AssignParameterVersionCommand>()!,
 [DisableParameterAssignmentCommand.Verb]=m=>m.AsCommand<DisableParameterAssignmentCommand>()!,
 };
 static readonly IReadOnlyDictionary<Type,Func<ICommand,List<ValidationError>>> _validationMap=new Dictionary<Type,Func<ICommand,List<ValidationError>>>
 {
 [typeof(AssignParameterVersionCommand)]=c=>new List<ValidationError>().ValidateCommandId(c.CommandId,c.CommandName).CaptureCommandValidation(()=>ValidateIdentity((IParameterAssignmentMutation)c)),
 [typeof(DisableParameterAssignmentCommand)]=c=>new List<ValidationError>().ValidateCommandId(c.CommandId,c.CommandName).CaptureCommandValidation(()=>ValidateIdentity((IParameterAssignmentMutation)c)),
 };
 static readonly IReadOnlyDictionary<Type,Func<ICommand,IParameterAssignmentCommandContext,ParameterAssignmentCommandState,Task<ServiceResult<GuidResult>>>> _receiveMap=new Dictionary<Type,Func<ICommand,IParameterAssignmentCommandContext,ParameterAssignmentCommandState,Task<ServiceResult<GuidResult>>>>
 {
 [typeof(AssignParameterVersionCommand)]=(c,ctx,state)=>((AssignParameterVersionCommand)c).ExecuteAsync(ctx,state,ctx.Logger),
 [typeof(DisableParameterAssignmentCommand)]=(c,ctx,state)=>((DisableParameterAssignmentCommand)c).ExecuteAsync(ctx,state,ctx.Logger),
 };
 static void ValidateIdentity(IParameterAssignmentMutation c) { if(c.Scope is null||c.EntityId.AssignmentId!=TomasAI.IFM.Domain.Reference.ParameterSets.Model.WorkflowParameterScopeModel.AssignmentId(c.Scope)||c.ExpectedRevision<0)throw new ArgumentException("PARAM.IDENTITY_INVALID"); }
 protected override ValueTask<bool> ShouldProcessDuplicateAsync(ICommandActorContext<ParameterAssignmentCommandActor> ctx,ICommand cmd,CancellationToken token)=>ValueTask.FromResult(true);
 protected override ValueTask OnCommandFinishedAsync(ICommandActorContext<ParameterAssignmentCommandActor> ctx,ICommand? cmd)=>cmd is null?ValueTask.CompletedTask:ReleaseLease(cmd);
 protected override ICommand ParseMessage(ICommandActorContext<ParameterAssignmentCommandActor> ctx,IActorMessage msg)=>ParseMappedCommand(ctx,msg,_parseMap);
 protected override ValueTask OnValidateAsync(ICommandActorContext<ParameterAssignmentCommandActor> ctx,ActorThreadId id,ICommand cmd){ValidateMappedCommand(cmd,_validationMap);return ValueTask.CompletedTask;}
 protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext<ParameterAssignmentCommandActor> ctx,ActorThreadId id,ICommand cmd)
 {
  var lease=await Services.ConfigurationDb.AcquireParameterWriteLeaseAsync();
  if(!leases.TryAdd((cmd.StreamId,cmd.CommandId),lease)){await lease.DisposeAsync();throw new InvalidOperationException("PARAM.OPERATION_ALREADY_RUNNING");}
  try{return await Services.StateRepository.LoadStateAsync(cmd);}
  catch{await ReleaseLease(cmd);throw;}
 }
 protected override async ValueTask OnSaveStateAsync(ICommandActorContext<ParameterAssignmentCommandActor> ctx,ActorThreadId id,IActorState state,ICommand cmd)
 {try{await Services.StateRepository.SaveStateAsync(ctx,(ParameterAssignmentCommandState)state,cmd);}finally{await ReleaseLease(cmd);}}
 async ValueTask ReleaseLease(ICommand? cmd){if(cmd is not null&&leases.TryRemove((cmd.StreamId,cmd.CommandId),out var lease))await lease.DisposeAsync();}
 protected override async ValueTask OnStartup(ICommandActorContext<ParameterAssignmentCommandActor> ctx)=>await Services.EventProjector.StartAsync(ctx);
 protected override async ValueTask OnShutdown(ICommandActorContext<ParameterAssignmentCommandActor> ctx)=>await Services.EventProjector.StopAsync();
 protected override async ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<ParameterAssignmentCommandActor> ctx,IActorState state,ICommand cmd)=>await ResolveMappedCommandHandler(cmd,_receiveMap)(cmd,Services,(ParameterAssignmentCommandState)state);
 protected override async ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(ICommandActorContext<ParameterAssignmentCommandActor> ctx,ActorThreadId id,ICommand cmd,Exception ex){await ReleaseLease(cmd);return new ServiceFailed<GuidResult>(cmd?.ErrorCode??33010,ex.Message);}
}
