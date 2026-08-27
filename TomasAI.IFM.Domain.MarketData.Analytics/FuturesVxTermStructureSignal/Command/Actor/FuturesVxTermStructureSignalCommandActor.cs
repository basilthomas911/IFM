using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Command.Actor;

/// <summary>Processes and persists event-sourced VX curve updates.</summary>
public sealed class FuturesVxTermStructureSignalCommandActor(
    ICommandActorContext<FuturesVxTermStructureSignalCommandActor> actorContext)
    : BaseEventSourceCommandActor<FuturesVxTermStructureSignalCommandActor>(actorContext,
        ((IFuturesVxTermStructureSignalCommandContext)actorContext).Logger)
{
    public const string ActorName = UpdateFuturesVxTermStructureSignalCommand.Actor;
    readonly IFuturesVxTermStructureSignalCommandContext typedContext = IsArgumentNull.Set(
        actorContext as IFuturesVxTermStructureSignalCommandContext, nameof(actorContext))!;
    IEventSourceActorStateRepository<FuturesVxTermStructureSignalCommandState> repository = default!;

    /// <inheritdoc />
    protected override async ValueTask OnStartup(ICommandActorContext<FuturesVxTermStructureSignalCommandActor> context)
    {
        repository = IsArgumentNull.Set(context.Container.Resolve<
            IEventSourceActorStateRepository<FuturesVxTermStructureSignalCommandState>>());
        await typedContext.EventProjector.StartAsync(context);
    }
    /// <inheritdoc />
    protected override async ValueTask OnShutdown(ICommandActorContext<FuturesVxTermStructureSignalCommandActor> context) =>
        await typedContext.EventProjector.StopAsync();
    /// <inheritdoc />
    protected override ICommand ParseMessage(ICommandActorContext<FuturesVxTermStructureSignalCommandActor> context,
        IActorMessage message)
    {
        if (message.Subject is not { ActorType: ActorType.Command, Name: ActorName } subject
            || !_parseMap.TryGetValue(subject.Verb, out var parseCommand))
            throw new InvalidOperationException($"Unable to resolve {ActorName} command from {message.Subject}.");
        return parseCommand.Invoke(message);
    }

    static readonly Dictionary<string, Func<IActorMessage, ICommand>> _parseMap = new()
    {
        [UpdateFuturesVxTermStructureSignalCommand.Verb] = message =>
            message.AsCommand<UpdateFuturesVxTermStructureSignalCommand>()
    };
    /// <inheritdoc />
    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(
        ICommandActorContext<FuturesVxTermStructureSignalCommandActor> context,
        IActorState state, ICommand command)
    {
        var commandName = command.GetType().Name;
        if (!_receiveMap.TryGetValue(commandName, out var receiveCommand))
            throw new InvalidOperationException($"Unsupported {ActorName} command {command.CommandName}.");
        return ValueTask.FromResult(receiveCommand.Invoke(
            command,
            context,
            (FuturesVxTermStructureSignalCommandState)state));
    }

    static readonly Dictionary<string, Func<ICommand,
        ICommandActorContext<FuturesVxTermStructureSignalCommandActor>,
        FuturesVxTermStructureSignalCommandState, ServiceResult<GuidResult>>> _receiveMap = new()
    {
        [typeof(UpdateFuturesVxTermStructureSignalCommand).Name] = static (command, _, state) =>
            ((UpdateFuturesVxTermStructureSignalCommand)command).Execute(state)
    };
    /// <inheritdoc />
    protected override ValueTask OnValidateAsync(ICommandActorContext<FuturesVxTermStructureSignalCommandActor> context,
        ActorThreadId threadId, ICommand command) => OnValidateAsync(context, threadId, command, CancellationToken.None);
    /// <inheritdoc />
    protected override async ValueTask OnValidateAsync(ICommandActorContext<FuturesVxTermStructureSignalCommandActor> context,
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
        [typeof(UpdateFuturesVxTermStructureSignalCommand).Name] = static command =>
            command.CommandId == Guid.Empty
                ? [new ValidationError(nameof(command.CommandId), "CommandId is required")]
                : []
    };
    /// <inheritdoc />
    protected override async ValueTask<IActorState> OnLoadStateAsync(
        ICommandActorContext<FuturesVxTermStructureSignalCommandActor> context,
        ActorThreadId threadId, ICommand command) => await repository.LoadStateAsync(command);
    /// <inheritdoc />
    protected override async ValueTask<IActorState> OnLoadStateAsync(
        ICommandActorContext<FuturesVxTermStructureSignalCommandActor> context,
        ActorThreadId threadId, ICommand command, CancellationToken cancellationToken) =>
        await repository.LoadStateAsync(command, cancellationToken);
    /// <inheritdoc />
    protected override async ValueTask OnSaveStateAsync(
        ICommandActorContext<FuturesVxTermStructureSignalCommandActor> context,
        ActorThreadId threadId, IActorState state, ICommand command) =>
        await repository.SaveStateAsync(context, (FuturesVxTermStructureSignalCommandState)state, command);
    /// <inheritdoc />
    protected override async ValueTask OnSaveStateAsync(
        ICommandActorContext<FuturesVxTermStructureSignalCommandActor> context,
        ActorThreadId threadId, IActorState state, ICommand command,
        CancellationToken cancellationToken) =>
        await repository.SaveStateAsync(context, (FuturesVxTermStructureSignalCommandState)state,
            command, cancellationToken);
    /// <inheritdoc />
    protected override async ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(
        ICommandActorContext<FuturesVxTermStructureSignalCommandActor> context,
        ActorThreadId threadId, ICommand command, Exception exception)
    {
        var error = await exception.SendErrorEventAsync<
            TomasAI.IFM.Shared.EventModelActor.Events.CommandExceptionEvent,
            ActorEntityId>(ErrorType.Command, context);
        return new ServiceFailed<GuidResult>(error);
    }
}
