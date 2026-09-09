using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Command;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.CapacityReservation.Emulator;

/// <summary>Maps financial Commands. Each handler loads and mutates authority within the database transaction.</summary>
public sealed class EmulatorExecutionCommandActor(ICommandActorContext<EmulatorExecutionCommandActor> context,EmulatorExecutionCommandServices services,ILogger<EmulatorExecutionCommandActor> logger)
    :BaseEventSourceCommandActor<EmulatorExecutionCommandActor>(context,logger)
{
    public const string ActorName=SubmitEmulatorOrderCommand.Actor;
    static readonly IReadOnlyDictionary<string,Func<IActorMessage,ICommand>> _parseMap=new Dictionary<string,Func<IActorMessage,ICommand>>(StringComparer.Ordinal)
    {
        [SubmitEmulatorOrderCommand.Verb]=message=>message.AsCommand<SubmitEmulatorOrderCommand>()!,
    }.ToFrozenDictionary(StringComparer.Ordinal);
    static readonly IReadOnlyDictionary<Type,Func<ICommand,List<ValidationError>>> _validationMap=new Dictionary<Type,Func<ICommand,List<ValidationError>>>
    {
        [typeof(SubmitEmulatorOrderCommand)]=command=>new List<ValidationError>().ValidateEmulatorOrder((SubmitEmulatorOrderCommand)command),
    }.ToFrozenDictionary();
    static readonly IReadOnlyDictionary<Type,Func<ICommand,EmulatorExecutionCommandServices,CancellationToken,ValueTask<ServiceResult<GuidResult>>>> _receiveMap=
        new Dictionary<Type,Func<ICommand,EmulatorExecutionCommandServices,CancellationToken,ValueTask<ServiceResult<GuidResult>>>>
        {
            [typeof(SubmitEmulatorOrderCommand)]=(command,owner,token)=>((SubmitEmulatorOrderCommand)command).ExecuteAsync(owner,token),
        }.ToFrozenDictionary();
    protected override ICommand ParseMessage(ICommandActorContext<EmulatorExecutionCommandActor> owner,IActorMessage message)=>ParseMappedCommand(owner,message,_parseMap);
    protected override ValueTask OnValidateAsync(ICommandActorContext<EmulatorExecutionCommandActor> owner,ActorThreadId threadId,ICommand command)
    {
        ValidateMappedCommand(command,_validationMap);
        return ValueTask.CompletedTask;
    }
    protected override ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext<EmulatorExecutionCommandActor> owner,ActorThreadId threadId,ICommand command)
        =>command.LoadFinancialStateAsync();
    protected override ValueTask<bool> ShouldProcessDuplicateAsync(ICommandActorContext<EmulatorExecutionCommandActor> owner,ICommand command,CancellationToken token)
        =>command.ReconcileFinancialDuplicateAsync(token);
    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<EmulatorExecutionCommandActor> owner,IActorState state,ICommand command)
        =>ResolveMappedCommandHandler(command,_receiveMap)(command,services,CancellationToken.None);
    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<EmulatorExecutionCommandActor> owner,IActorState state,ICommand command,CancellationToken token)
        =>ResolveMappedCommandHandler(command,_receiveMap)(command,services,token);
    protected override ValueTask OnStartup(ICommandActorContext<EmulatorExecutionCommandActor> owner,CancellationToken token)=>services.Projector.StartAsync(owner,token);
    protected override ValueTask OnShutdown(ICommandActorContext<EmulatorExecutionCommandActor> owner)=>services.Projector.StopAsync();
    protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(ICommandActorContext<EmulatorExecutionCommandActor> owner,ActorThreadId threadId,ICommand command,Exception exception)
        =>exception.FinancialCommandFailure(services.Logger);
}

public sealed class EmulatorExecutionCommandContext(IActorSupervisor supervisor)
    :CommandActorContext(supervisor,new ActorMailboxId(ActorType.Command,EmulatorExecutionCommandActor.ActorName)),ICommandActorContext<EmulatorExecutionCommandActor>;

