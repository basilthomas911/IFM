using Newtonsoft.Json;
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
    protected override ICommand ParseMessage(ICommandActorContext<FuturesVwapSignalCommandActor> context,
        IActorMessage message)
    {
        if (message.Subject is not { ActorType: ActorType.Command, Name: ActorName } subject
            || !_parseMap.TryGetValue(subject.Verb, out var parseCommand))
            throw new InvalidOperationException($"Unable to resolve {ActorName} command from {message.Subject}.");
        return parseCommand.Invoke(message);
    }

    static readonly Dictionary<string, Func<IActorMessage, ICommand>> _parseMap = new()
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
        var commandName = command.GetType().Name;
        if (!_receiveMap.TryGetValue(commandName, out var receiveCommand))
            throw new InvalidOperationException($"Unsupported VWAP command {command.CommandName}.");
        return ValueTask.FromResult(receiveCommand.Invoke(
            command,
            context,
            (FuturesVwapSignalCommandState)state));
    }

    static readonly Dictionary<string, Func<ICommand, ICommandActorContext<FuturesVwapSignalCommandActor>,
        FuturesVwapSignalCommandState, ServiceResult<GuidResult>>> _receiveMap = new()
    {
        [typeof(UpdateFuturesVwapSignalCommand).Name] = static (command, _, state) =>
            ((UpdateFuturesVwapSignalCommand)command).Execute(state),
        [typeof(RecoverFuturesVwapSignalCommand).Name] = static (command, _, state) =>
            ((RecoverFuturesVwapSignalCommand)command).Execute(state)
    };

    /// <inheritdoc />
    protected override ValueTask OnValidateAsync(ICommandActorContext<FuturesVwapSignalCommandActor> context,
        ActorThreadId threadId, ICommand command) => OnValidateAsync(context, threadId, command, CancellationToken.None);

    /// <inheritdoc />
    protected override async ValueTask OnValidateAsync(ICommandActorContext<FuturesVwapSignalCommandActor> context,
        ActorThreadId threadId, ICommand command, CancellationToken cancellationToken)
    {
        await typedContext.DbEventSource.InsertCommandLogAsync(command, DateTime.UtcNow,
            JsonConvert.SerializeObject(command), cancellationToken);
        var commandName = command.GetType().Name;
        if (!_validationMap.TryGetValue(commandName, out var validateCommand))
            throw new InvalidOperationException($"Unsupported VWAP command {command.CommandName}.");
        validateCommand.Invoke(command)
            .ThrowCommandValidationExceptionOnAnyError(command.ErrorCode);
    }

    static readonly Dictionary<string, Func<ICommand, List<ValidationError>>> _validationMap = new()
    {
        [typeof(UpdateFuturesVwapSignalCommand).Name] = ValidateCommand,
        [typeof(RecoverFuturesVwapSignalCommand).Name] = ValidateCommand
    };

    static List<ValidationError> ValidateCommand(ICommand command) =>
        command.CommandId == Guid.Empty
            ? [new ValidationError(nameof(command.CommandId), "CommandId is required")]
            : [];

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
