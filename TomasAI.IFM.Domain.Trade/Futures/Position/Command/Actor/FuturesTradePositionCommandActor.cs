using System.Collections.Frozen;
using TomasAI.IFM.Domain.Trade.Futures.Position.Command.Extensions;
using TomasAI.IFM.Domain.Trade.Futures.Position.Command.State;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Position;
using TomasAI.IFM.Domain.Trade.Shared.Model;
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

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> ParseMap =
        new Dictionary<string, Func<IActorMessage, ICommand>>(StringComparer.Ordinal)
        {
            [OpenFuturesPositionCommand.Verb] = message => message.AsCommand<OpenFuturesPositionCommand>()!,
            [UpdateFuturesPositionMarketPriceCommand.Verb] = message => message.AsCommand<UpdateFuturesPositionMarketPriceCommand>()!,
            [EndOfDayFuturesPositionCommand.Verb] = message => message.AsCommand<EndOfDayFuturesPositionCommand>()!,
            [CloseFuturesPositionCommand.Verb] = message => message.AsCommand<CloseFuturesPositionCommand>()!,
            [CorrectFuturesPositionBasisCommand.Verb] = message => message.AsCommand<CorrectFuturesPositionBasisCommand>()!,
            [SnapshotFuturesPositionCommand.Verb] = message => message.AsCommand<SnapshotFuturesPositionCommand>()!
        }.ToFrozenDictionary(StringComparer.Ordinal);

    static readonly Type[] CommandTypes =
    [
        typeof(OpenFuturesPositionCommand), typeof(UpdateFuturesPositionMarketPriceCommand),
        typeof(EndOfDayFuturesPositionCommand), typeof(CloseFuturesPositionCommand),
        typeof(CorrectFuturesPositionBasisCommand), typeof(SnapshotFuturesPositionCommand)
    ];

    static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> ValidationMap =
        CommandTypes.ToDictionary(type => type, _ => (Func<ICommand, List<ValidationError>>)Validate)
            .ToFrozenDictionary();

    static readonly IReadOnlyDictionary<Type,
        Func<ICommand, FuturesPositionCommandState, ServiceResult<GuidResult>>> ReceiveMap =
        new Dictionary<Type, Func<ICommand, FuturesPositionCommandState, ServiceResult<GuidResult>>>
        {
            [typeof(OpenFuturesPositionCommand)] = static (command, state) =>
                ((OpenFuturesPositionCommand)command).Execute(state),
            [typeof(UpdateFuturesPositionMarketPriceCommand)] = static (command, state) =>
                ((UpdateFuturesPositionMarketPriceCommand)command).Execute(state),
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
        command is UpdateFuturesPositionMarketPriceCommand;

    protected override bool IsResidentMessage(ActorSubject subject) =>
        subject.Verb == UpdateFuturesPositionMarketPriceCommand.Verb;

    protected override ValueTask OnInMemoryStartupAsync(
        ICommandActorContext<FuturesTradePositionCommandActor> context) =>
        services.EventProjector.StartAsync(context);

    protected override ValueTask OnInMemoryShutdownAsync(
        ICommandActorContext<FuturesTradePositionCommandActor> context) =>
        services.EventProjector.StopAsync();

    protected override ICommand ParseMessage(
        ICommandActorContext<FuturesTradePositionCommandActor> context,
        IActorMessage message) => ParseMappedCommand(context, message, ParseMap);

    protected override ValueTask OnValidateAsync(
        ICommandActorContext<FuturesTradePositionCommandActor> context,
        ActorThreadId threadId,
        ICommand command)
    {
        ValidateMappedCommand(command, ValidationMap);
        return ValueTask.CompletedTask;
    }

    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(
        ICommandActorContext<FuturesTradePositionCommandActor> context,
        IActorState state,
        ICommand command) => ValueTask.FromResult(
            ResolveMappedCommandHandler(command, ReceiveMap)(command, (FuturesPositionCommandState)state));

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

    static List<ValidationError> Validate(ICommand command) =>
        new List<ValidationError>().ValidateCommandId(command.CommandId, command.CommandName)
            .CaptureCommandValidation(() =>
            {
                if (command is not ICommand<StrategyPositionId> typed ||
                    !typed.EntityId.IsValid ||
                    command.Subject.EntityId != typed.EntityId.Format() ||
                    command is OpenFuturesPositionCommand open &&
                    (open.Trade.AssetFamily != TradeAssetFamily.Futures ||
                     open.Trade.StrategyKind != TradeStrategyKind.FuturesOutright))
                    throw new ArgumentException("Valid one-leg Futures position identity, type, and subject are required.");
            });

    static IFuturesPositionCommandContext Typed(
        ICommandActorContext<FuturesTradePositionCommandActor> context) =>
        context as IFuturesPositionCommandContext ??
        throw new ArgumentException("Typed Futures position context required.");
}
