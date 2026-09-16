using System.Collections.Frozen;
using TomasAI.IFM.Domain.Trade.Futures.Command.State;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Futures.Command.Actor;

public sealed class FuturesTradeCommandActor(
    ICommandActorContext<FuturesTradeCommandActor> context)
    : BaseEventSourceCommandActor<FuturesTradeCommandActor>(
        context,
        Typed(context).Logger)
{
    public const string ActorName = FuturesTradeActorNames.Command;

    readonly IFuturesTradeCommandContext _services = Typed(context);

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> _parseMap =
        new Dictionary<string, Func<IActorMessage, ICommand>>(StringComparer.Ordinal)
        {
            [CreateFuturesTradeCommand.Verb] = message =>
                message.AsCommand<CreateFuturesTradeCommand>()!,
            [AmendFuturesTradeEvidenceCommand.Verb] = message =>
                message.AsCommand<AmendFuturesTradeEvidenceCommand>()!,
            [BeginCloseFuturesTradeCommand.Verb] = message =>
                message.AsCommand<BeginCloseFuturesTradeCommand>()!,
            [CloseFuturesTradeCommand.Verb] = message =>
                message.AsCommand<CloseFuturesTradeCommand>()!
        }.ToFrozenDictionary(StringComparer.Ordinal);



    static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>
        {
            [typeof(CreateFuturesTradeCommand)] = Validate,
            [typeof(AmendFuturesTradeEvidenceCommand)] = Validate,
            [typeof(BeginCloseFuturesTradeCommand)] = Validate,
            [typeof(CloseFuturesTradeCommand)] = Validate,
        }.ToFrozenDictionary();

    static readonly IReadOnlyDictionary<Type,
        Func<ICommand, FuturesTradeCommandState, ServiceResult<GuidResult>>> _receiveMap =
        new Dictionary<Type,
            Func<ICommand, FuturesTradeCommandState, ServiceResult<GuidResult>>>
        {
            [typeof(CreateFuturesTradeCommand)] = static (command, state) =>
                ((CreateFuturesTradeCommand)command).Execute(state),
            [typeof(AmendFuturesTradeEvidenceCommand)] = static (command, state) =>
                ((AmendFuturesTradeEvidenceCommand)command).Execute(state),
            [typeof(BeginCloseFuturesTradeCommand)] = static (command, state) =>
                ((BeginCloseFuturesTradeCommand)command).Execute(state),
            [typeof(CloseFuturesTradeCommand)] = static (command, state) =>
                ((CloseFuturesTradeCommand)command).Execute(state)
        }.ToFrozenDictionary();

    protected override ValueTask OnStartup(
        ICommandActorContext<FuturesTradeCommandActor> context) =>
        _services.EventProjector.StartAsync(context);

    protected override ValueTask OnShutdown(
        ICommandActorContext<FuturesTradeCommandActor> context) =>
        _services.EventProjector.StopAsync();

    protected override ICommand ParseMessage(
        ICommandActorContext<FuturesTradeCommandActor> context,
        IActorMessage message) =>
        ParseMappedCommand(context, message, _parseMap);

    protected override ValueTask OnValidateAsync(
        ICommandActorContext<FuturesTradeCommandActor> context,
        ActorThreadId actorThreadId,
        ICommand command)
    {
        ValidateMappedCommand(command, _validationMap);
        return ValueTask.CompletedTask;
    }

    protected override async ValueTask<IActorState> OnLoadStateAsync(
        ICommandActorContext<FuturesTradeCommandActor> context,
        ActorThreadId actorThreadId,
        ICommand command) =>
        await _services.StateRepository.LoadStateAsync(command);

    protected override async ValueTask OnSaveStateAsync(
        ICommandActorContext<FuturesTradeCommandActor> context,
        ActorThreadId actorThreadId,
        IActorState state,
        ICommand command) =>
        await _services.StateRepository.SaveStateAsync(
            context,
            (FuturesTradeCommandState)state,
            command);

    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(
        ICommandActorContext<FuturesTradeCommandActor> context,
        IActorState state,
        ICommand command) =>
        ValueTask.FromResult(
            ResolveMappedCommandHandler(command, _receiveMap)(
                command,
                (FuturesTradeCommandState)state));

    protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(
        ICommandActorContext<FuturesTradeCommandActor> context,
        ActorThreadId actorThreadId,
        ICommand command,
        Exception exception) =>
        ValueTask.FromResult<ServiceResult<GuidResult>>(
            new ServiceFailed<GuidResult>(command.ErrorCode, exception.Message));

    static List<ValidationError> Validate(ICommand command) =>
        new List<ValidationError>()
            .ValidateCommandId(command.CommandId, command.CommandName)
            .CaptureCommandValidation(() =>
            {
                if (command is not ICommand<TradeEntityId> typedCommand ||
                    !typedCommand.EntityId.IsValid ||
                    command.Subject.EntityId != typedCommand.EntityId.Format() ||
                    command is CreateFuturesTradeCommand
                    {
                        Trade.AssetFamily: not TradeAssetFamily.Futures
                    })
                {
                    throw new ArgumentException(
                        "Valid Futures Trade identity, type, and subject are required.");
                }
            });

    static IFuturesTradeCommandContext Typed(
        ICommandActorContext<FuturesTradeCommandActor> context) =>
        context as IFuturesTradeCommandContext
        ?? throw new ArgumentException("Typed Futures Trade context required.");
}
