using Microsoft.Extensions.Logging;
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
    protected override ICommand ParseMessage(
        ICommandActorContext<FuturesEmaSignalCommandActor> context,
        IActorMessage message)
        => ParseMappedCommand(context, message, _parseMap);

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> _parseMap = new Dictionary<string, Func<IActorMessage, ICommand>>()
    {
        [GenerateFuturesEmaSignalCommand.Verb] = message =>
            message.AsCommand<GenerateFuturesEmaSignalCommand>()
    };

    /// <inheritdoc />
    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(
        ICommandActorContext<FuturesEmaSignalCommandActor> context, IActorState state, ICommand command)
    {
        var receiveCommand = ResolveMappedCommandHandler(command, _receiveMap);
        return ValueTask.FromResult(receiveCommand.Invoke(
            command,
            context,
            (FuturesEmaSignalCommandState)state));
    }

    static readonly IReadOnlyDictionary<Type, Func<ICommand, ICommandActorContext<FuturesEmaSignalCommandActor>,
        FuturesEmaSignalCommandState, ServiceResult<GuidResult>>> _receiveMap = new Dictionary<Type, Func<ICommand, ICommandActorContext<FuturesEmaSignalCommandActor>,
        FuturesEmaSignalCommandState, ServiceResult<GuidResult>>>()
    {
        [typeof(GenerateFuturesEmaSignalCommand)] = static (command, _, state) =>
            ((GenerateFuturesEmaSignalCommand)command).Execute(state)
    };

    /// <inheritdoc />
    protected override ValueTask OnValidateAsync(ICommandActorContext<FuturesEmaSignalCommandActor> context,
        ActorThreadId threadId, ICommand command) => OnValidateAsync(context, threadId, command, CancellationToken.None);

    /// <inheritdoc />
    protected override ValueTask OnValidateAsync(ICommandActorContext<FuturesEmaSignalCommandActor> context,
        ActorThreadId threadId, ICommand command, CancellationToken cancellationToken)
    {
        ValidateMappedCommand(command, _validationMap);
        return ValueTask.CompletedTask;
    }

    static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>()
    {
        [typeof(GenerateFuturesEmaSignalCommand)] = static command =>
        {
            var generate = (GenerateFuturesEmaSignalCommand)command;
            return new List<ValidationError>()
                .ValidateCommandId(generate.CommandId, generate.CommandName)
                .ValidateEntityId(generate.EntityId, generate.CommandName);
        }
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
