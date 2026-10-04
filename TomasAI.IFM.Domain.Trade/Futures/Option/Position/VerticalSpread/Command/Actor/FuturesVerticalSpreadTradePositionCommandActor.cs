using TomasAI.IFM.Domain.Trade.Futures.Command.Validation;
using System.Collections.Frozen;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Command;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Command.State;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Command.Actor;

public sealed class FuturesVerticalSpreadTradePositionCommandActor(
    ICommandActorContext<FuturesVerticalSpreadTradePositionCommandActor> context,
    InMemoryEventSourceActorOptions? options = null)
    : BaseInMemoryEventSourceCommandActor<FuturesVerticalSpreadTradePositionCommandActor, VerticalSpreadPositionCommandState>(
        context, Typed(context).Logger, options)
{
    public const string ActorName = PositionActorNames.VerticalSpreadCommand;
    readonly IVerticalSpreadPositionCommandContext services = Typed(context);

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> _parseMap =
        new Dictionary<string, Func<IActorMessage, ICommand>>(StringComparer.Ordinal)
        {
            [OpenVerticalSpreadPositionCommand.Verb] = message => message.AsCommand<OpenVerticalSpreadPositionCommand>()!,
            [UpdateVerticalSpreadPositionLegMarketPriceCommand.Verb] = message => message.AsCommand<UpdateVerticalSpreadPositionLegMarketPriceCommand>()!,
            [ChangeTradeLegDataCommand.Verb] = message => message.AsCommand<ChangeTradeLegDataCommand>()!,
            [EndOfDayVerticalSpreadPositionCommand.Verb] = message => message.AsCommand<EndOfDayVerticalSpreadPositionCommand>()!,
            [CloseVerticalSpreadPositionCommand.Verb] = message => message.AsCommand<CloseVerticalSpreadPositionCommand>()!,
            [CorrectVerticalSpreadPositionBasisCommand.Verb] = message => message.AsCommand<CorrectVerticalSpreadPositionBasisCommand>()!,
            [SnapshotVerticalSpreadPositionCommand.Verb] = message => message.AsCommand<SnapshotVerticalSpreadPositionCommand>()!
        }.ToFrozenDictionary(StringComparer.Ordinal);



    static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>
        {
            [typeof(OpenVerticalSpreadPositionCommand)] = static command =>
            {
                var typed = (OpenVerticalSpreadPositionCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateStrategyPositionId(typed.EntityId, typed.CommandName)
                    .ValidatePositionCommand(typed, TradeAssetFamily.FuturesOption, TradeStrategyKind.VerticalSpread);
            },
            [typeof(UpdateVerticalSpreadPositionLegMarketPriceCommand)] = static command =>
            {
                var typed = (UpdateVerticalSpreadPositionLegMarketPriceCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateStrategyPositionId(typed.EntityId, typed.CommandName)
                    .ValidatePositionCommand(typed, TradeAssetFamily.FuturesOption, TradeStrategyKind.VerticalSpread);
            },
            [typeof(ChangeTradeLegDataCommand)] = static command =>
            {
                var typed = (ChangeTradeLegDataCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateStrategyPositionId(typed.EntityId, typed.CommandName)
                    .ValidatePositionCommand(typed, TradeAssetFamily.FuturesOption, TradeStrategyKind.VerticalSpread);
            },
            [typeof(EndOfDayVerticalSpreadPositionCommand)] = static command =>
            {
                var typed = (EndOfDayVerticalSpreadPositionCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateStrategyPositionId(typed.EntityId, typed.CommandName)
                    .ValidatePositionCommand(typed, TradeAssetFamily.FuturesOption, TradeStrategyKind.VerticalSpread);
            },
            [typeof(CloseVerticalSpreadPositionCommand)] = static command =>
            {
                var typed = (CloseVerticalSpreadPositionCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateStrategyPositionId(typed.EntityId, typed.CommandName)
                    .ValidatePositionCommand(typed, TradeAssetFamily.FuturesOption, TradeStrategyKind.VerticalSpread);
            },
            [typeof(CorrectVerticalSpreadPositionBasisCommand)] = static command =>
            {
                var typed = (CorrectVerticalSpreadPositionBasisCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateStrategyPositionId(typed.EntityId, typed.CommandName)
                    .ValidatePositionCommand(typed, TradeAssetFamily.FuturesOption, TradeStrategyKind.VerticalSpread);
            },
            [typeof(SnapshotVerticalSpreadPositionCommand)] = static command =>
            {
                var typed = (SnapshotVerticalSpreadPositionCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateStrategyPositionId(typed.EntityId, typed.CommandName)
                    .ValidatePositionCommand(typed, TradeAssetFamily.FuturesOption, TradeStrategyKind.VerticalSpread);
            },
        }.ToFrozenDictionary();

    static readonly IReadOnlyDictionary<Type, Func<ICommand, VerticalSpreadPositionCommandState, ServiceResult<GuidResult>>> _receiveMap =
        new Dictionary<Type, Func<ICommand, VerticalSpreadPositionCommandState, ServiceResult<GuidResult>>>
        {
            [typeof(OpenVerticalSpreadPositionCommand)] = static (command, state) => ((OpenVerticalSpreadPositionCommand)command).Execute(state),
            [typeof(UpdateVerticalSpreadPositionLegMarketPriceCommand)] = static (command, state) => ((UpdateVerticalSpreadPositionLegMarketPriceCommand)command).Execute(state),
            [typeof(ChangeTradeLegDataCommand)] = static (command, state) => ((ChangeTradeLegDataCommand)command).Execute(state),
            [typeof(EndOfDayVerticalSpreadPositionCommand)] = static (command, state) => ((EndOfDayVerticalSpreadPositionCommand)command).Execute(state),
            [typeof(CloseVerticalSpreadPositionCommand)] = static (command, state) => ((CloseVerticalSpreadPositionCommand)command).Execute(state),
            [typeof(CorrectVerticalSpreadPositionBasisCommand)] = static (command, state) => ((CorrectVerticalSpreadPositionBasisCommand)command).Execute(state),
            [typeof(SnapshotVerticalSpreadPositionCommand)] = static (command, state) => ((SnapshotVerticalSpreadPositionCommand)command).Execute(state)
        }.ToFrozenDictionary();

    protected override bool IsResidentCommand(ICommand command) =>
        command is UpdateVerticalSpreadPositionLegMarketPriceCommand or ChangeTradeLegDataCommand;

    protected override bool IsResidentMessage(ActorSubject subject) =>
        subject.Verb is UpdateVerticalSpreadPositionLegMarketPriceCommand.Verb or ChangeTradeLegDataCommand.Verb;

    protected override ValueTask OnInMemoryStartupAsync(ICommandActorContext<FuturesVerticalSpreadTradePositionCommandActor> context) => services.EventProjector.StartAsync(context);
    protected override ValueTask OnInMemoryShutdownAsync(ICommandActorContext<FuturesVerticalSpreadTradePositionCommandActor> context) => services.EventProjector.StopAsync();
    protected override ICommand ParseMessage(ICommandActorContext<FuturesVerticalSpreadTradePositionCommandActor> context, IActorMessage message) => ParseMappedCommand(context, message, _parseMap);

    protected override ValueTask OnValidateAsync(ICommandActorContext<FuturesVerticalSpreadTradePositionCommandActor> context, ActorThreadId threadId, ICommand command)
    {
        ValidateMappedCommand(command, _validationMap);
        return ValueTask.CompletedTask;
    }

    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<FuturesVerticalSpreadTradePositionCommandActor> context, IActorState state, ICommand command) =>
        ValueTask.FromResult(ResolveMappedCommandHandler(command, _receiveMap)(command, (VerticalSpreadPositionCommandState)state));

    protected override ValueTask<VerticalSpreadPositionCommandState> LoadStateFromStoreAsync(ICommandActorContext<FuturesVerticalSpreadTradePositionCommandActor> context, ActorThreadId threadId, ICommand command, CancellationToken cancellationToken) => services.StateRepository.LoadStateAsync(command, cancellationToken);
    protected override ValueTask SaveStateToStoreAsync(ICommandActorContext<FuturesVerticalSpreadTradePositionCommandActor> context, ActorThreadId threadId, VerticalSpreadPositionCommandState state, ICommand command, CancellationToken cancellationToken) => services.StateRepository.SaveStateAsync(context, state, command, cancellationToken);
    protected override ValueTask PersistResidentEventsAsync(ICommandActorContext<FuturesVerticalSpreadTradePositionCommandActor> context, ICommand command, DomainEventCollection events, long expectedStreamVersion, CancellationToken cancellationToken) => services.StateRepository.SaveResidentEventsAsync(context, events, command, expectedStreamVersion, cancellationToken);
    protected override ValueTask<ServiceResult<GuidResult>> HandleCommandExceptionAsync(ICommandActorContext<FuturesVerticalSpreadTradePositionCommandActor> context, ActorThreadId threadId, ICommand command, Exception exception) => ValueTask.FromResult<ServiceResult<GuidResult>>(new ServiceFailed<GuidResult>(command.ErrorCode, exception.Message));

    static IVerticalSpreadPositionCommandContext Typed(ICommandActorContext<FuturesVerticalSpreadTradePositionCommandActor> context) =>
        context as IVerticalSpreadPositionCommandContext ?? throw new ArgumentException("Typed Vertical Spread position context required.");
}
