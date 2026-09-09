using System.Collections.Frozen;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Function;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Function.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Function.Actor;

/// <summary>Mapped, completed-only composition Function with deadline-bounded lifecycle stages.</summary>
public sealed class RiskManagementFunctionActor(IRiskManagementFunctionContext actorContext)
    : BaseEventSourceFunctionActor<RiskManagementFunctionActor, ExecuteRiskManagementPipelineCommand,
        RiskManagementExecutionId, IntrinsicTimeStrategyWorkflowEntityId, RiskManagementFunctionState,
        RiskManagementFunctionCompletedEvent, RiskManagementFunctionFailedEvent>(
            actorContext ?? throw new ArgumentNullException(nameof(actorContext)),
            actorContext.StateRepository, actorContext.FunctionProjector, actorContext.Logger)
{
    readonly IRiskManagementFunctionContext _context = actorContext;

    public const string ActorName = ExecuteRiskManagementPipelineCommand.Actor;

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ExecuteRiskManagementPipelineCommand>> _parseMap =
        new Dictionary<string, Func<IActorMessage, ExecuteRiskManagementPipelineCommand>>(StringComparer.Ordinal)
        {
            [ExecuteRiskManagementPipelineCommand.Verb] = static message => message.AsCommand<ExecuteRiskManagementPipelineCommand>()!
        }.ToFrozenDictionary(StringComparer.Ordinal);

    readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>
        {
            [typeof(ExecuteRiskManagementPipelineCommand)] = command =>
            {
                var request = (ExecuteRiskManagementPipelineCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(request.CommandId, request.CommandName)
                    .ValidateRiskFields(request);
            }
        }.ToFrozenDictionary();

    static readonly IReadOnlyDictionary<Type, Func<ExecuteRiskManagementPipelineCommand,
        IRiskManagementFunctionContext,
        Func<FunctionEventContext<ExecuteRiskManagementPipelineCommand>, FunctionResult<RiskManagementFunctionCompletedEvent, RiskManagementFunctionFailedEvent>>, CancellationToken,
        ValueTask<FunctionResult<RiskManagementFunctionCompletedEvent, RiskManagementFunctionFailedEvent>>>> _receiveMap =
        new Dictionary<Type, Func<ExecuteRiskManagementPipelineCommand, IRiskManagementFunctionContext,
        Func<FunctionEventContext<ExecuteRiskManagementPipelineCommand>, FunctionResult<RiskManagementFunctionCompletedEvent, RiskManagementFunctionFailedEvent>>, CancellationToken,
            ValueTask<FunctionResult<RiskManagementFunctionCompletedEvent, RiskManagementFunctionFailedEvent>>>>
        {
            [typeof(ExecuteRiskManagementPipelineCommand)] = static (request, context, dispatchEvent, token) => request.ExecuteAsync(context, dispatchEvent, token)
        }.ToFrozenDictionary();

    static readonly IReadOnlyDictionary<Type, Func<ExecuteRiskManagementPipelineCommand, FunctionFailureStage,
        IRiskManagementFunctionContext, FunctionExecutionPolicy>> _executionPolicyMap =
        new Dictionary<Type, Func<ExecuteRiskManagementPipelineCommand, FunctionFailureStage,
            IRiskManagementFunctionContext, FunctionExecutionPolicy>>
        {
            [typeof(ExecuteRiskManagementPipelineCommand)] = static (request, stage, context) => request.ResolveExecutionPolicy(stage, context)
        }.ToFrozenDictionary();

    /// <summary>Maps lifecycle policy requests without interpreting actor-specific deadlines or settings.</summary>
    protected override FunctionExecutionPolicy ResolveExecutionPolicy(ExecuteRiskManagementPipelineCommand request, FunctionFailureStage stage)
        => DispatchMappedExecutionPolicy(request, stage, _context, _executionPolicyMap);

    protected override ExecuteRiskManagementPipelineCommand ParseMessage(
        IFunctionActorContext<RiskManagementFunctionActor> context, IActorMessage message)
        => ParseMappedFunction(context, message, _parseMap);

    protected override ValueTask ValidateAsync(IFunctionActorContext<RiskManagementFunctionActor> context,
        ActorThreadId threadId, ExecuteRiskManagementPipelineCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        ValidateMappedCommand(request, _validationMap);
        return ValueTask.CompletedTask;
    }

    static readonly IReadOnlyDictionary<Type, Func<FunctionEventContext<ExecuteRiskManagementPipelineCommand>, TimeProvider,
        FunctionResult<RiskManagementFunctionCompletedEvent, RiskManagementFunctionFailedEvent>>> _eventMap =
        new Dictionary<Type, Func<FunctionEventContext<ExecuteRiskManagementPipelineCommand>, TimeProvider,
            FunctionResult<RiskManagementFunctionCompletedEvent, RiskManagementFunctionFailedEvent>>>
        {
            [typeof(RiskManagementFunctionCompletedEvent)] = static (input, clock) => input.Complete(clock),
            [typeof(RiskManagementFunctionFailedEvent)] = static (input, clock) => input.Fail(clock)
        }.ToFrozenDictionary();

    /// <summary>Dispatches the request through the receive map with the shared terminal-event callback.</summary>
    protected override ValueTask<FunctionResult<RiskManagementFunctionCompletedEvent, RiskManagementFunctionFailedEvent>> ExecuteFunctionAsync(
        IFunctionActorContext<RiskManagementFunctionActor> context, RiskManagementFunctionState state,
        ExecuteRiskManagementPipelineCommand request, CancellationToken cancellationToken)
        => ResolveMappedFunctionHandler(request, _receiveMap)(request, _context,
            input => HandleFunctionEvent(context, input), cancellationToken);

    /// <summary>Dispatches lifecycle failures and execution outcomes through the exact terminal-event map.</summary>
    protected override FunctionResult<RiskManagementFunctionCompletedEvent, RiskManagementFunctionFailedEvent> HandleFunctionEvent(
        IFunctionActorContext<RiskManagementFunctionActor> context, FunctionEventContext<ExecuteRiskManagementPipelineCommand> input)
        => MapEvent(input, _context.TimeProvider);

    /// <summary>Resolves terminal handlers for Function outcomes and workflow transport failures.</summary>
    internal static FunctionResult<RiskManagementFunctionCompletedEvent, RiskManagementFunctionFailedEvent> MapEvent(
        FunctionEventContext<ExecuteRiskManagementPipelineCommand> input, TimeProvider clock)
        => DispatchMappedFunctionEvent(input, clock, _eventMap);
}
