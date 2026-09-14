using System.Collections.Frozen;
using TomasAI.IFM.Domain.Trade.Futures.Position.Workflow.Command.State;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Workflow.Command.Actor;

public sealed class FuturesExitPositionWorkflowCommandActor(
    ICommandActorContext<FuturesExitPositionWorkflowCommandActor> actorContext)
    : BaseEventSourceCommandActor<FuturesExitPositionWorkflowCommandActor>(actorContext, Typed(actorContext).Logger)
{
    public const string ActorName = StartFuturesExitPositionWorkflowCommand.Actor;
    readonly IFuturesExitPositionWorkflowCommandContext services = Typed(actorContext);

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> ParseMap =
        new Dictionary<string, Func<IActorMessage, ICommand>>(StringComparer.Ordinal)
        {
            [StartFuturesExitPositionWorkflowCommand.Verb] = static message =>
                message.AsCommand<StartFuturesExitPositionWorkflowCommand>()!
        }.ToFrozenDictionary(StringComparer.Ordinal);

    static readonly IReadOnlyDictionary<Type, Func<ICommand, FuturesExitPositionWorkflowCommandState,
        ServiceResult<GuidResult>>> ReceiveMap =
        new Dictionary<Type, Func<ICommand, FuturesExitPositionWorkflowCommandState, ServiceResult<GuidResult>>>
        {
            [typeof(StartFuturesExitPositionWorkflowCommand)] = static (command, state) =>
                ((StartFuturesExitPositionWorkflowCommand)command).Execute(state)
        }.ToFrozenDictionary();

    protected override ValueTask OnStartup(ICommandActorContext<FuturesExitPositionWorkflowCommandActor> context) =>
        services.EventProjector.StartAsync(context);
    protected override ValueTask OnShutdown(ICommandActorContext<FuturesExitPositionWorkflowCommandActor> context) =>
        services.EventProjector.StopAsync();
    protected override ICommand ParseMessage(ICommandActorContext<FuturesExitPositionWorkflowCommandActor> context,
        IActorMessage message) => ParseMappedCommand(context, message, ParseMap);
    protected override ValueTask OnValidateAsync(ICommandActorContext<FuturesExitPositionWorkflowCommandActor> context,
        ActorThreadId threadId, ICommand command)
    {
        var typed = (StartFuturesExitPositionWorkflowCommand)command;
        var errors = new List<ValidationError>().ValidateCommandId(typed.CommandId, typed.CommandName)
            .ValidateEntityId(typed.EntityId, typed.CommandName);
        if (errors.Count != 0) throw new ValidationException([.. errors]);
        return ValueTask.CompletedTask;
    }
    protected override async ValueTask<IActorState> OnLoadStateAsync(
        ICommandActorContext<FuturesExitPositionWorkflowCommandActor> context, ActorThreadId threadId,
        ICommand command) => await services.StateRepository.LoadStateAsync(command).ConfigureAwait(false);
    protected override async ValueTask OnSaveStateAsync(
        ICommandActorContext<FuturesExitPositionWorkflowCommandActor> context, ActorThreadId threadId,
        IActorState state, ICommand command) => await services.StateRepository.SaveStateAsync(
            context, (FuturesExitPositionWorkflowCommandState)state, command).ConfigureAwait(false);
    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(
        ICommandActorContext<FuturesExitPositionWorkflowCommandActor> context, IActorState state,
        ICommand command) => ValueTask.FromResult(ResolveMappedCommandHandler(command, ReceiveMap)(
            command, (FuturesExitPositionWorkflowCommandState)state));
    protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(
        ICommandActorContext<FuturesExitPositionWorkflowCommandActor> context, ActorThreadId threadId,
        ICommand command, Exception exception) => ValueTask.FromResult<ServiceResult<GuidResult>>(
            new ServiceFailed<GuidResult>(command.ErrorCode, exception.Message));
    static IFuturesExitPositionWorkflowCommandContext Typed(
        ICommandActorContext<FuturesExitPositionWorkflowCommandActor> context) =>
        context as IFuturesExitPositionWorkflowCommandContext ??
        throw new ArgumentException("Typed Futures exit-workflow context required.");
}
