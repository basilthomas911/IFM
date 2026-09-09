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
public sealed class GeneralLedgerCommandActor(ICommandActorContext<GeneralLedgerCommandActor> context,GeneralLedgerCommandServices services,ILogger<GeneralLedgerCommandActor> logger)
    :BaseEventSourceCommandActor<GeneralLedgerCommandActor>(context,logger)
{
    public const string ActorName=PostFundTransactionCommand.Actor;
    static readonly IReadOnlyDictionary<string,Func<IActorMessage,ICommand>> _parseMap=new Dictionary<string,Func<IActorMessage,ICommand>>(StringComparer.Ordinal)
    {
        [PostFundTransactionCommand.Verb]=message=>message.AsCommand<PostFundTransactionCommand>()!,
        [PostFundTransactionsCommand.Verb]=message=>message.AsCommand<PostFundTransactionsCommand>()!,
    }.ToFrozenDictionary(StringComparer.Ordinal);
    static readonly IReadOnlyDictionary<Type,Func<ICommand,List<ValidationError>>> _validationMap=new Dictionary<Type,Func<ICommand,List<ValidationError>>>
    {
        [typeof(PostFundTransactionCommand)]=command=>new List<ValidationError>().ValidateFinancialRequest<PostFundTransactionCommand,LedgerPostingRequest>((PostFundTransactionCommand)command,ActorType.Command,ActorName,PostFundTransactionCommand.Verb),
        [typeof(PostFundTransactionsCommand)]=command=>new List<ValidationError>().ValidateFinancialRequest<PostFundTransactionsCommand,LedgerPostingBatchRequest>((PostFundTransactionsCommand)command,ActorType.Command,ActorName,PostFundTransactionsCommand.Verb),
    }.ToFrozenDictionary();
    static readonly IReadOnlyDictionary<Type,Func<ICommand,GeneralLedgerCommandServices,CancellationToken,ValueTask<ServiceResult<GuidResult>>>> _receiveMap=
        new Dictionary<Type,Func<ICommand,GeneralLedgerCommandServices,CancellationToken,ValueTask<ServiceResult<GuidResult>>>>
        {
            [typeof(PostFundTransactionCommand)]=(command,owner,token)=>((PostFundTransactionCommand)command).ExecuteAsync(owner,token),
            [typeof(PostFundTransactionsCommand)]=(command,owner,token)=>((PostFundTransactionsCommand)command).ExecuteAsync(owner,token),
        }.ToFrozenDictionary();
    protected override ICommand ParseMessage(ICommandActorContext<GeneralLedgerCommandActor> owner,IActorMessage message)=>ParseMappedCommand(owner,message,_parseMap);
    protected override ValueTask OnValidateAsync(ICommandActorContext<GeneralLedgerCommandActor> owner,ActorThreadId threadId,ICommand command)
    {
        ValidateMappedCommand(command,_validationMap);
        return ValueTask.CompletedTask;
    }
    protected override ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext<GeneralLedgerCommandActor> owner,ActorThreadId threadId,ICommand command)
        =>command.LoadFinancialStateAsync();
    protected override ValueTask<bool> ShouldProcessDuplicateAsync(ICommandActorContext<GeneralLedgerCommandActor> owner,ICommand command,CancellationToken token)
        =>command.ReconcileFinancialDuplicateAsync(token);
    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<GeneralLedgerCommandActor> owner,IActorState state,ICommand command)
        =>ResolveMappedCommandHandler(command,_receiveMap)(command,services,CancellationToken.None);
    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<GeneralLedgerCommandActor> owner,IActorState state,ICommand command,CancellationToken token)
        =>ResolveMappedCommandHandler(command,_receiveMap)(command,services,token);
    protected override ValueTask OnStartup(ICommandActorContext<GeneralLedgerCommandActor> owner,CancellationToken token)=>services.Projector.StartAsync(owner,token);
    protected override ValueTask OnShutdown(ICommandActorContext<GeneralLedgerCommandActor> owner)=>services.Projector.StopAsync();
    protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(ICommandActorContext<GeneralLedgerCommandActor> owner,ActorThreadId threadId,ICommand command,Exception exception)
        =>exception.FinancialCommandFailure(services.Logger);
}

public sealed class GeneralLedgerCommandContext(IActorSupervisor supervisor)
    :CommandActorContext(supervisor,new ActorMailboxId(ActorType.Command,GeneralLedgerCommandActor.ActorName)),ICommandActorContext<GeneralLedgerCommandActor>;

