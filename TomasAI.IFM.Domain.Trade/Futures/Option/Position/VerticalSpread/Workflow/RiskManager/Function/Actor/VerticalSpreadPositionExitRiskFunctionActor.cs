using System.Collections.Frozen;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Function;
using TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Function.Actor;
using TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Function.State;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Workflow.RiskManager.Function;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Workflow.RiskManager.Function.Actor;

public sealed class VerticalSpreadPositionExitRiskFunctionActor(
    IFunctionActorContext<VerticalSpreadPositionExitRiskFunctionActor> actorContext)
    : BaseEventSourceFunctionActor<VerticalSpreadPositionExitRiskFunctionActor,
        EvaluatePositionExitRiskCommand, ExitPositionWorkflowId, ExitPositionWorkflowId,
        PositionExitRiskFunctionState, PositionExitRiskCompletedEvent,
        ExitPositionWorkflowFailedEvent>(
            Typed(actorContext), Typed(actorContext).StateRepository, null, Typed(actorContext).Logger)
{
    public const string ActorName = ExitPositionWorkflowActorNames.VerticalSpreadRiskManager;
    readonly IPositionExitRiskFunctionContext<VerticalSpreadPositionExitRiskFunctionActor> context = Typed(actorContext);

    static readonly IReadOnlyDictionary<string, Func<IActorMessage,
        EvaluatePositionExitRiskCommand>> ParseMap =
        new Dictionary<string, Func<IActorMessage, EvaluatePositionExitRiskCommand>>(
            StringComparer.Ordinal)
        {
            [EvaluatePositionExitRiskCommand.Verb] = static message =>
                message.AsCommand<EvaluatePositionExitRiskCommand>()!
        }.ToFrozenDictionary(StringComparer.Ordinal);

    static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> ValidationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>
        {
            [typeof(EvaluatePositionExitRiskCommand)] = static command =>
                new List<ValidationError>()
                    .ValidateCommandId(command.CommandId, command.CommandName)
                    .CaptureCommandValidation(() =>
                        StrategyExitFunctionEventMapping.ValidateRisk(
                            (EvaluatePositionExitRiskCommand)command, ActorName, TradeStrategyKind.VerticalSpread))
        }.ToFrozenDictionary();

    static readonly IReadOnlyDictionary<Type, Func<EvaluatePositionExitRiskCommand,
        PositionExitRiskFunctionState, IPositionExitRiskFunctionContext<VerticalSpreadPositionExitRiskFunctionActor>,
        Func<FunctionEventContext<EvaluatePositionExitRiskCommand>,
            FunctionResult<PositionExitRiskCompletedEvent, ExitPositionWorkflowFailedEvent>>,
        CancellationToken, ValueTask<FunctionResult<PositionExitRiskCompletedEvent,
            ExitPositionWorkflowFailedEvent>>>> ReceiveMap =
        new Dictionary<Type, Func<EvaluatePositionExitRiskCommand,
            PositionExitRiskFunctionState,
            IPositionExitRiskFunctionContext<VerticalSpreadPositionExitRiskFunctionActor>,
            Func<FunctionEventContext<EvaluatePositionExitRiskCommand>,
                FunctionResult<PositionExitRiskCompletedEvent, ExitPositionWorkflowFailedEvent>>,
            CancellationToken, ValueTask<FunctionResult<PositionExitRiskCompletedEvent,
                ExitPositionWorkflowFailedEvent>>>>
        {
            [typeof(EvaluatePositionExitRiskCommand)] =
                static (request, state, owner, dispatch, token) =>
                    request.ExecuteAsync(
                        state, owner.Portfolio, owner.TimeProvider, dispatch, token)
        }.ToFrozenDictionary();

    static readonly IReadOnlyDictionary<Type,
        Func<FunctionEventContext<EvaluatePositionExitRiskCommand>, TimeProvider,
            FunctionResult<PositionExitRiskCompletedEvent, ExitPositionWorkflowFailedEvent>>>
        EventMap =
        new Dictionary<Type,
            Func<FunctionEventContext<EvaluatePositionExitRiskCommand>, TimeProvider,
                FunctionResult<PositionExitRiskCompletedEvent, ExitPositionWorkflowFailedEvent>>>
        {
            [typeof(PositionExitRiskCompletedEvent)] = static (input, clock) =>
                input.Complete(clock),
            [typeof(ExitPositionWorkflowFailedEvent)] = static (input, clock) =>
                input.Fail(clock)
        }.ToFrozenDictionary();

    static readonly IReadOnlyDictionary<Type, Func<EvaluatePositionExitRiskCommand,
        FunctionFailureStage, IPositionExitRiskFunctionContext<VerticalSpreadPositionExitRiskFunctionActor>,
        FunctionExecutionPolicy>> _executionPolicyMap =
        new Dictionary<Type, Func<EvaluatePositionExitRiskCommand, FunctionFailureStage,
            IPositionExitRiskFunctionContext<VerticalSpreadPositionExitRiskFunctionActor>, FunctionExecutionPolicy>>
        {
            [typeof(EvaluatePositionExitRiskCommand)] = static (request, stage, owner) =>
                request.ResolveExecutionPolicy(stage, owner.TimeProvider)
        }.ToFrozenDictionary();

    protected override FunctionExecutionPolicy ResolveExecutionPolicy(
        EvaluatePositionExitRiskCommand request, FunctionFailureStage stage) =>
        DispatchMappedExecutionPolicy(request, stage, context, _executionPolicyMap);

    protected override EvaluatePositionExitRiskCommand ParseMessage(
        IFunctionActorContext<VerticalSpreadPositionExitRiskFunctionActor> actorContext,
        IActorMessage message) =>
        ParseMappedFunction(actorContext, message, ParseMap);

    protected override ValueTask ValidateAsync(
        IFunctionActorContext<VerticalSpreadPositionExitRiskFunctionActor> actorContext,
        ActorThreadId threadId,
        EvaluatePositionExitRiskCommand request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateMappedCommand(request, ValidationMap);
        return ValueTask.CompletedTask;
    }

    protected override ValueTask<FunctionResult<PositionExitRiskCompletedEvent,
        ExitPositionWorkflowFailedEvent>> ExecuteFunctionAsync(
        IFunctionActorContext<VerticalSpreadPositionExitRiskFunctionActor> actorContext,
        PositionExitRiskFunctionState state,
        EvaluatePositionExitRiskCommand request,
        CancellationToken cancellationToken)
        => ResolveMappedFunctionHandler(request, ReceiveMap)(
            request, state, context, input => HandleFunctionEvent(actorContext, input),
            cancellationToken);

    protected override FunctionResult<PositionExitRiskCompletedEvent,
        ExitPositionWorkflowFailedEvent> HandleFunctionEvent(
        IFunctionActorContext<VerticalSpreadPositionExitRiskFunctionActor> actorContext,
        FunctionEventContext<EvaluatePositionExitRiskCommand> input) =>
        DispatchMappedFunctionEvent(input, context.TimeProvider, EventMap);

    static IPositionExitRiskFunctionContext<VerticalSpreadPositionExitRiskFunctionActor> Typed(
        IFunctionActorContext<VerticalSpreadPositionExitRiskFunctionActor> actorContext) =>
        actorContext as IPositionExitRiskFunctionContext<VerticalSpreadPositionExitRiskFunctionActor> ??
        throw new ArgumentException("Typed Vertical Spread position-exit risk context required.");
}
