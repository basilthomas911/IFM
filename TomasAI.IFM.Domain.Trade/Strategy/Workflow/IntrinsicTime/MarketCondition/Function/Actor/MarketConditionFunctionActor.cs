using System.Collections.Frozen;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Function;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Function.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Function.Actor;

/// <summary>Mapped, completed-only assessment Function with deadline-bounded lifecycle stages.</summary>
public sealed class MarketConditionFunctionActor(IMarketConditionFunctionContext actorContext)
    : BaseEventSourceFunctionActor<MarketConditionFunctionActor, ExecuteMarketConditionAssessmentCommand,
        MarketConditionAssessmentExecutionId, IntrinsicTimeStrategyWorkflowEntityId, MarketConditionAssessmentState,
        MarketConditionAssessmentCompletedEvent, MarketConditionAssessmentFailedEvent>(
            actorContext ?? throw new ArgumentNullException(nameof(actorContext)),
            actorContext.StateRepository, actorContext.FunctionProjector, actorContext.Logger)
{
    readonly IMarketConditionFunctionContext _context = actorContext;

    public const string ActorName = ExecuteMarketConditionAssessmentCommand.Actor;

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ExecuteMarketConditionAssessmentCommand>> _parseMap =
        new Dictionary<string, Func<IActorMessage, ExecuteMarketConditionAssessmentCommand>>(StringComparer.Ordinal)
        {
            [ExecuteMarketConditionAssessmentCommand.Verb] = static message => message.AsCommand<ExecuteMarketConditionAssessmentCommand>()!
        }.ToFrozenDictionary(StringComparer.Ordinal);

    static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>
        {
            [typeof(ExecuteMarketConditionAssessmentCommand)] = static command =>
            {
                var request = (ExecuteMarketConditionAssessmentCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(request.CommandId, request.CommandName)
                    .ValidateAssessmentIdentity(request.EntityId)
                    .ValidateAssessmentRevision(request.InputWorkflowRevision)
                    .ValidateAssessmentWorkflow(request.WorkflowView)
                    .ValidateAssessmentTrigger(request.TriggerEvent)
                    .ValidateAssessmentTraceId(request.CorrelationId, nameof(request.CorrelationId))
                    .ValidateAssessmentTraceId(request.CausationId, nameof(request.CausationId))
                    .ValidateAssessmentTimestamp(request.RequestedAtUtc, nameof(request.RequestedAtUtc))
                    .ValidateAssessmentTimestamp(request.ExpiresAtUtc, nameof(request.ExpiresAtUtc))
                    .ValidateAssessmentParameters(request.ParameterSet)
                    .ValidateAssessmentHash(request.ParameterPayloadSha256, nameof(request.ParameterPayloadSha256))
                    .ValidateAssessmentHash(request.RegimePayloadSha256, nameof(request.RegimePayloadSha256))
                    .ValidateAssessmentHorizon(request.TargetHorizon)
                    .ValidateAssessmentUpstream(request.RegimeResultEnvelope, request.WorkflowView)
                    .ValidateAssessmentConsistency(request);
            }
        }.ToFrozenDictionary();

    static readonly IReadOnlyDictionary<Type, Func<ExecuteMarketConditionAssessmentCommand,
        IMarketConditionFunctionContext,
        Func<FunctionEventContext<ExecuteMarketConditionAssessmentCommand>, FunctionResult<MarketConditionAssessmentCompletedEvent, MarketConditionAssessmentFailedEvent>>, CancellationToken,
        ValueTask<FunctionResult<MarketConditionAssessmentCompletedEvent, MarketConditionAssessmentFailedEvent>>>> _receiveMap =
        new Dictionary<Type, Func<ExecuteMarketConditionAssessmentCommand, IMarketConditionFunctionContext,
        Func<FunctionEventContext<ExecuteMarketConditionAssessmentCommand>, FunctionResult<MarketConditionAssessmentCompletedEvent, MarketConditionAssessmentFailedEvent>>, CancellationToken,
            ValueTask<FunctionResult<MarketConditionAssessmentCompletedEvent, MarketConditionAssessmentFailedEvent>>>>
        {
            [typeof(ExecuteMarketConditionAssessmentCommand)] = static (request, context, dispatchEvent, token) => request.ExecuteAsync(context, dispatchEvent, token)
        }.ToFrozenDictionary();

    static readonly IReadOnlyDictionary<Type, Func<ExecuteMarketConditionAssessmentCommand, FunctionFailureStage,
        IMarketConditionFunctionContext, FunctionExecutionPolicy>> _executionPolicyMap =
        new Dictionary<Type, Func<ExecuteMarketConditionAssessmentCommand, FunctionFailureStage,
            IMarketConditionFunctionContext, FunctionExecutionPolicy>>
        {
            [typeof(ExecuteMarketConditionAssessmentCommand)] = static (request, stage, context) => request.ResolveExecutionPolicy(stage, context)
        }.ToFrozenDictionary();

    /// <summary>Maps lifecycle policy requests without interpreting actor-specific deadlines or settings.</summary>
    protected override FunctionExecutionPolicy ResolveExecutionPolicy(ExecuteMarketConditionAssessmentCommand request, FunctionFailureStage stage)
        => DispatchMappedExecutionPolicy(request, stage, _context, _executionPolicyMap);

    protected override ExecuteMarketConditionAssessmentCommand ParseMessage(
        IFunctionActorContext<MarketConditionFunctionActor> context, IActorMessage message)
        => ParseMappedFunction(context, message, _parseMap);

    protected override ValueTask ValidateAsync(IFunctionActorContext<MarketConditionFunctionActor> context,
        ActorThreadId threadId, ExecuteMarketConditionAssessmentCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        ValidateMappedCommand(request, _validationMap);
        return ValueTask.CompletedTask;
    }

    static readonly IReadOnlyDictionary<Type, Func<FunctionEventContext<ExecuteMarketConditionAssessmentCommand>, TimeProvider,
        FunctionResult<MarketConditionAssessmentCompletedEvent, MarketConditionAssessmentFailedEvent>>> _eventMap =
        new Dictionary<Type, Func<FunctionEventContext<ExecuteMarketConditionAssessmentCommand>, TimeProvider,
            FunctionResult<MarketConditionAssessmentCompletedEvent, MarketConditionAssessmentFailedEvent>>>
        {
            [typeof(MarketConditionAssessmentCompletedEvent)] = static (input, clock) => input.Complete(clock),
            [typeof(MarketConditionAssessmentFailedEvent)] = static (input, clock) => input.Fail(clock)
        }.ToFrozenDictionary();

    /// <summary>Dispatches the request through the receive map with the shared terminal-event callback.</summary>
    protected override ValueTask<FunctionResult<MarketConditionAssessmentCompletedEvent, MarketConditionAssessmentFailedEvent>> ExecuteFunctionAsync(
        IFunctionActorContext<MarketConditionFunctionActor> context, MarketConditionAssessmentState state,
        ExecuteMarketConditionAssessmentCommand request, CancellationToken cancellationToken)
        => ResolveMappedFunctionHandler(request, _receiveMap)(request, _context,
            input => HandleFunctionEvent(context, input), cancellationToken);

    /// <summary>Dispatches lifecycle failures and execution outcomes through the exact terminal-event map.</summary>
    protected override FunctionResult<MarketConditionAssessmentCompletedEvent, MarketConditionAssessmentFailedEvent> HandleFunctionEvent(
        IFunctionActorContext<MarketConditionFunctionActor> context, FunctionEventContext<ExecuteMarketConditionAssessmentCommand> input)
        => MapEvent(input, _context.TimeProvider);

    /// <summary>Resolves terminal handlers for Function outcomes and workflow transport failures.</summary>
    internal static FunctionResult<MarketConditionAssessmentCompletedEvent, MarketConditionAssessmentFailedEvent> MapEvent(
        FunctionEventContext<ExecuteMarketConditionAssessmentCommand> input, TimeProvider clock)
        => DispatchMappedFunctionEvent(input, clock, _eventMap);
}
