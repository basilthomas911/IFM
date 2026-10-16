using TomasAI.IFM.Domain.Trade.Futures.Command.Validation;
using System.Collections.Frozen;
using TomasAI.IFM.Domain.Trade.Futures.Option.Command;
using TomasAI.IFM.Domain.Trade.Futures.Option.Command.State;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Command.Actor;
public sealed class FuturesOptionTradeCommandActor(ICommandActorContext<FuturesOptionTradeCommandActor> context) : BaseEventSourceCommandActor<FuturesOptionTradeCommandActor>(context, Typed(context).Logger)
{
    // The repository commits audit, source events, and material rows in one PostgreSQL transaction.
    protected override bool AuditIsCommittedWithState(ICommand command)
        => string.Equals(_services.DbFactory.TradeDb.ProviderName, "System.Data.Postgres", StringComparison.Ordinal);

    protected override bool IsCommittedDuplicateException(ICommand command, Exception exception)
        => AuditIsCommittedWithState(command)
            && exception is TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit.CommandAuditDuplicateException
                { HasCommittedEvents: true } duplicate && duplicate.CommandId == command.CommandId;

    public const string ActorName = FuturesOptionTradeActorNames.Command;
    readonly IFuturesOptionTradeCommandContext _services = Typed(context);
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> _parseMap = new Dictionary<string, Func<IActorMessage, ICommand>>
(StringComparer.Ordinal)    {
        [CreateOptionTradeCommand.Verb] = message => message.AsCommand<CreateOptionTradeCommand>()!,
        [AmendOptionTradeEvidenceCommand.Verb] = message => message.AsCommand<AmendOptionTradeEvidenceCommand>()!,
        [BeginCloseOptionTradeCommand.Verb] = message => message.AsCommand<BeginCloseOptionTradeCommand>()!,
        [CloseOptionTradeCommand.Verb] = message => message.AsCommand<CloseOptionTradeCommand>()!
    }.ToFrozenDictionary();
    static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap = new Dictionary<Type, Func<ICommand, List<ValidationError>>>
    {
        [typeof(CreateOptionTradeCommand)] = static command =>
        {
            var typed = (CreateOptionTradeCommand)command;
            return new List<ValidationError>().ValidateCommandId(typed.CommandId, typed.CommandName).ValidateTradeEntityId(typed.EntityId, typed.CommandName).ValidateEstablishedTradeCommand(typed, TradeAssetFamily.FuturesOption, null);
        },
        [typeof(AmendOptionTradeEvidenceCommand)] = static command =>
        {
            var typed = (AmendOptionTradeEvidenceCommand)command;
            return new List<ValidationError>().ValidateCommandId(typed.CommandId, typed.CommandName).ValidateTradeEntityId(typed.EntityId, typed.CommandName).ValidateEstablishedTradeCommand(typed, TradeAssetFamily.FuturesOption, null);
        },
        [typeof(BeginCloseOptionTradeCommand)] = static command =>
        {
            var typed = (BeginCloseOptionTradeCommand)command;
            return new List<ValidationError>().ValidateCommandId(typed.CommandId, typed.CommandName).ValidateTradeEntityId(typed.EntityId, typed.CommandName).ValidateEstablishedTradeCommand(typed, TradeAssetFamily.FuturesOption, null);
        },
        [typeof(CloseOptionTradeCommand)] = static command =>
        {
            var typed = (CloseOptionTradeCommand)command;
            return new List<ValidationError>().ValidateCommandId(typed.CommandId, typed.CommandName).ValidateTradeEntityId(typed.EntityId, typed.CommandName).ValidateEstablishedTradeCommand(typed, TradeAssetFamily.FuturesOption, null);
        },
    }.ToFrozenDictionary();
    static readonly IReadOnlyDictionary<Type, Func<ICommand, FuturesOptionTradeCommandState, ServiceResult<GuidResult>>> _receiveMap = new Dictionary<Type, Func<ICommand, FuturesOptionTradeCommandState, ServiceResult<GuidResult>>>
    {
        [typeof(CreateOptionTradeCommand)] = static (command, state) => ((CreateOptionTradeCommand)command).Execute(state),
        [typeof(AmendOptionTradeEvidenceCommand)] = static (command, state) => ((AmendOptionTradeEvidenceCommand)command).Execute(state),
        [typeof(BeginCloseOptionTradeCommand)] = static (command, state) => ((BeginCloseOptionTradeCommand)command).Execute(state),
        [typeof(CloseOptionTradeCommand)] = static (command, state) => ((CloseOptionTradeCommand)command).Execute(state)
    }.ToFrozenDictionary();
    protected override ValueTask OnStartup(ICommandActorContext<FuturesOptionTradeCommandActor> context) => _services.EventProjector.StartAsync(context);
    protected override ValueTask OnShutdown(ICommandActorContext<FuturesOptionTradeCommandActor> context) => _services.EventProjector.StopAsync();
    protected override ICommand ParseMessage(ICommandActorContext<FuturesOptionTradeCommandActor> context, IActorMessage message) => ParseMappedCommand(context, message, _parseMap);
    protected override ValueTask OnValidateAsync(ICommandActorContext<FuturesOptionTradeCommandActor> context, ActorThreadId threadId, ICommand command)
    {
        ValidateMappedCommand(command, _validationMap);
        return ValueTask.CompletedTask;
    }

    protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext<FuturesOptionTradeCommandActor> context, ActorThreadId threadId, ICommand command) => await (_services.StateRepository.LoadStateAsync(command)).ConfigureAwait(false);
    protected override async ValueTask OnSaveStateAsync(ICommandActorContext<FuturesOptionTradeCommandActor> context, ActorThreadId threadId, IActorState state, ICommand command) => await (_services.StateRepository.SaveStateAsync(context, (FuturesOptionTradeCommandState)state, command)).ConfigureAwait(false);
    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<FuturesOptionTradeCommandActor> context, IActorState state, ICommand command) => ValueTask.FromResult(ResolveMappedCommandHandler(command, _receiveMap)(command, (FuturesOptionTradeCommandState)state));
    protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(ICommandActorContext<FuturesOptionTradeCommandActor> context, ActorThreadId threadId, ICommand command, Exception ex)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(ex);
        return ValueTask.FromResult<ServiceResult<GuidResult>>(new ServiceFailed<GuidResult>(command.ErrorCode, ex.Message));
    }

    static IFuturesOptionTradeCommandContext Typed(ICommandActorContext<FuturesOptionTradeCommandActor> context) => context as IFuturesOptionTradeCommandContext ?? throw new ArgumentException("Typed Futures Option Trade context required.", nameof(context));
}
