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

namespace TomasAI.IFM.Domain.Portfolio.CapacityReservation.Command.Actor;

/// <summary>Maps financial Commands. Each handler loads and mutates authority within the database transaction.</summary>
public sealed class CapacityReservationCommandActor(ICommandActorContext<CapacityReservationCommandActor> context,CapacityReservationCommandServices services,ILogger<CapacityReservationCommandActor> logger)
    :BaseEventSourceCommandActor<CapacityReservationCommandActor>(context,logger)
{
    public const string ActorName=ChangeCapacityReservationCommand.Actor;
    static readonly IReadOnlyDictionary<string,Func<IActorMessage,ICommand>> _parseMap=new Dictionary<string,Func<IActorMessage,ICommand>>(StringComparer.Ordinal)
    {
        [ChangeCapacityReservationCommand.Verb]=message=>message.AsCommand<ChangeCapacityReservationCommand>()!,
    }.ToFrozenDictionary(StringComparer.Ordinal);
    static readonly IReadOnlyDictionary<Type,Func<ICommand,List<ValidationError>>> _validationMap=new Dictionary<Type,Func<ICommand,List<ValidationError>>>
    {
        [typeof(ChangeCapacityReservationCommand)]=command=>new List<ValidationError>().ValidateFinancialRequest<ChangeCapacityReservationCommand,CapacityLifecycleRequest>((ChangeCapacityReservationCommand)command,ActorType.Command,ActorName,ChangeCapacityReservationCommand.Verb),
    }.ToFrozenDictionary();
    static readonly IReadOnlyDictionary<Type,Func<ICommand,CapacityReservationCommandServices,CancellationToken,ValueTask<ServiceResult<GuidResult>>>> _receiveMap=
        new Dictionary<Type,Func<ICommand,CapacityReservationCommandServices,CancellationToken,ValueTask<ServiceResult<GuidResult>>>>
        {
            [typeof(ChangeCapacityReservationCommand)]=(command,owner,token)=>((ChangeCapacityReservationCommand)command).ExecuteAsync(owner,token),
        }.ToFrozenDictionary();
    protected override ICommand ParseMessage(ICommandActorContext<CapacityReservationCommandActor> owner,IActorMessage message)=>ParseMappedCommand(owner,message,_parseMap);
    protected override ValueTask OnValidateAsync(ICommandActorContext<CapacityReservationCommandActor> owner,ActorThreadId threadId,ICommand command)
    {
        ValidateMappedCommand(command,_validationMap);
        return ValueTask.CompletedTask;
    }
    protected override ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext<CapacityReservationCommandActor> owner,ActorThreadId threadId,ICommand command)
        =>command.LoadFinancialStateAsync();
    protected override ValueTask<bool> ShouldProcessDuplicateAsync(ICommandActorContext<CapacityReservationCommandActor> owner,ICommand command,CancellationToken token)
        =>command.ReconcileFinancialDuplicateAsync(token);
    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<CapacityReservationCommandActor> owner,IActorState state,ICommand command)
        =>ResolveMappedCommandHandler(command,_receiveMap)(command,services,CancellationToken.None);
    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<CapacityReservationCommandActor> owner,IActorState state,ICommand command,CancellationToken token)
        =>ResolveMappedCommandHandler(command,_receiveMap)(command,services,token);
    protected override ValueTask OnStartup(ICommandActorContext<CapacityReservationCommandActor> owner,CancellationToken token)=>services.Projector.StartAsync(owner,token);
    protected override ValueTask OnShutdown(ICommandActorContext<CapacityReservationCommandActor> owner)=>services.Projector.StopAsync();
    protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(ICommandActorContext<CapacityReservationCommandActor> owner,ActorThreadId threadId,ICommand command,Exception exception)
        =>exception.FinancialCommandFailure(services.Logger);
}

public sealed class CapacityReservationCommandContext(IActorSupervisor supervisor)
    :CommandActorContext(supervisor,new ActorMailboxId(ActorType.Command,CapacityReservationCommandActor.ActorName)),ICommandActorContext<CapacityReservationCommandActor>;

