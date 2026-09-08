using System.Collections.Frozen;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Function;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Function.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Function.Actor;

/// <summary>Mapped, completed-only selection Function with deadline-bounded lifecycle stages.</summary>
public sealed class TradeSelectionFunctionActor(ITradeSelectionFunctionContext actorContext)
    : BaseEventSourceFunctionActor<TradeSelectionFunctionActor, ExecuteTradeSelectionPipelineCommand,
        TradeSelectionExecutionId, IntrinsicTimeStrategyWorkflowEntityId, TradeSelectionFunctionState,
        TradeSelectionFunctionCompletedEvent, TradeSelectionFunctionFailedEvent>(
            actorContext ?? throw new ArgumentNullException(nameof(actorContext)),
            actorContext.StateRepository, actorContext.FunctionProjector, actorContext.Logger)
{
    readonly ITradeSelectionFunctionContext _context = actorContext;

    public const string ActorName = ExecuteTradeSelectionPipelineCommand.Actor;

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ExecuteTradeSelectionPipelineCommand>> _parseMap =
        new Dictionary<string, Func<IActorMessage, ExecuteTradeSelectionPipelineCommand>>(StringComparer.Ordinal)
        {
            [ExecuteTradeSelectionPipelineCommand.Verb] = static message => message.AsCommand<ExecuteTradeSelectionPipelineCommand>()!
        }.ToFrozenDictionary(StringComparer.Ordinal);

    readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>
        {
            [typeof(ExecuteTradeSelectionPipelineCommand)] = command =>
            {
                var request = (ExecuteTradeSelectionPipelineCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(request.CommandId, request.CommandName)
                    .ValidateSelectionFields(request)
                    .ValidateSelectionConsistency(request)
                    .ValidateSelectionCapabilities(request, actorContext.Capabilities);
            }
        }.ToFrozenDictionary();

    static readonly IReadOnlyDictionary<Type, Func<ExecuteTradeSelectionPipelineCommand,
        ITradeSelectionFunctionContext,
        Func<FunctionEventContext<ExecuteTradeSelectionPipelineCommand>, FunctionResult<TradeSelectionFunctionCompletedEvent, TradeSelectionFunctionFailedEvent>>, CancellationToken,
        ValueTask<FunctionResult<TradeSelectionFunctionCompletedEvent, TradeSelectionFunctionFailedEvent>>>> _receiveMap =
        new Dictionary<Type, Func<ExecuteTradeSelectionPipelineCommand, ITradeSelectionFunctionContext,
        Func<FunctionEventContext<ExecuteTradeSelectionPipelineCommand>, FunctionResult<TradeSelectionFunctionCompletedEvent, TradeSelectionFunctionFailedEvent>>, CancellationToken,
            ValueTask<FunctionResult<TradeSelectionFunctionCompletedEvent, TradeSelectionFunctionFailedEvent>>>>
        {
            [typeof(ExecuteTradeSelectionPipelineCommand)] = static (request, context, dispatchEvent, token) => request.ExecuteAsync(context, dispatchEvent, token)
        }.ToFrozenDictionary();

    static readonly IReadOnlyDictionary<Type, Func<ExecuteTradeSelectionPipelineCommand, FunctionFailureStage,
        ITradeSelectionFunctionContext, FunctionExecutionPolicy>> _executionPolicyMap =
        new Dictionary<Type, Func<ExecuteTradeSelectionPipelineCommand, FunctionFailureStage,
            ITradeSelectionFunctionContext, FunctionExecutionPolicy>>
        {
            [typeof(ExecuteTradeSelectionPipelineCommand)] = static (request, stage, context) => request.ResolveExecutionPolicy(stage, context)
        }.ToFrozenDictionary();

    /// <summary>Maps lifecycle policy requests without interpreting actor-specific deadlines or settings.</summary>
    protected override FunctionExecutionPolicy ResolveExecutionPolicy(ExecuteTradeSelectionPipelineCommand request, FunctionFailureStage stage)
        => DispatchMappedExecutionPolicy(request, stage, _context, _executionPolicyMap);

    protected override ExecuteTradeSelectionPipelineCommand ParseMessage(
        IFunctionActorContext<TradeSelectionFunctionActor> context, IActorMessage message)
        => ParseMappedFunction(context, message, _parseMap);

    protected override ValueTask ValidateAsync(IFunctionActorContext<TradeSelectionFunctionActor> context,
        ActorThreadId threadId, ExecuteTradeSelectionPipelineCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        ValidateMappedCommand(request, _validationMap);
        return ValueTask.CompletedTask;
    }

    static readonly IReadOnlyDictionary<Type, Func<FunctionEventContext<ExecuteTradeSelectionPipelineCommand>, TimeProvider,
        FunctionResult<TradeSelectionFunctionCompletedEvent, TradeSelectionFunctionFailedEvent>>> _eventMap =
        new Dictionary<Type, Func<FunctionEventContext<ExecuteTradeSelectionPipelineCommand>, TimeProvider,
            FunctionResult<TradeSelectionFunctionCompletedEvent, TradeSelectionFunctionFailedEvent>>>
        {
            [typeof(TradeSelectionFunctionCompletedEvent)] = static (input, clock) => input.Complete(clock),
            [typeof(TradeSelectionFunctionFailedEvent)] = static (input, clock) => input.Fail(clock)
        }.ToFrozenDictionary();

    /// <summary>Dispatches the request through the receive map with the shared terminal-event callback.</summary>
    protected override ValueTask<FunctionResult<TradeSelectionFunctionCompletedEvent, TradeSelectionFunctionFailedEvent>> ExecuteFunctionAsync(
        IFunctionActorContext<TradeSelectionFunctionActor> context, TradeSelectionFunctionState state,
        ExecuteTradeSelectionPipelineCommand request, CancellationToken cancellationToken)
        => ResolveMappedFunctionHandler(request, _receiveMap)(request, _context,
            input => HandleFunctionEvent(context, input), cancellationToken);

    /// <summary>Dispatches lifecycle failures and execution outcomes through the exact terminal-event map.</summary>
    protected override FunctionResult<TradeSelectionFunctionCompletedEvent, TradeSelectionFunctionFailedEvent> HandleFunctionEvent(
        IFunctionActorContext<TradeSelectionFunctionActor> context, FunctionEventContext<ExecuteTradeSelectionPipelineCommand> input)
        => MapEvent(input, _context.TimeProvider);

    /// <summary>Resolves terminal handlers for Function outcomes and workflow transport failures.</summary>
    internal static FunctionResult<TradeSelectionFunctionCompletedEvent, TradeSelectionFunctionFailedEvent> MapEvent(
        FunctionEventContext<ExecuteTradeSelectionPipelineCommand> input, TimeProvider clock)
        => DispatchMappedFunctionEvent(input, clock, _eventMap);
}
