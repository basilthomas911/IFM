using System.Collections.Frozen;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Function;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Function.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Command.Validation;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Function.Actor;

/// <summary>Executes and synchronously returns one completed or failed Regime Discovery pipeline result.</summary>
public sealed class RegimeDiscoveryFunctionActor(
    IRegimeDiscoveryFunctionContext actorContext)
    : BaseEventSourceFunctionActor<
        RegimeDiscoveryFunctionActor,
        ExecuteRegimeDiscoveryPipelineCommand,
        RegimeDiscoveryExecutionEntityId,
        IntrinsicTimeStrategyWorkflowEntityId,
        RegimeDiscoveryFunctionState,
        RegimeDiscoveryPipelineCompletedEvent,
        RegimeDiscoveryPipelineFailedEvent>(
            actorContext ?? throw new ArgumentNullException(nameof(actorContext)),
            actorContext.StateRepository,
            actorContext.FunctionProjector,
            actorContext.Logger)
{
    readonly IRegimeDiscoveryFunctionContext _context = actorContext;

    public const string ActorName = ExecuteRegimeDiscoveryPipelineCommand.Actor;

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ExecuteRegimeDiscoveryPipelineCommand>>
        _parseMap = new Dictionary<string, Func<IActorMessage, ExecuteRegimeDiscoveryPipelineCommand>>(
            StringComparer.Ordinal)
        {
            [ExecuteRegimeDiscoveryPipelineCommand.Verb] =
                message => message.AsCommand<ExecuteRegimeDiscoveryPipelineCommand>()!
        }.ToFrozenDictionary(StringComparer.Ordinal);

    static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>>
        _validationMap = new Dictionary<Type, Func<ICommand, List<ValidationError>>>
        {
            [typeof(ExecuteRegimeDiscoveryPipelineCommand)] = command =>
            {
                var request = (ExecuteRegimeDiscoveryPipelineCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(request.CommandId, request.CommandName)
                    .ValidateRegimeDiscoveryExecutionEntityId(request.EntityId)
                    .ValidateRegimeDiscoveryRevision(request.InputWorkflowRevision)
                    .ValidateRegimeDiscoveryWorkflowView(request.WorkflowView)
                    .ValidateRegimeDiscoveryTrigger(request.TriggerEvent)
                    .ValidateRegimeDiscoveryTraceId(request.CorrelationId, nameof(request.CorrelationId))
                    .ValidateRegimeDiscoveryTraceId(request.CausationId, nameof(request.CausationId))
                    .ValidateRegimeDiscoveryTimestamp(request.RequestedAtUtc, nameof(request.RequestedAtUtc))
                    .ValidateRegimeDiscoveryTimestamp(request.ExpiresAtUtc, nameof(request.ExpiresAtUtc))
                    .ValidateRegimeDiscoveryParameterSet(request.ParameterSet)
                    .ValidateRegimeDiscoveryParameterHash(request.ParameterPayloadSha256)
                    .ValidateRegimeDiscoveryTargetHorizon(request.TargetHorizon)
                    .ValidateRegimeDiscoveryConsistency(request);
            }
        }.ToFrozenDictionary();

    static readonly IReadOnlyDictionary<Type, Func<
        ExecuteRegimeDiscoveryPipelineCommand,
        IRegimeDiscoveryFunctionContext,
        Func<FunctionEventContext<ExecuteRegimeDiscoveryPipelineCommand>, FunctionResult<RegimeDiscoveryPipelineCompletedEvent, RegimeDiscoveryPipelineFailedEvent>>,
        CancellationToken,
        ValueTask<FunctionResult<RegimeDiscoveryPipelineCompletedEvent, RegimeDiscoveryPipelineFailedEvent>>>>
        _receiveMap = new Dictionary<Type, Func<
            ExecuteRegimeDiscoveryPipelineCommand,
            IRegimeDiscoveryFunctionContext,
            Func<FunctionEventContext<ExecuteRegimeDiscoveryPipelineCommand>, FunctionResult<RegimeDiscoveryPipelineCompletedEvent, RegimeDiscoveryPipelineFailedEvent>>,
            CancellationToken,
            ValueTask<FunctionResult<RegimeDiscoveryPipelineCompletedEvent, RegimeDiscoveryPipelineFailedEvent>>>>
        {
            [typeof(ExecuteRegimeDiscoveryPipelineCommand)] =
                (request, context, dispatchEvent, cancellationToken) => request.ExecuteAsync(context, dispatchEvent, cancellationToken)
        }.ToFrozenDictionary();

    static readonly IReadOnlyDictionary<Type, Func<FunctionEventContext<ExecuteRegimeDiscoveryPipelineCommand>, TimeProvider,
        FunctionResult<RegimeDiscoveryPipelineCompletedEvent, RegimeDiscoveryPipelineFailedEvent>>> _eventMap =
        new Dictionary<Type, Func<FunctionEventContext<ExecuteRegimeDiscoveryPipelineCommand>, TimeProvider,
            FunctionResult<RegimeDiscoveryPipelineCompletedEvent, RegimeDiscoveryPipelineFailedEvent>>>
        {
            [typeof(RegimeDiscoveryPipelineCompletedEvent)] = (input, _) => input.Complete(),
            [typeof(RegimeDiscoveryPipelineFailedEvent)] = (input, clock) => input.Fail(clock)
        }.ToFrozenDictionary();

    static readonly IReadOnlyDictionary<Type, Func<ExecuteRegimeDiscoveryPipelineCommand, FunctionFailureStage,
        IRegimeDiscoveryFunctionContext, FunctionExecutionPolicy>> _executionPolicyMap =
        new Dictionary<Type, Func<ExecuteRegimeDiscoveryPipelineCommand, FunctionFailureStage,
            IRegimeDiscoveryFunctionContext, FunctionExecutionPolicy>>
        {
            [typeof(ExecuteRegimeDiscoveryPipelineCommand)] = static (request, stage, context) => request.ResolveExecutionPolicy(stage, context)
        }.ToFrozenDictionary();

    /// <summary>Maps lifecycle policy requests without interpreting actor-specific deadlines or settings.</summary>
    protected override FunctionExecutionPolicy ResolveExecutionPolicy(ExecuteRegimeDiscoveryPipelineCommand request, FunctionFailureStage stage)
        => DispatchMappedExecutionPolicy(request, stage, _context, _executionPolicyMap);

    protected override ExecuteRegimeDiscoveryPipelineCommand ParseMessage(
        IFunctionActorContext<RegimeDiscoveryFunctionActor> context,
        IActorMessage message)
        => ParseMappedFunction(context, message, _parseMap);

    protected override ValueTask ValidateAsync(
        IFunctionActorContext<RegimeDiscoveryFunctionActor> context,
        ActorThreadId threadId,
        ExecuteRegimeDiscoveryPipelineCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        ValidateMappedCommand(request, _validationMap);
        return ValueTask.CompletedTask;
    }

    protected override ValueTask<FunctionResult<
        RegimeDiscoveryPipelineCompletedEvent,
        RegimeDiscoveryPipelineFailedEvent>> ExecuteFunctionAsync(
        IFunctionActorContext<RegimeDiscoveryFunctionActor> context,
        RegimeDiscoveryFunctionState state,
        ExecuteRegimeDiscoveryPipelineCommand request,
        CancellationToken cancellationToken)
    {
        var receive = ResolveMappedFunctionHandler(request, _receiveMap);
        return receive(request, _context, input => HandleFunctionEvent(context, input), cancellationToken);
    }

    /// <summary>Routes lifecycle failures and calculation outcomes through the exact-type event map.</summary>
    /// <param name="context">The framework lifecycle context; the execution clock comes from the injected domain context.</param>
    /// <param name="input">The target terminal event and the information needed by its extension handler.</param>
    /// <returns>The completed candidate or non-durable failed response produced by the mapped extension.</returns>
    protected override FunctionResult<RegimeDiscoveryPipelineCompletedEvent, RegimeDiscoveryPipelineFailedEvent> HandleFunctionEvent(
        IFunctionActorContext<RegimeDiscoveryFunctionActor> context,
        FunctionEventContext<ExecuteRegimeDiscoveryPipelineCommand> input)
        => MapEvent(input, _context.TimeProvider);

    /// <summary>Resolves terminal-event handlers for Function execution and workflow transport failures.</summary>
    /// <param name="input">The exact event type and its outcome or lifecycle failure details.</param>
    /// <param name="timeProvider">The clock used by failure handlers.</param>
    /// <returns>The single terminal value produced by the mapped extension.</returns>
    internal static FunctionResult<RegimeDiscoveryPipelineCompletedEvent, RegimeDiscoveryPipelineFailedEvent> MapEvent(
        FunctionEventContext<ExecuteRegimeDiscoveryPipelineCommand> input, TimeProvider timeProvider)
        => DispatchMappedFunctionEvent(input, timeProvider, _eventMap);

}
