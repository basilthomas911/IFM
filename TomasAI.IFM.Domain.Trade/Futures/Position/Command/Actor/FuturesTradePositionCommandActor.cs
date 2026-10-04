using TomasAI.IFM.Domain.Trade.Futures.Command.Validation;
using System.Collections.Frozen;
using TomasAI.IFM.Domain.Trade.Futures.Position.Command.State;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Position;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Command.Actor;

public sealed class FuturesTradePositionCommandActor(
    ICommandActorContext<FuturesTradePositionCommandActor> context,
    InMemoryEventSourceActorOptions? options = null)
    : BaseInMemoryEventSourceCommandActor<FuturesTradePositionCommandActor, FuturesPositionCommandState>(
        context, Typed(context).Logger, options)
{
    public const string ActorName = FuturesPositionActorNames.Command;
    readonly IFuturesPositionCommandContext services = Typed(context);

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> _parseMap =
        new Dictionary<string, Func<IActorMessage, ICommand>>(StringComparer.Ordinal)
        {
            [OpenFuturesPositionCommand.Verb] = message => message.AsCommand<OpenFuturesPositionCommand>()!,
            [UpdateFuturesPositionMarketPriceCommand.Verb] = message => message.AsCommand<UpdateFuturesPositionMarketPriceCommand>()!,
            [ChangeTradeLegDataCommand.Verb] = message => message.AsCommand<ChangeTradeLegDataCommand>()!,
            [EndOfDayFuturesPositionCommand.Verb] = message => message.AsCommand<EndOfDayFuturesPositionCommand>()!,
            [CloseFuturesPositionCommand.Verb] = message => message.AsCommand<CloseFuturesPositionCommand>()!,
            [CorrectFuturesPositionBasisCommand.Verb] = message => message.AsCommand<CorrectFuturesPositionBasisCommand>()!,
            [SnapshotFuturesPositionCommand.Verb] = message => message.AsCommand<SnapshotFuturesPositionCommand>()!
        }.ToFrozenDictionary(StringComparer.Ordinal);



    static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>
        {
            [typeof(OpenFuturesPositionCommand)] = static command =>
            {
                var typed = (OpenFuturesPositionCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateStrategyPositionId(typed.EntityId, typed.CommandName)
                    .ValidatePositionCommand(typed, TradeAssetFamily.Futures, TradeStrategyKind.FuturesOutright);
            },
            [typeof(UpdateFuturesPositionMarketPriceCommand)] = static command =>
            {
                var typed = (UpdateFuturesPositionMarketPriceCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateStrategyPositionId(typed.EntityId, typed.CommandName)
                    .ValidatePositionCommand(typed, TradeAssetFamily.Futures, TradeStrategyKind.FuturesOutright);
            },
            [typeof(ChangeTradeLegDataCommand)] = static command =>
            {
                var typed = (ChangeTradeLegDataCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateStrategyPositionId(typed.EntityId, typed.CommandName)
                    .ValidatePositionCommand(typed, TradeAssetFamily.Futures, TradeStrategyKind.FuturesOutright);
            },
            [typeof(EndOfDayFuturesPositionCommand)] = static command =>
            {
                var typed = (EndOfDayFuturesPositionCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateStrategyPositionId(typed.EntityId, typed.CommandName)
                    .ValidatePositionCommand(typed, TradeAssetFamily.Futures, TradeStrategyKind.FuturesOutright);
            },
            [typeof(CloseFuturesPositionCommand)] = static command =>
            {
                var typed = (CloseFuturesPositionCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateStrategyPositionId(typed.EntityId, typed.CommandName)
                    .ValidatePositionCommand(typed, TradeAssetFamily.Futures, TradeStrategyKind.FuturesOutright);
            },
            [typeof(CorrectFuturesPositionBasisCommand)] = static command =>
            {
                var typed = (CorrectFuturesPositionBasisCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateStrategyPositionId(typed.EntityId, typed.CommandName)
                    .ValidatePositionCommand(typed, TradeAssetFamily.Futures, TradeStrategyKind.FuturesOutright);
            },
            [typeof(SnapshotFuturesPositionCommand)] = static command =>
            {
                var typed = (SnapshotFuturesPositionCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateStrategyPositionId(typed.EntityId, typed.CommandName)
                    .ValidatePositionCommand(typed, TradeAssetFamily.Futures, TradeStrategyKind.FuturesOutright);
            },
        }.ToFrozenDictionary();

    static readonly IReadOnlyDictionary<Type,
        Func<ICommand, FuturesPositionCommandState, ServiceResult<GuidResult>>> _receiveMap =
        new Dictionary<Type, Func<ICommand, FuturesPositionCommandState, ServiceResult<GuidResult>>>
        {
            [typeof(OpenFuturesPositionCommand)] = static (command, state) =>
                ((OpenFuturesPositionCommand)command).Execute(state),
            [typeof(UpdateFuturesPositionMarketPriceCommand)] = static (command, state) =>
                ((UpdateFuturesPositionMarketPriceCommand)command).Execute(state),
            [typeof(ChangeTradeLegDataCommand)] = static (command, state) =>
                ((ChangeTradeLegDataCommand)command).Execute(state),
            [typeof(EndOfDayFuturesPositionCommand)] = static (command, state) =>
                ((EndOfDayFuturesPositionCommand)command).Execute(state),
            [typeof(CloseFuturesPositionCommand)] = static (command, state) =>
                ((CloseFuturesPositionCommand)command).Execute(state),
            [typeof(CorrectFuturesPositionBasisCommand)] = static (command, state) =>
                ((CorrectFuturesPositionBasisCommand)command).Execute(state),
            [typeof(SnapshotFuturesPositionCommand)] = static (command, state) =>
                ((SnapshotFuturesPositionCommand)command).Execute(state)
        }.ToFrozenDictionary();

    protected override bool IsResidentCommand(ICommand command) =>
        command is UpdateFuturesPositionMarketPriceCommand or ChangeTradeLegDataCommand;

    protected override bool IsResidentMessage(ActorSubject subject) =>
        subject.Verb is UpdateFuturesPositionMarketPriceCommand.Verb or ChangeTradeLegDataCommand.Verb;

    protected override ValueTask OnInMemoryStartupAsync(
        ICommandActorContext<FuturesTradePositionCommandActor> context) =>
        services.EventProjector.StartAsync(context);

    protected override ValueTask OnInMemoryShutdownAsync(
        ICommandActorContext<FuturesTradePositionCommandActor> context) =>
        services.EventProjector.StopAsync();

    protected override ICommand ParseMessage(
        ICommandActorContext<FuturesTradePositionCommandActor> context,
        IActorMessage message) => ParseMappedCommand(context, message, _parseMap);

    protected override ValueTask OnValidateAsync(
        ICommandActorContext<FuturesTradePositionCommandActor> context,
        ActorThreadId threadId,
        ICommand command)
    {
        ValidateMappedCommand(command, _validationMap);
        return ValueTask.CompletedTask;
    }

    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(
        ICommandActorContext<FuturesTradePositionCommandActor> context,
        IActorState state,
        ICommand command) => ValueTask.FromResult(
            ResolveMappedCommandHandler(command, _receiveMap)(command, (FuturesPositionCommandState)state));

    protected override ValueTask<FuturesPositionCommandState> LoadStateFromStoreAsync(
        ICommandActorContext<FuturesTradePositionCommandActor> context,
        ActorThreadId threadId,
        ICommand command,
        CancellationToken cancellationToken) =>
        services.StateRepository.LoadStateAsync(command, cancellationToken);

    protected override ValueTask SaveStateToStoreAsync(
        ICommandActorContext<FuturesTradePositionCommandActor> context,
        ActorThreadId threadId,
        FuturesPositionCommandState state,
        ICommand command,
        CancellationToken cancellationToken) =>
        services.StateRepository.SaveStateAsync(context, state, command, cancellationToken);

    protected override ValueTask PersistResidentEventsAsync(
        ICommandActorContext<FuturesTradePositionCommandActor> context,
        ICommand command,
        DomainEventCollection events,
        long expectedStreamVersion,
        CancellationToken cancellationToken) => services.StateRepository.SaveResidentEventsAsync(
            context, events, command, expectedStreamVersion, cancellationToken);

    protected override ValueTask<ServiceResult<GuidResult>> HandleCommandExceptionAsync(
        ICommandActorContext<FuturesTradePositionCommandActor> context,
        ActorThreadId threadId,
        ICommand command,
        Exception exception) => ValueTask.FromResult<ServiceResult<GuidResult>>(
            new ServiceFailed<GuidResult>(command.ErrorCode, exception.Message));

    static IFuturesPositionCommandContext Typed(
        ICommandActorContext<FuturesTradePositionCommandActor> context) =>
        context as IFuturesPositionCommandContext ??
        throw new ArgumentException("Typed Futures position context required.");
}
