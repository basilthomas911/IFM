using System.Collections.Frozen;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Domain.Trade.Position.Workflow.Function;
using TomasAI.IFM.Domain.Trade.Position.Workflow.Function.Actor;
using TomasAI.IFM.Domain.Trade.Position.Workflow.Function.State;
using TomasAI.IFM.Domain.Trade.Futures.Position.Workflow.OrderComposer.Function;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Workflow.OrderComposer.Function.Actor;

public sealed class FuturesExitOrderCompositionFunctionActor(
    IFunctionActorContext<FuturesExitOrderCompositionFunctionActor> actorContext)
    : BaseEventSourceFunctionActor<FuturesExitOrderCompositionFunctionActor,
        ComposeExitOrderCommand, ExitPositionWorkflowId, ExitPositionWorkflowId,
        ExitOrderCompositionFunctionState, ExitOrderCompositionCompletedEvent,
        ExitPositionWorkflowFailedEvent>(
            Typed(actorContext), Typed(actorContext).StateRepository, null, Typed(actorContext).Logger)
{
    public const string ActorName = ExitPositionWorkflowActorNames.FuturesOrderComposer;
    readonly IExitOrderCompositionFunctionContext<FuturesExitOrderCompositionFunctionActor> context = Typed(actorContext);

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ComposeExitOrderCommand>> ParseMap =
        new Dictionary<string, Func<IActorMessage, ComposeExitOrderCommand>>(StringComparer.Ordinal)
        {
            [ComposeExitOrderCommand.Verb] = static message =>
                message.AsCommand<ComposeExitOrderCommand>()!
        }.ToFrozenDictionary(StringComparer.Ordinal);

    static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> ValidationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>
        {
            [typeof(ComposeExitOrderCommand)] = static command =>
                new List<ValidationError>()
                    .ValidateCommandId(command.CommandId, command.CommandName)
                    .CaptureCommandValidation(() =>
                        StrategyExitFunctionEventMapping.ValidateComposition(
                            (ComposeExitOrderCommand)command, ActorName, TradeStrategyKind.FuturesOutright))
        }.ToFrozenDictionary();

    static readonly IReadOnlyDictionary<Type, Func<ComposeExitOrderCommand,
        ExitOrderCompositionFunctionState, IExitOrderCompositionFunctionContext<FuturesExitOrderCompositionFunctionActor>,
        Func<FunctionEventContext<ComposeExitOrderCommand>,
            FunctionResult<ExitOrderCompositionCompletedEvent, ExitPositionWorkflowFailedEvent>>,
        CancellationToken, ValueTask<FunctionResult<ExitOrderCompositionCompletedEvent,
            ExitPositionWorkflowFailedEvent>>>> ReceiveMap =
        new Dictionary<Type, Func<ComposeExitOrderCommand, ExitOrderCompositionFunctionState,
            IExitOrderCompositionFunctionContext<FuturesExitOrderCompositionFunctionActor>,
            Func<FunctionEventContext<ComposeExitOrderCommand>,
                FunctionResult<ExitOrderCompositionCompletedEvent, ExitPositionWorkflowFailedEvent>>,
            CancellationToken, ValueTask<FunctionResult<ExitOrderCompositionCompletedEvent,
                ExitPositionWorkflowFailedEvent>>>>
        {
            [typeof(ComposeExitOrderCommand)] = static (request, state, owner, dispatch, token) =>
                request.ExecuteAsync(state, owner.TimeProvider, dispatch, token)
        }.ToFrozenDictionary();

    static readonly IReadOnlyDictionary<Type, Func<FunctionEventContext<ComposeExitOrderCommand>,
        TimeProvider, FunctionResult<ExitOrderCompositionCompletedEvent,
            ExitPositionWorkflowFailedEvent>>> EventMap =
        new Dictionary<Type, Func<FunctionEventContext<ComposeExitOrderCommand>, TimeProvider,
            FunctionResult<ExitOrderCompositionCompletedEvent, ExitPositionWorkflowFailedEvent>>>
        {
            [typeof(ExitOrderCompositionCompletedEvent)] = static (input, clock) =>
                input.Complete(clock),
            [typeof(ExitPositionWorkflowFailedEvent)] = static (input, clock) =>
                input.Fail(clock)
        }.ToFrozenDictionary();

    static readonly IReadOnlyDictionary<Type, Func<ComposeExitOrderCommand,
        FunctionFailureStage, IExitOrderCompositionFunctionContext<FuturesExitOrderCompositionFunctionActor>,
        FunctionExecutionPolicy>> _executionPolicyMap =
        new Dictionary<Type, Func<ComposeExitOrderCommand, FunctionFailureStage,
            IExitOrderCompositionFunctionContext<FuturesExitOrderCompositionFunctionActor>, FunctionExecutionPolicy>>
        {
            [typeof(ComposeExitOrderCommand)] = static (request, stage, owner) =>
                request.ResolveExecutionPolicy(stage, owner.TimeProvider)
        }.ToFrozenDictionary();

    protected override FunctionExecutionPolicy ResolveExecutionPolicy(
        ComposeExitOrderCommand request, FunctionFailureStage stage) =>
        DispatchMappedExecutionPolicy(request, stage, context, _executionPolicyMap);

    protected override ComposeExitOrderCommand ParseMessage(
        IFunctionActorContext<FuturesExitOrderCompositionFunctionActor> actorContext,
        IActorMessage message) =>
        ParseMappedFunction(actorContext, message, ParseMap);

    protected override ValueTask ValidateAsync(
        IFunctionActorContext<FuturesExitOrderCompositionFunctionActor> actorContext,
        ActorThreadId threadId,
        ComposeExitOrderCommand request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateMappedCommand(request, ValidationMap);
        return ValueTask.CompletedTask;
    }

    protected override ValueTask<FunctionResult<ExitOrderCompositionCompletedEvent,
        ExitPositionWorkflowFailedEvent>> ExecuteFunctionAsync(
        IFunctionActorContext<FuturesExitOrderCompositionFunctionActor> actorContext,
        ExitOrderCompositionFunctionState state,
        ComposeExitOrderCommand request,
        CancellationToken cancellationToken) =>
        ResolveMappedFunctionHandler(request, ReceiveMap)(
            request, state, context, input => HandleFunctionEvent(actorContext, input),
            cancellationToken);

    protected override FunctionResult<ExitOrderCompositionCompletedEvent,
        ExitPositionWorkflowFailedEvent> HandleFunctionEvent(
        IFunctionActorContext<FuturesExitOrderCompositionFunctionActor> actorContext,
        FunctionEventContext<ComposeExitOrderCommand> input) =>
        DispatchMappedFunctionEvent(input, context.TimeProvider, EventMap);

    static IExitOrderCompositionFunctionContext<FuturesExitOrderCompositionFunctionActor> Typed(
        IFunctionActorContext<FuturesExitOrderCompositionFunctionActor> actorContext) =>
        actorContext as IExitOrderCompositionFunctionContext<FuturesExitOrderCompositionFunctionActor> ??
        throw new ArgumentException("Typed Futures exit-composition context required.");
}
