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
public sealed class ParameterSetCommandActor(ICommandActorContext<ParameterSetCommandActor> context)
 : BaseEventSourceCommandActor<ParameterSetCommandActor>(context,((IParameterSetCommandContext)context).Logger)
{
 readonly ConcurrentDictionary<(string Stream,Guid Command),IAsyncDisposable> leases=new();
 public const string ActorName="ParameterSetCommand";
 IParameterSetCommandContext Services { get; } = (IParameterSetCommandContext)context;
 static readonly IReadOnlyDictionary<string,Func<IActorMessage,ICommand>> _parseMap=new Dictionary<string,Func<IActorMessage,ICommand>>(StringComparer.Ordinal)
 {
 [CreateParameterSetCommand.Verb]=m=>m.AsCommand<CreateParameterSetCommand>()!,
 [SaveParameterDraftCommand.Verb]=m=>m.AsCommand<SaveParameterDraftCommand>()!,
 [RenameParameterSetCommand.Verb]=m=>m.AsCommand<RenameParameterSetCommand>()!,
 [PublishParameterVersionCommand.Verb]=m=>m.AsCommand<PublishParameterVersionCommand>()!,
 [RetireParameterVersionCommand.Verb]=m=>m.AsCommand<RetireParameterVersionCommand>()!,
 };
 static readonly IReadOnlyDictionary<Type,Func<ICommand,List<ValidationError>>> _validationMap=new Dictionary<Type,Func<ICommand,List<ValidationError>>>
 {
 [typeof(CreateParameterSetCommand)]=c=>new List<ValidationError>().ValidateCommandId(c.CommandId,c.CommandName).CaptureCommandValidation(()=>ValidateIdentity((IParameterSetMutation)c)),
 [typeof(SaveParameterDraftCommand)]=c=>new List<ValidationError>().ValidateCommandId(c.CommandId,c.CommandName).CaptureCommandValidation(()=>ValidateIdentity((IParameterSetMutation)c)),
 [typeof(RenameParameterSetCommand)]=c=>new List<ValidationError>().ValidateCommandId(c.CommandId,c.CommandName).CaptureCommandValidation(()=>ValidateIdentity((IParameterSetMutation)c)),
 [typeof(PublishParameterVersionCommand)]=c=>new List<ValidationError>().ValidateCommandId(c.CommandId,c.CommandName).CaptureCommandValidation(()=>ValidateIdentity((IParameterSetMutation)c)),
 [typeof(RetireParameterVersionCommand)]=c=>new List<ValidationError>().ValidateCommandId(c.CommandId,c.CommandName).CaptureCommandValidation(()=>ValidateIdentity((IParameterSetMutation)c)),
 };
 static readonly IReadOnlyDictionary<Type,Func<ICommand,IParameterSetCommandContext,ParameterSetCommandState,Task<ServiceResult<GuidResult>>>> _receiveMap=new Dictionary<Type,Func<ICommand,IParameterSetCommandContext,ParameterSetCommandState,Task<ServiceResult<GuidResult>>>>
 {
 [typeof(CreateParameterSetCommand)]=(c,ctx,state)=>((CreateParameterSetCommand)c).ExecuteAsync(ctx,state,ctx.Logger),
 [typeof(SaveParameterDraftCommand)]=(c,ctx,state)=>((SaveParameterDraftCommand)c).ExecuteAsync(ctx,state,ctx.Logger),
 [typeof(RenameParameterSetCommand)]=(c,ctx,state)=>((RenameParameterSetCommand)c).ExecuteAsync(ctx,state,ctx.Logger),
 [typeof(PublishParameterVersionCommand)]=(c,ctx,state)=>((PublishParameterVersionCommand)c).ExecuteAsync(ctx,state,ctx.Logger),
 [typeof(RetireParameterVersionCommand)]=(c,ctx,state)=>((RetireParameterVersionCommand)c).ExecuteAsync(ctx,state,ctx.Logger),
 };
 static void ValidateIdentity(IParameterSetMutation c) { if(c.EntityId.SetId==Guid.Empty||c.ExpectedRevision<0)throw new ArgumentException("PARAM.IDENTITY_INVALID"); }
 protected override ValueTask<bool> ShouldProcessDuplicateAsync(ICommandActorContext<ParameterSetCommandActor> ctx,ICommand cmd,CancellationToken token)=>ValueTask.FromResult(true);
 protected override ValueTask OnCommandFinishedAsync(ICommandActorContext<ParameterSetCommandActor> ctx,ICommand? cmd)=>cmd is null?ValueTask.CompletedTask:ReleaseLease(cmd);
 protected override ICommand ParseMessage(ICommandActorContext<ParameterSetCommandActor> ctx,IActorMessage msg)=>ParseMappedCommand(ctx,msg,_parseMap);
 protected override ValueTask OnValidateAsync(ICommandActorContext<ParameterSetCommandActor> ctx,ActorThreadId id,ICommand cmd){ValidateMappedCommand(cmd,_validationMap);return ValueTask.CompletedTask;}
 protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext<ParameterSetCommandActor> ctx,ActorThreadId id,ICommand cmd)
 {
  if(cmd is CreateParameterSetCommand or SaveParameterDraftCommand)
  {
   var mutation=(IParameterSetMutation)cmd;
   if(await Services.ConfigurationDb.ReadParameterSchemaAsync(mutation.ComponentCode,mutation.SchemaVersion) is null)
    throw new InvalidOperationException("PARAM.SCHEMA_UNREGISTERED");
  }
  var lease=await Services.ConfigurationDb.AcquireParameterWriteLeaseAsync();
  if(!leases.TryAdd((cmd.StreamId,cmd.CommandId),lease)){await lease.DisposeAsync();throw new InvalidOperationException("PARAM.OPERATION_ALREADY_RUNNING");}
  try{return await Services.StateRepository.LoadStateAsync(cmd);}
  catch{await ReleaseLease(cmd);throw;}
 }
 protected override async ValueTask OnSaveStateAsync(ICommandActorContext<ParameterSetCommandActor> ctx,ActorThreadId id,IActorState state,ICommand cmd)
 {try{await Services.StateRepository.SaveStateAsync(ctx,(ParameterSetCommandState)state,cmd);}finally{await ReleaseLease(cmd);}}
 async ValueTask ReleaseLease(ICommand? cmd){if(cmd is not null&&leases.TryRemove((cmd.StreamId,cmd.CommandId),out var lease))await lease.DisposeAsync();}
 protected override async ValueTask OnStartup(ICommandActorContext<ParameterSetCommandActor> ctx)=>await Services.EventProjector.StartAsync(ctx);
 protected override async ValueTask OnShutdown(ICommandActorContext<ParameterSetCommandActor> ctx)=>await Services.EventProjector.StopAsync();
 protected override async ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<ParameterSetCommandActor> ctx,IActorState state,ICommand cmd)=>await ResolveMappedCommandHandler(cmd,_receiveMap)(cmd,Services,(ParameterSetCommandState)state);
 protected override async ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(ICommandActorContext<ParameterSetCommandActor> ctx,ActorThreadId id,ICommand cmd,Exception ex){await ReleaseLease(cmd);return new ServiceFailed<GuidResult>(cmd?.ErrorCode??33010,ex.Message);}
}
