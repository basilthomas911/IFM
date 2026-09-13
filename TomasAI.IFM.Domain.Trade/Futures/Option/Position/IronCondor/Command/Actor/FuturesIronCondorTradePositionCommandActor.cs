using System.Collections.Frozen;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.State;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.Actor;

public sealed class FuturesIronCondorTradePositionCommandActor(
    ICommandActorContext<FuturesIronCondorTradePositionCommandActor> context,
    InMemoryEventSourceActorOptions? options = null)
    : BaseInMemoryEventSourceCommandActor<FuturesIronCondorTradePositionCommandActor, IronCondorPositionCommandState>(
        context, Typed(context).Logger, options)
{
    public const string ActorName = PositionActorNames.IronCondorCommand;
    readonly IIronCondorPositionCommandContext services = Typed(context);

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> ParseMap =
        new Dictionary<string, Func<IActorMessage, ICommand>>(StringComparer.Ordinal)
        {
            [OpenIronCondorPositionCommand.Verb] = message => message.AsCommand<OpenIronCondorPositionCommand>()!,
            [UpdateIronCondorPositionLegMarketPriceCommand.Verb] = message => message.AsCommand<UpdateIronCondorPositionLegMarketPriceCommand>()!,
            [ChangeTradeLegDataCommand.Verb] = message => message.AsCommand<ChangeTradeLegDataCommand>()!,
            [EndOfDayIronCondorPositionCommand.Verb] = message => message.AsCommand<EndOfDayIronCondorPositionCommand>()!,
            [CloseIronCondorPositionCommand.Verb] = message => message.AsCommand<CloseIronCondorPositionCommand>()!,
            [CorrectIronCondorPositionBasisCommand.Verb] = message => message.AsCommand<CorrectIronCondorPositionBasisCommand>()!,
            [SnapshotIronCondorPositionCommand.Verb] = message => message.AsCommand<SnapshotIronCondorPositionCommand>()!
        }.ToFrozenDictionary(StringComparer.Ordinal);

    static readonly Type[] CommandTypes =
    [
        typeof(OpenIronCondorPositionCommand), typeof(UpdateIronCondorPositionLegMarketPriceCommand),
        typeof(ChangeTradeLegDataCommand), typeof(EndOfDayIronCondorPositionCommand),
        typeof(CloseIronCondorPositionCommand), typeof(CorrectIronCondorPositionBasisCommand),
        typeof(SnapshotIronCondorPositionCommand)
    ];

    static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> ValidationMap =
        CommandTypes.ToDictionary(type => type, _ => (Func<ICommand, List<ValidationError>>)Validate).ToFrozenDictionary();

    static readonly IReadOnlyDictionary<Type, Func<ICommand, IronCondorPositionCommandState, ServiceResult<GuidResult>>> ReceiveMap =
        new Dictionary<Type, Func<ICommand, IronCondorPositionCommandState, ServiceResult<GuidResult>>>
        {
            [typeof(OpenIronCondorPositionCommand)] = static (command, state) => ((OpenIronCondorPositionCommand)command).Execute(state),
            [typeof(UpdateIronCondorPositionLegMarketPriceCommand)] = static (command, state) => ((UpdateIronCondorPositionLegMarketPriceCommand)command).Execute(state),
            [typeof(ChangeTradeLegDataCommand)] = static (command, state) => ((ChangeTradeLegDataCommand)command).Execute(state),
            [typeof(EndOfDayIronCondorPositionCommand)] = static (command, state) => ((EndOfDayIronCondorPositionCommand)command).Execute(state),
            [typeof(CloseIronCondorPositionCommand)] = static (command, state) => ((CloseIronCondorPositionCommand)command).Execute(state),
            [typeof(CorrectIronCondorPositionBasisCommand)] = static (command, state) => ((CorrectIronCondorPositionBasisCommand)command).Execute(state),
            [typeof(SnapshotIronCondorPositionCommand)] = static (command, state) => ((SnapshotIronCondorPositionCommand)command).Execute(state)
        }.ToFrozenDictionary();

    protected override bool IsResidentCommand(ICommand command) =>
        command is UpdateIronCondorPositionLegMarketPriceCommand or ChangeTradeLegDataCommand;

    protected override bool IsResidentMessage(ActorSubject subject) =>
        subject.Verb is UpdateIronCondorPositionLegMarketPriceCommand.Verb or ChangeTradeLegDataCommand.Verb;

    protected override ValueTask OnInMemoryStartupAsync(ICommandActorContext<FuturesIronCondorTradePositionCommandActor> context) => services.EventProjector.StartAsync(context);
    protected override ValueTask OnInMemoryShutdownAsync(ICommandActorContext<FuturesIronCondorTradePositionCommandActor> context) => services.EventProjector.StopAsync();
    protected override ICommand ParseMessage(ICommandActorContext<FuturesIronCondorTradePositionCommandActor> context, IActorMessage message) => ParseMappedCommand(context, message, ParseMap);

    protected override ValueTask OnValidateAsync(ICommandActorContext<FuturesIronCondorTradePositionCommandActor> context, ActorThreadId threadId, ICommand command)
    {
        ValidateMappedCommand(command, ValidationMap);
        return ValueTask.CompletedTask;
    }

    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<FuturesIronCondorTradePositionCommandActor> context, IActorState state, ICommand command) =>
        ValueTask.FromResult(ResolveMappedCommandHandler(command, ReceiveMap)(command, (IronCondorPositionCommandState)state));

    protected override ValueTask<IronCondorPositionCommandState> LoadStateFromStoreAsync(ICommandActorContext<FuturesIronCondorTradePositionCommandActor> context, ActorThreadId threadId, ICommand command, CancellationToken cancellationToken) => services.StateRepository.LoadStateAsync(command, cancellationToken);
    protected override ValueTask SaveStateToStoreAsync(ICommandActorContext<FuturesIronCondorTradePositionCommandActor> context, ActorThreadId threadId, IronCondorPositionCommandState state, ICommand command, CancellationToken cancellationToken) => services.StateRepository.SaveStateAsync(context, state, command, cancellationToken);
    protected override ValueTask PersistResidentEventsAsync(ICommandActorContext<FuturesIronCondorTradePositionCommandActor> context, ICommand command, DomainEventCollection events, long expectedStreamVersion, CancellationToken cancellationToken) => services.StateRepository.SaveResidentEventsAsync(context, events, command, expectedStreamVersion, cancellationToken);
    protected override ValueTask<ServiceResult<GuidResult>> HandleCommandExceptionAsync(ICommandActorContext<FuturesIronCondorTradePositionCommandActor> context, ActorThreadId threadId, ICommand command, Exception exception) => ValueTask.FromResult<ServiceResult<GuidResult>>(new ServiceFailed<GuidResult>(command.ErrorCode, exception.Message));

    static List<ValidationError> Validate(ICommand command) =>
        new List<ValidationError>().ValidateCommandId(command.CommandId, command.CommandName).CaptureCommandValidation(() =>
        {
            if (command is not ICommand<StrategyPositionId> typed || !typed.EntityId.IsValid ||
                command.Subject.EntityId != typed.EntityId.Format() ||
                command is OpenIronCondorPositionCommand { Trade.StrategyKind: not TradeStrategyKind.IronCondor } ||
                command is ChangeTradeLegDataCommand routed &&
                (routed.TradeType != TradeStrategyKind.IronCondor || string.IsNullOrWhiteSpace(routed.ContractId)))
                throw new ArgumentException("Valid Iron Condor position identity, type, ContractId, and subject are required.");
        });

    static IIronCondorPositionCommandContext Typed(ICommandActorContext<FuturesIronCondorTradePositionCommandActor> context) =>
        context as IIronCondorPositionCommandContext ?? throw new ArgumentException("Typed Iron Condor position context required.");
}
