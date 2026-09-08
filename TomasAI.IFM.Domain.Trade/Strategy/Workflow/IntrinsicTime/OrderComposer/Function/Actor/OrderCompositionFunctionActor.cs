using System.Collections.Frozen;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Function;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Function.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Function.Actor;

/// <summary>Mapped, completed-only composition Function with deadline-bounded lifecycle stages.</summary>
public sealed class OrderCompositionFunctionActor(IOrderCompositionFunctionContext actorContext)
    : BaseEventSourceFunctionActor<OrderCompositionFunctionActor, ExecuteOrderCompositionPipelineCommand,
        OrderCompositionExecutionId, IntrinsicTimeStrategyWorkflowEntityId, OrderCompositionFunctionState,
        OrderCompositionFunctionCompletedEvent, OrderCompositionFunctionFailedEvent>(
            actorContext ?? throw new ArgumentNullException(nameof(actorContext)),
            actorContext.StateRepository, actorContext.FunctionProjector, actorContext.Logger)
{
    readonly IOrderCompositionFunctionContext _context = actorContext;

    public const string ActorName = ExecuteOrderCompositionPipelineCommand.Actor;

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ExecuteOrderCompositionPipelineCommand>> _parseMap =
        new Dictionary<string, Func<IActorMessage, ExecuteOrderCompositionPipelineCommand>>(StringComparer.Ordinal)
        {
            [ExecuteOrderCompositionPipelineCommand.Verb] = static message => message.AsCommand<ExecuteOrderCompositionPipelineCommand>()!
        }.ToFrozenDictionary(StringComparer.Ordinal);

    readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>
        {
            [typeof(ExecuteOrderCompositionPipelineCommand)] = command =>
            {
                var request = (ExecuteOrderCompositionPipelineCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(request.CommandId, request.CommandName)
                    .ValidateCompositionFields(request)
                    .ValidateCompositionEvidence(request)
                    .ValidateCompositionCatalog(request);
            }
        }.ToFrozenDictionary();

    static readonly IReadOnlyDictionary<Type, Func<ExecuteOrderCompositionPipelineCommand,
        IOrderCompositionFunctionContext,
        Func<FunctionEventContext<ExecuteOrderCompositionPipelineCommand>, FunctionResult<OrderCompositionFunctionCompletedEvent, OrderCompositionFunctionFailedEvent>>, CancellationToken,
        ValueTask<FunctionResult<OrderCompositionFunctionCompletedEvent, OrderCompositionFunctionFailedEvent>>>> _receiveMap =
        new Dictionary<Type, Func<ExecuteOrderCompositionPipelineCommand, IOrderCompositionFunctionContext,
        Func<FunctionEventContext<ExecuteOrderCompositionPipelineCommand>, FunctionResult<OrderCompositionFunctionCompletedEvent, OrderCompositionFunctionFailedEvent>>, CancellationToken,
            ValueTask<FunctionResult<OrderCompositionFunctionCompletedEvent, OrderCompositionFunctionFailedEvent>>>>
        {
            [typeof(ExecuteOrderCompositionPipelineCommand)] = static (request, context, dispatchEvent, token) => request.ExecuteAsync(context, dispatchEvent, token)
        }.ToFrozenDictionary();

    static readonly IReadOnlyDictionary<Type, Func<ExecuteOrderCompositionPipelineCommand, FunctionFailureStage,
        IOrderCompositionFunctionContext, FunctionExecutionPolicy>> _executionPolicyMap =
        new Dictionary<Type, Func<ExecuteOrderCompositionPipelineCommand, FunctionFailureStage,
            IOrderCompositionFunctionContext, FunctionExecutionPolicy>>
        {
            [typeof(ExecuteOrderCompositionPipelineCommand)] = static (request, stage, context) => request.ResolveExecutionPolicy(stage, context)
        }.ToFrozenDictionary();

    /// <summary>Maps lifecycle policy requests without interpreting actor-specific deadlines or settings.</summary>
    protected override FunctionExecutionPolicy ResolveExecutionPolicy(ExecuteOrderCompositionPipelineCommand request, FunctionFailureStage stage)
        => DispatchMappedExecutionPolicy(request, stage, _context, _executionPolicyMap);

    protected override ExecuteOrderCompositionPipelineCommand ParseMessage(
        IFunctionActorContext<OrderCompositionFunctionActor> context, IActorMessage message)
        => ParseMappedFunction(context, message, _parseMap);

    protected override ValueTask ValidateAsync(IFunctionActorContext<OrderCompositionFunctionActor> context,
        ActorThreadId threadId, ExecuteOrderCompositionPipelineCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        ValidateMappedCommand(request, _validationMap);
        return ValueTask.CompletedTask;
    }

    static readonly IReadOnlyDictionary<Type, Func<FunctionEventContext<ExecuteOrderCompositionPipelineCommand>, TimeProvider,
        FunctionResult<OrderCompositionFunctionCompletedEvent, OrderCompositionFunctionFailedEvent>>> _eventMap =
        new Dictionary<Type, Func<FunctionEventContext<ExecuteOrderCompositionPipelineCommand>, TimeProvider,
            FunctionResult<OrderCompositionFunctionCompletedEvent, OrderCompositionFunctionFailedEvent>>>
        {
            [typeof(OrderCompositionFunctionCompletedEvent)] = static (input, clock) => input.Complete(clock),
            [typeof(OrderCompositionFunctionFailedEvent)] = static (input, clock) => input.Fail(clock)
        }.ToFrozenDictionary();

    /// <summary>Dispatches the request through the receive map with the shared terminal-event callback.</summary>
    protected override ValueTask<FunctionResult<OrderCompositionFunctionCompletedEvent, OrderCompositionFunctionFailedEvent>> ExecuteFunctionAsync(
        IFunctionActorContext<OrderCompositionFunctionActor> context, OrderCompositionFunctionState state,
        ExecuteOrderCompositionPipelineCommand request, CancellationToken cancellationToken)
        => ResolveMappedFunctionHandler(request, _receiveMap)(request, _context,
            input => HandleFunctionEvent(context, input), cancellationToken);

    /// <summary>Dispatches lifecycle failures and execution outcomes through the exact terminal-event map.</summary>
    protected override FunctionResult<OrderCompositionFunctionCompletedEvent, OrderCompositionFunctionFailedEvent> HandleFunctionEvent(
        IFunctionActorContext<OrderCompositionFunctionActor> context, FunctionEventContext<ExecuteOrderCompositionPipelineCommand> input)
        => MapEvent(input, _context.TimeProvider);

    /// <summary>Resolves terminal handlers for Function outcomes and workflow transport failures.</summary>
    internal static FunctionResult<OrderCompositionFunctionCompletedEvent, OrderCompositionFunctionFailedEvent> MapEvent(
        FunctionEventContext<ExecuteOrderCompositionPipelineCommand> input, TimeProvider clock)
        => DispatchMappedFunctionEvent(input, clock, _eventMap);
}
