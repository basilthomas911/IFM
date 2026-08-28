using TomasAI.IFM.Domain.MarketData.Analytics.FuturesBbSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesBbSignal.Command.Actor;

/// <summary>Processes Bollinger commands and persists their event-sourced state.</summary>
public sealed class FuturesBbSignalCommandActor(ICommandActorContext<FuturesBbSignalCommandActor> actorContext)
    : BaseEventSourceCommandActor<FuturesBbSignalCommandActor>(actorContext,
        ((IFuturesBbSignalCommandContext)actorContext).Logger)
{
    /// <summary>Gets the actor mailbox name.</summary>
    public const string ActorName = GenerateFuturesBbSignalCommand.Actor;
    readonly IFuturesBbSignalCommandContext typedContext = IsArgumentNull.Set(
        actorContext as IFuturesBbSignalCommandContext, nameof(actorContext))!;
    IEventSourceActorStateRepository<FuturesBbSignalCommandState> repository = default!;

    /// <inheritdoc />
    protected override async ValueTask OnStartup(ICommandActorContext<FuturesBbSignalCommandActor> context)
    {
        repository = IsArgumentNull.Set(context.Container.Resolve<
            IEventSourceActorStateRepository<FuturesBbSignalCommandState>>());
        await typedContext.EventProjector.StartAsync(context);
    }
    /// <inheritdoc />
    protected override async ValueTask OnShutdown(ICommandActorContext<FuturesBbSignalCommandActor> context) =>
        await typedContext.EventProjector.StopAsync();
    /// <inheritdoc />
    protected override ICommand ParseMessage(
        ICommandActorContext<FuturesBbSignalCommandActor> context,
        IActorMessage message)
        => ParseMappedCommand(context, message, _parseMap);

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> _parseMap = new Dictionary<string, Func<IActorMessage, ICommand>>()
    {
        [GenerateFuturesBbSignalCommand.Verb] = message =>
            message.AsCommand<GenerateFuturesBbSignalCommand>()
    };
    /// <inheritdoc />
    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(
        ICommandActorContext<FuturesBbSignalCommandActor> context, IActorState state, ICommand command)
    {
        var receiveCommand = ResolveMappedCommandHandler(command, _receiveMap);
        return ValueTask.FromResult(receiveCommand.Invoke(
            command,
            context,
            (FuturesBbSignalCommandState)state));
    }

    static readonly IReadOnlyDictionary<Type, Func<ICommand, ICommandActorContext<FuturesBbSignalCommandActor>,
        FuturesBbSignalCommandState, ServiceResult<GuidResult>>> _receiveMap = new Dictionary<Type, Func<ICommand, ICommandActorContext<FuturesBbSignalCommandActor>,
        FuturesBbSignalCommandState, ServiceResult<GuidResult>>>()
    {
        [typeof(GenerateFuturesBbSignalCommand)] = static (command, _, state) =>
            ((GenerateFuturesBbSignalCommand)command).Execute(state)
    };
    /// <inheritdoc />
    protected override ValueTask OnValidateAsync(ICommandActorContext<FuturesBbSignalCommandActor> context,
        ActorThreadId threadId, ICommand command) => OnValidateAsync(context, threadId, command, CancellationToken.None);
    /// <inheritdoc />
    protected override ValueTask OnValidateAsync(ICommandActorContext<FuturesBbSignalCommandActor> context,
        ActorThreadId threadId, ICommand command, CancellationToken cancellationToken)
    {
        ValidateMappedCommand(command, _validationMap);
        return ValueTask.CompletedTask;
    }

    static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>()
    {
        [typeof(GenerateFuturesBbSignalCommand)] = static command =>
        {
            var generate = (GenerateFuturesBbSignalCommand)command;
            return new List<ValidationError>()
                .ValidateCommandId(generate.CommandId, generate.CommandName)
                .ValidateEntityId(generate.EntityId, generate.CommandName);
        }
    };
    /// <inheritdoc />
    protected override async ValueTask<IActorState> OnLoadStateAsync(
        ICommandActorContext<FuturesBbSignalCommandActor> context, ActorThreadId threadId, ICommand command) =>
        await repository.LoadStateAsync(command);
    /// <inheritdoc />
    protected override async ValueTask<IActorState> OnLoadStateAsync(
        ICommandActorContext<FuturesBbSignalCommandActor> context, ActorThreadId threadId, ICommand command,
        CancellationToken cancellationToken) => await repository.LoadStateAsync(command, cancellationToken);
    /// <inheritdoc />
    protected override async ValueTask OnSaveStateAsync(ICommandActorContext<FuturesBbSignalCommandActor> context,
        ActorThreadId threadId, IActorState state, ICommand command) =>
        await repository.SaveStateAsync(context, (FuturesBbSignalCommandState)state, command);
    /// <inheritdoc />
    protected override async ValueTask OnSaveStateAsync(ICommandActorContext<FuturesBbSignalCommandActor> context,
        ActorThreadId threadId, IActorState state, ICommand command, CancellationToken cancellationToken) =>
        await repository.SaveStateAsync(context, (FuturesBbSignalCommandState)state, command, cancellationToken);
    /// <inheritdoc />
    protected override async ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(
        ICommandActorContext<FuturesBbSignalCommandActor> context, ActorThreadId threadId,
        ICommand command, Exception exception)
    {
        var error = await exception.SendErrorEventAsync<
            TomasAI.IFM.Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(ErrorType.Command, context);
        return new ServiceFailed<GuidResult>(error);
    }
}
