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

namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger.Command.Actor;

/// <summary>Maps financial Commands. Each handler loads and mutates authority within the database transaction.</summary>
public sealed class LedgerConfigurationCommandActor(ICommandActorContext<LedgerConfigurationCommandActor> context,LedgerConfigurationCommandServices services,ILogger<LedgerConfigurationCommandActor> logger)
    :BaseEventSourceCommandActor<LedgerConfigurationCommandActor>(context,logger)
{
    public const string ActorName=ConfigureLedgerCommand.Actor;
    static readonly IReadOnlyDictionary<string,Func<IActorMessage,ICommand>> _parseMap=new Dictionary<string,Func<IActorMessage,ICommand>>(StringComparer.Ordinal)
    {
        [ConfigureLedgerCommand.Verb]=message=>message.AsCommand<ConfigureLedgerCommand>()!,
    }.ToFrozenDictionary(StringComparer.Ordinal);
    static readonly IReadOnlyDictionary<Type,Func<ICommand,List<ValidationError>>> _validationMap=new Dictionary<Type,Func<ICommand,List<ValidationError>>>
    {
        [typeof(ConfigureLedgerCommand)]=command=>new List<ValidationError>().ValidateLedgerConfiguration((ConfigureLedgerCommand)command),
    }.ToFrozenDictionary();
    static readonly IReadOnlyDictionary<Type,Func<ICommand,LedgerConfigurationCommandServices,CancellationToken,ValueTask<ServiceResult<GuidResult>>>> _receiveMap=
        new Dictionary<Type,Func<ICommand,LedgerConfigurationCommandServices,CancellationToken,ValueTask<ServiceResult<GuidResult>>>>
        {
            [typeof(ConfigureLedgerCommand)]=(command,owner,token)=>((ConfigureLedgerCommand)command).ExecuteAsync(owner,token),
        }.ToFrozenDictionary();
    protected override ICommand ParseMessage(ICommandActorContext<LedgerConfigurationCommandActor> owner,IActorMessage message)=>ParseMappedCommand(owner,message,_parseMap);
    protected override ValueTask OnValidateAsync(ICommandActorContext<LedgerConfigurationCommandActor> owner,ActorThreadId threadId,ICommand command)
    {
        ValidateMappedCommand(command,_validationMap);
        return ValueTask.CompletedTask;
    }
    protected override ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext<LedgerConfigurationCommandActor> owner,ActorThreadId threadId,ICommand command)
        =>command.LoadFinancialStateAsync();
    protected override ValueTask<bool> ShouldProcessDuplicateAsync(ICommandActorContext<LedgerConfigurationCommandActor> owner,ICommand command,CancellationToken token)
        =>command.ReconcileFinancialDuplicateAsync(token);
    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<LedgerConfigurationCommandActor> owner,IActorState state,ICommand command)
        =>ResolveMappedCommandHandler(command,_receiveMap)(command,services,CancellationToken.None);
    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<LedgerConfigurationCommandActor> owner,IActorState state,ICommand command,CancellationToken token)
        =>ResolveMappedCommandHandler(command,_receiveMap)(command,services,token);
    protected override ValueTask OnStartup(ICommandActorContext<LedgerConfigurationCommandActor> owner,CancellationToken token)=>services.Projector.StartAsync(owner,token);
    protected override ValueTask OnShutdown(ICommandActorContext<LedgerConfigurationCommandActor> owner)=>services.Projector.StopAsync();
    protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(ICommandActorContext<LedgerConfigurationCommandActor> owner,ActorThreadId threadId,ICommand command,Exception exception)
        =>exception.FinancialCommandFailure(services.Logger);
}

public sealed class LedgerConfigurationCommandContext(IActorSupervisor supervisor)
    :CommandActorContext(supervisor,new ActorMailboxId(ActorType.Command,LedgerConfigurationCommandActor.ActorName)),ICommandActorContext<LedgerConfigurationCommandActor>;


