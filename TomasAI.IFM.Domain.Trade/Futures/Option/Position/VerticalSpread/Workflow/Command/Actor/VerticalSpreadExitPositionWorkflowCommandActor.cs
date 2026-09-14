using System.Collections.Frozen;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Workflow.Command.State;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Workflow.Command.Actor;

public sealed class VerticalSpreadExitPositionWorkflowCommandActor(
    ICommandActorContext<VerticalSpreadExitPositionWorkflowCommandActor> actorContext)
    : BaseEventSourceCommandActor<VerticalSpreadExitPositionWorkflowCommandActor>(actorContext, Typed(actorContext).Logger)
{
    public const string ActorName = StartVerticalSpreadExitPositionWorkflowCommand.Actor;
    readonly IVerticalSpreadExitPositionWorkflowCommandContext services = Typed(actorContext);

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> ParseMap =
        new Dictionary<string, Func<IActorMessage, ICommand>>(StringComparer.Ordinal)
        {
            [StartVerticalSpreadExitPositionWorkflowCommand.Verb] = static message =>
                message.AsCommand<StartVerticalSpreadExitPositionWorkflowCommand>()!
        }.ToFrozenDictionary(StringComparer.Ordinal);

    static readonly IReadOnlyDictionary<Type, Func<ICommand, VerticalSpreadExitPositionWorkflowCommandState,
        ServiceResult<GuidResult>>> ReceiveMap =
        new Dictionary<Type, Func<ICommand, VerticalSpreadExitPositionWorkflowCommandState, ServiceResult<GuidResult>>>
        {
            [typeof(StartVerticalSpreadExitPositionWorkflowCommand)] = static (command, state) =>
                ((StartVerticalSpreadExitPositionWorkflowCommand)command).Execute(state)
        }.ToFrozenDictionary();

    protected override ValueTask OnStartup(ICommandActorContext<VerticalSpreadExitPositionWorkflowCommandActor> context) =>
        services.EventProjector.StartAsync(context);
    protected override ValueTask OnShutdown(ICommandActorContext<VerticalSpreadExitPositionWorkflowCommandActor> context) =>
        services.EventProjector.StopAsync();
    protected override ICommand ParseMessage(ICommandActorContext<VerticalSpreadExitPositionWorkflowCommandActor> context,
        IActorMessage message) => ParseMappedCommand(context, message, ParseMap);
    protected override ValueTask OnValidateAsync(ICommandActorContext<VerticalSpreadExitPositionWorkflowCommandActor> context,
        ActorThreadId threadId, ICommand command)
    {
        var typed = (StartVerticalSpreadExitPositionWorkflowCommand)command;
        var errors = new List<ValidationError>().ValidateCommandId(typed.CommandId, typed.CommandName)
            .ValidateEntityId(typed.EntityId, typed.CommandName);
        if (errors.Count != 0) throw new ValidationException([.. errors]);
        return ValueTask.CompletedTask;
    }
    protected override async ValueTask<IActorState> OnLoadStateAsync(
        ICommandActorContext<VerticalSpreadExitPositionWorkflowCommandActor> context, ActorThreadId threadId,
        ICommand command) => await services.StateRepository.LoadStateAsync(command).ConfigureAwait(false);
    protected override async ValueTask OnSaveStateAsync(
        ICommandActorContext<VerticalSpreadExitPositionWorkflowCommandActor> context, ActorThreadId threadId,
        IActorState state, ICommand command) => await services.StateRepository.SaveStateAsync(
            context, (VerticalSpreadExitPositionWorkflowCommandState)state, command).ConfigureAwait(false);
    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(
        ICommandActorContext<VerticalSpreadExitPositionWorkflowCommandActor> context, IActorState state,
        ICommand command) => ValueTask.FromResult(ResolveMappedCommandHandler(command, ReceiveMap)(
            command, (VerticalSpreadExitPositionWorkflowCommandState)state));
    protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(
        ICommandActorContext<VerticalSpreadExitPositionWorkflowCommandActor> context, ActorThreadId threadId,
        ICommand command, Exception exception) => ValueTask.FromResult<ServiceResult<GuidResult>>(
            new ServiceFailed<GuidResult>(command.ErrorCode, exception.Message));
    static IVerticalSpreadExitPositionWorkflowCommandContext Typed(
        ICommandActorContext<VerticalSpreadExitPositionWorkflowCommandActor> context) =>
        context as IVerticalSpreadExitPositionWorkflowCommandContext ??
        throw new ArgumentException("Typed Vertical Spread exit-workflow context required.");
}
