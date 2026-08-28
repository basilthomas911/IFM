using Microsoft.Extensions.Logging;
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
    protected override ICommand ParseMessage(
        ICommandActorContext<FuturesVxTermStructureSignalCommandActor> context,
        IActorMessage message)
        => ParseMappedCommand(context, message, _parseMap);

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> _parseMap = new Dictionary<string, Func<IActorMessage, ICommand>>()
    {
        [UpdateFuturesVxTermStructureSignalCommand.Verb] = message =>
            message.AsCommand<UpdateFuturesVxTermStructureSignalCommand>()
    };
    /// <inheritdoc />
    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(
        ICommandActorContext<FuturesVxTermStructureSignalCommandActor> context,
        IActorState state, ICommand command)
    {
        var receiveCommand = ResolveMappedCommandHandler(command, _receiveMap);
        return ValueTask.FromResult(receiveCommand.Invoke(
            command,
            context,
            (FuturesVxTermStructureSignalCommandState)state));
    }

    static readonly IReadOnlyDictionary<Type, Func<ICommand,
        ICommandActorContext<FuturesVxTermStructureSignalCommandActor>,
        FuturesVxTermStructureSignalCommandState, ServiceResult<GuidResult>>> _receiveMap = new Dictionary<Type, Func<ICommand,
        ICommandActorContext<FuturesVxTermStructureSignalCommandActor>,
        FuturesVxTermStructureSignalCommandState, ServiceResult<GuidResult>>>()
    {
        [typeof(UpdateFuturesVxTermStructureSignalCommand)] = static (command, _, state) =>
            ((UpdateFuturesVxTermStructureSignalCommand)command).Execute(state)
    };
    /// <inheritdoc />
    protected override ValueTask OnValidateAsync(ICommandActorContext<FuturesVxTermStructureSignalCommandActor> context,
        ActorThreadId threadId, ICommand command) => OnValidateAsync(context, threadId, command, CancellationToken.None);
    /// <inheritdoc />
    protected override ValueTask OnValidateAsync(ICommandActorContext<FuturesVxTermStructureSignalCommandActor> context,
        ActorThreadId threadId, ICommand command, CancellationToken cancellationToken)
    {
        ValidateMappedCommand(command, _validationMap);
        return ValueTask.CompletedTask;
    }

    static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>()
    {
        [typeof(UpdateFuturesVxTermStructureSignalCommand)] = static command =>
        {
            var update = (UpdateFuturesVxTermStructureSignalCommand)command;
            return new List<ValidationError>()
                .ValidateCommandId(update.CommandId, update.CommandName)
                .ValidateEntityId(update.EntityId, update.CommandName);
        }
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
