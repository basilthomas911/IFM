using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Command.Actor;

/// <summary>Processes and persists exact futures-session VWAP transitions.</summary>
public sealed class FuturesVwapSignalCommandActor(
    ICommandActorContext<FuturesVwapSignalCommandActor> actorContext)
    : BaseEventSourceCommandActor<FuturesVwapSignalCommandActor>(actorContext,
        ((IFuturesVwapSignalCommandContext)actorContext).Logger)
{
    /// <summary>Identifies the VWAP Command mailbox.</summary>
    public const string ActorName = UpdateFuturesVwapSignalCommand.Actor;
    readonly IFuturesVwapSignalCommandContext typedContext = IsArgumentNull.Set(
        actorContext as IFuturesVwapSignalCommandContext, nameof(actorContext))!;

    /// <inheritdoc />
    protected override async ValueTask OnStartup(ICommandActorContext<FuturesVwapSignalCommandActor> context)
        => await typedContext.EventProjector.StartAsync(context);

    /// <inheritdoc />
    protected override async ValueTask OnShutdown(ICommandActorContext<FuturesVwapSignalCommandActor> context) =>
        await typedContext.EventProjector.StopAsync();

    /// <inheritdoc />
    protected override ICommand ParseMessage(
        ICommandActorContext<FuturesVwapSignalCommandActor> context,
        IActorMessage message)
        => ParseMappedCommand(context, message, _parseMap);

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> _parseMap = new Dictionary<string, Func<IActorMessage, ICommand>>()
    {
        [UpdateFuturesVwapSignalCommand.Verb] = message =>
            message.AsCommand<UpdateFuturesVwapSignalCommand>(),
        [RecoverFuturesVwapSignalCommand.Verb] = message =>
            message.AsCommand<RecoverFuturesVwapSignalCommand>()
    };

    /// <inheritdoc />
    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(
        ICommandActorContext<FuturesVwapSignalCommandActor> context,
        IActorState state, ICommand command)
    {
        var receiveCommand = ResolveMappedCommandHandler(command, _receiveMap);
        return ValueTask.FromResult(receiveCommand.Invoke(
            command,
            context,
            (FuturesVwapSignalCommandState)state));
    }

    static readonly IReadOnlyDictionary<Type, Func<ICommand, ICommandActorContext<FuturesVwapSignalCommandActor>,
        FuturesVwapSignalCommandState, ServiceResult<GuidResult>>> _receiveMap = new Dictionary<Type, Func<ICommand, ICommandActorContext<FuturesVwapSignalCommandActor>,
        FuturesVwapSignalCommandState, ServiceResult<GuidResult>>>()
    {
        [typeof(UpdateFuturesVwapSignalCommand)] = static (command, _, state) =>
            ((UpdateFuturesVwapSignalCommand)command).Execute(state),
        [typeof(RecoverFuturesVwapSignalCommand)] = static (command, _, state) =>
            ((RecoverFuturesVwapSignalCommand)command).Execute(state)
    };

    /// <inheritdoc />
    protected override ValueTask OnValidateAsync(ICommandActorContext<FuturesVwapSignalCommandActor> context,
        ActorThreadId threadId, ICommand command) => OnValidateAsync(context, threadId, command, CancellationToken.None);

    /// <inheritdoc />
    protected override ValueTask OnValidateAsync(ICommandActorContext<FuturesVwapSignalCommandActor> context,
        ActorThreadId threadId, ICommand command, CancellationToken cancellationToken)
    {
        ValidateMappedCommand(command, _validationMap);
        return ValueTask.CompletedTask;
    }

    static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>()
    {
        [typeof(UpdateFuturesVwapSignalCommand)] = static command =>
        {
            var update = (UpdateFuturesVwapSignalCommand)command;
            return new List<ValidationError>()
                .ValidateCommandId(update.CommandId, update.CommandName)
                .ValidateEntityId(update.EntityId, update.CommandName);
        },
        [typeof(RecoverFuturesVwapSignalCommand)] = static command =>
        {
            var recover = (RecoverFuturesVwapSignalCommand)command;
            return new List<ValidationError>()
                .ValidateCommandId(recover.CommandId, recover.CommandName)
                .ValidateEntityId(recover.EntityId, recover.CommandName);
        }
    };

    /// <inheritdoc />
    protected override async ValueTask<IActorState> OnLoadStateAsync(
        ICommandActorContext<FuturesVwapSignalCommandActor> context,
        ActorThreadId threadId, ICommand command) => await typedContext.StateRepository.LoadStateAsync(command);

    /// <inheritdoc />
    protected override async ValueTask<IActorState> OnLoadStateAsync(
        ICommandActorContext<FuturesVwapSignalCommandActor> context,
        ActorThreadId threadId, ICommand command, CancellationToken cancellationToken) =>
        await typedContext.StateRepository.LoadStateAsync(command, cancellationToken);

    /// <inheritdoc />
    protected override async ValueTask OnSaveStateAsync(
        ICommandActorContext<FuturesVwapSignalCommandActor> context,
        ActorThreadId threadId, IActorState state, ICommand command) =>
        await typedContext.StateRepository.SaveStateAsync(
            context, (FuturesVwapSignalCommandState)state, command);

    /// <inheritdoc />
    protected override async ValueTask OnSaveStateAsync(
        ICommandActorContext<FuturesVwapSignalCommandActor> context,
        ActorThreadId threadId, IActorState state, ICommand command,
        CancellationToken cancellationToken) =>
        await typedContext.StateRepository.SaveStateAsync(context, (FuturesVwapSignalCommandState)state,
            command, cancellationToken);

    /// <inheritdoc />
    protected override async ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(
        ICommandActorContext<FuturesVwapSignalCommandActor> context,
        ActorThreadId threadId, ICommand command, Exception exception)
    {
        var error = await exception.SendErrorEventAsync<
            TomasAI.IFM.Shared.EventModelActor.Events.CommandExceptionEvent,
            ActorEntityId>(ErrorType.Command, context);
        return new ServiceFailed<GuidResult>(error);
    }
}
