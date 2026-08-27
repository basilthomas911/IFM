using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Command.Actor;

/// <summary>Processes EMA commands and persists their event-sourced state.</summary>
public sealed class FuturesEmaSignalCommandActor(ICommandActorContext<FuturesEmaSignalCommandActor> actorContext)
    : BaseEventSourceCommandActor<FuturesEmaSignalCommandActor>(actorContext,
        ((IFuturesEmaSignalCommandContext)actorContext).Logger)
{
    /// <summary>Gets the actor mailbox name.</summary>
    public const string ActorName = GenerateFuturesEmaSignalCommand.Actor;
    readonly IFuturesEmaSignalCommandContext typedContext = IsArgumentNull.Set(
        actorContext as IFuturesEmaSignalCommandContext, nameof(actorContext))!;
    IEventSourceActorStateRepository<FuturesEmaSignalCommandState> repository = default!;

    /// <inheritdoc />
    protected override async ValueTask OnStartup(ICommandActorContext<FuturesEmaSignalCommandActor> context)
    {
        repository = IsArgumentNull.Set(context.Container.Resolve<
            IEventSourceActorStateRepository<FuturesEmaSignalCommandState>>());
        await typedContext.EventProjector.StartAsync(context);
    }

    /// <inheritdoc />
    protected override async ValueTask OnShutdown(ICommandActorContext<FuturesEmaSignalCommandActor> context) =>
        await typedContext.EventProjector.StopAsync();

    /// <inheritdoc />
    protected override ICommand ParseMessage(ICommandActorContext<FuturesEmaSignalCommandActor> context, IActorMessage message)
    {
        if (message.Subject is not { ActorType: ActorType.Command, Name: ActorName } subject
            || !_parseMap.TryGetValue(subject.Verb, out var parseCommand))
            throw new InvalidOperationException($"Unable to resolve {ActorName} command from {message.Subject}.");
        return parseCommand.Invoke(message);
    }

    static readonly Dictionary<string, Func<IActorMessage, ICommand>> _parseMap = new()
    {
        [GenerateFuturesEmaSignalCommand.Verb] = message =>
            message.AsCommand<GenerateFuturesEmaSignalCommand>()
    };

    /// <inheritdoc />
    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(
        ICommandActorContext<FuturesEmaSignalCommandActor> context, IActorState state, ICommand command)
    {
        var commandName = command.GetType().Name;
        if (!_receiveMap.TryGetValue(commandName, out var receiveCommand))
            throw new InvalidOperationException($"Unsupported {ActorName} command {command.CommandName}.");
        return ValueTask.FromResult(receiveCommand.Invoke(
            command,
            context,
            (FuturesEmaSignalCommandState)state));
    }

    static readonly Dictionary<string, Func<ICommand, ICommandActorContext<FuturesEmaSignalCommandActor>,
        FuturesEmaSignalCommandState, ServiceResult<GuidResult>>> _receiveMap = new()
    {
        [typeof(GenerateFuturesEmaSignalCommand).Name] = static (command, _, state) =>
            ((GenerateFuturesEmaSignalCommand)command).Execute(state)
    };

    /// <inheritdoc />
    protected override ValueTask OnValidateAsync(ICommandActorContext<FuturesEmaSignalCommandActor> context,
        ActorThreadId threadId, ICommand command) => OnValidateAsync(context, threadId, command, CancellationToken.None);

    /// <inheritdoc />
    protected override async ValueTask OnValidateAsync(ICommandActorContext<FuturesEmaSignalCommandActor> context,
        ActorThreadId threadId, ICommand command, CancellationToken cancellationToken)
    {
        await typedContext.DbEventSource.InsertCommandLogAsync(command, DateTime.UtcNow,
            JsonConvert.SerializeObject(command), cancellationToken);
        var commandName = command.GetType().Name;
        if (!_validationMap.TryGetValue(commandName, out var validateCommand))
            throw new InvalidOperationException($"Unsupported {ActorName} command {command.CommandName}.");
        validateCommand.Invoke(command)
            .ThrowCommandValidationExceptionOnAnyError(command.ErrorCode);
    }

    static readonly Dictionary<string, Func<ICommand, List<ValidationError>>> _validationMap = new()
    {
        [typeof(GenerateFuturesEmaSignalCommand).Name] = static command =>
            command.CommandId == Guid.Empty
                ? [new ValidationError(nameof(command.CommandId), "CommandId is required")]
                : []
    };

    /// <inheritdoc />
    protected override async ValueTask<IActorState> OnLoadStateAsync(
        ICommandActorContext<FuturesEmaSignalCommandActor> context, ActorThreadId threadId, ICommand command) =>
        await repository.LoadStateAsync(command);
    /// <inheritdoc />
    protected override async ValueTask<IActorState> OnLoadStateAsync(
        ICommandActorContext<FuturesEmaSignalCommandActor> context, ActorThreadId threadId, ICommand command,
        CancellationToken cancellationToken) => await repository.LoadStateAsync(command, cancellationToken);
    /// <inheritdoc />
    protected override async ValueTask OnSaveStateAsync(ICommandActorContext<FuturesEmaSignalCommandActor> context,
        ActorThreadId threadId, IActorState state, ICommand command) =>
        await repository.SaveStateAsync(context, (FuturesEmaSignalCommandState)state, command);
    /// <inheritdoc />
    protected override async ValueTask OnSaveStateAsync(ICommandActorContext<FuturesEmaSignalCommandActor> context,
        ActorThreadId threadId, IActorState state, ICommand command, CancellationToken cancellationToken) =>
        await repository.SaveStateAsync(context, (FuturesEmaSignalCommandState)state, command, cancellationToken);

    /// <inheritdoc />
    protected override async ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(
        ICommandActorContext<FuturesEmaSignalCommandActor> context, ActorThreadId threadId,
        ICommand command, Exception exception)
    {
        var error = await exception.SendErrorEventAsync<
            TomasAI.IFM.Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(ErrorType.Command, context);
        return new ServiceFailed<GuidResult>(error);
    }
}
