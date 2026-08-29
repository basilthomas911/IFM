using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Function.Extensions;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Function.State;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Function.Actor;

public sealed class MarketConditionFunctionActor(IFunctionActorContext<MarketConditionFunctionActor> actorContext)
    : BaseEventSourceFunctionActor<MarketConditionFunctionActor, ExecuteMarketConditionPipelineCommand,
        MarketConditionExecutionEntityId, IntrinsicTimeStrategyWorkflowEntityId, MarketConditionFunctionState,
        MarketConditionPipelineCompletedEvent, MarketConditionPipelineFailedEvent>(actorContext,
            Typed(actorContext).StateRepository, Typed(actorContext).FunctionProjector, Typed(actorContext).Logger)
{
    public const string ActorName = ExecuteMarketConditionPipelineCommand.Actor;
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ExecuteMarketConditionPipelineCommand>> _parseMap =
        new Dictionary<string, Func<IActorMessage, ExecuteMarketConditionPipelineCommand>>(StringComparer.Ordinal)
        { [ExecuteMarketConditionPipelineCommand.Verb] = x => x.AsCommand<ExecuteMarketConditionPipelineCommand>()! };
    static readonly IReadOnlyDictionary<Type, Func<ExecuteMarketConditionPipelineCommand, List<ValidationError>>> _validationMap =
        new Dictionary<Type, Func<ExecuteMarketConditionPipelineCommand, List<ValidationError>>>
        { [typeof(ExecuteMarketConditionPipelineCommand)] = x => new List<ValidationError>()
            .ValidateCommandId(x.CommandId, x.CommandName).ValidateEntityId(x.EntityId, x.CommandName)
            .CaptureCommandValidation(() => Validate(x)) };
    static readonly IReadOnlyDictionary<Type, Func<ExecuteMarketConditionPipelineCommand,
        IFunctionActorContext<MarketConditionFunctionActor>, CancellationToken,
        ValueTask<FunctionResult<MarketConditionPipelineCompletedEvent, MarketConditionPipelineFailedEvent>>>> _receiveMap =
        new Dictionary<Type, Func<ExecuteMarketConditionPipelineCommand,
            IFunctionActorContext<MarketConditionFunctionActor>, CancellationToken,
            ValueTask<FunctionResult<MarketConditionPipelineCompletedEvent, MarketConditionPipelineFailedEvent>>>>
        { [typeof(ExecuteMarketConditionPipelineCommand)] = (x, c, t) => x.ExecuteAsync(c, t) };

    protected override ExecuteMarketConditionPipelineCommand ParseMessage(
        IFunctionActorContext<MarketConditionFunctionActor> context, IActorMessage message)
        => ParseMappedFunction(context, message, _parseMap);
    protected override ValueTask ValidateAsync(IFunctionActorContext<MarketConditionFunctionActor> context,
        ActorThreadId threadId, ExecuteMarketConditionPipelineCommand request, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!_validationMap.TryGetValue(request.GetType(), out var validate))
            throw new InvalidOperationException($"No validation is registered for {request.GetType().Name}.");
        var errors = validate(request);
        if (errors.Count != 0) throw new CommandValidationException(request.ErrorCode,
            string.Join(Environment.NewLine, errors.Select(x => x.ErrorMessage)));
        return ValueTask.CompletedTask;
    }
    protected override ValueTask<FunctionResult<MarketConditionPipelineCompletedEvent,
        MarketConditionPipelineFailedEvent>> ExecuteFunctionAsync(
        IFunctionActorContext<MarketConditionFunctionActor> context, MarketConditionFunctionState state,
        ExecuteMarketConditionPipelineCommand request, CancellationToken token)
        => ResolveMappedFunctionHandler(request, _receiveMap)(request, context, token);
    protected override MarketConditionPipelineFailedEvent CreateConflictFailedEvent(
        ExecuteMarketConditionPipelineCommand request) => ExecuteMarketConditionPipeline.CreateFailedEvent(request,
            MarketConditionFailureCategory.ContractInvalid, MarketConditionReasonCodes.ContractInvalid,
            "A conflicting completed Market Condition input already exists for this execution.",
            Typed(Context).TimeProvider.GetUtcNow().UtcDateTime);
    protected override MarketConditionPipelineFailedEvent CreateFailedEvent(
        ExecuteMarketConditionPipelineCommand? request, Exception exception, FunctionFailureStage stage)
    {
        var now = Typed(Context).TimeProvider.GetUtcNow().UtcDateTime;
        if (request is null) return new()
        {
            Subject = new ActorSubject(ActorType.Function, ActorName, MarketConditionPipelineFailedEvent.Verb, string.Empty),
            Id = Guid.CreateVersion7(new DateTimeOffset(now, TimeSpan.Zero)), ErrorDate = now, ReceivedOn = now,
            ErrorCode = MarketConditionPipelineFailedEvent.ErrorId, ErrorMessage = "Market Condition Function request could not be processed.",
            ErrorType = ErrorType.Command, ErrorData = stage.ToString(), EventSource = $"{ActorName}Actor",
            CommandName = nameof(ExecuteMarketConditionPipelineCommand), PipelineStage = StrategyWorkflowStage.MarketCondition,
            FailureCategory = MarketConditionFailureCategory.ContractInvalid
        };
        var category = stage switch
        {
            FunctionFailureStage.Projection => MarketConditionFailureCategory.ProjectionFailed,
            FunctionFailureStage.Persistence => MarketConditionFailureCategory.PersistenceFailed,
            _ => MarketConditionFailureCategory.CalculationFailed
        };
        return ExecuteMarketConditionPipeline.CreateFailedEvent(request, category,
            stage == FunctionFailureStage.Projection ? MarketConditionReasonCodes.Projection :
            stage == FunctionFailureStage.Persistence ? MarketConditionReasonCodes.Persistence :
            MarketConditionReasonCodes.Calculation,
            stage == FunctionFailureStage.Validation
                ? $"Market Condition validation failed: {exception.Message}"
                : $"Market Condition {stage.ToString().ToLowerInvariant()} failed: {exception.GetType().Name}.", now);
    }

    static void Validate(ExecuteMarketConditionPipelineCommand c)
    {
        var errors = new List<string>();
        if (c.Subject.ActorType != ActorType.Function) errors.Add("Subject.ActorType must be Function.");
        errors.AddRange(new MarketConditionExecutionEntityIdValidationRules().Execute(c.EntityId).Select(x => x.ErrorMessage));
        if (!string.Equals(c.Subject.EntityId, c.EntityId.Format(), StringComparison.Ordinal)) errors.Add("Subject entity mismatch.");
        if (c.WorkflowView.EntityId != c.WorkflowEntityId || c.WorkflowView.WorkflowId != c.WorkflowId ||
            c.WorkflowView.WorkflowRevision != c.InputWorkflowRevision || c.WorkflowView.Status != WorkflowStrategyMachineStatus.Started ||
            c.WorkflowView.CurrentStage != StrategyWorkflowStage.MarketCondition) errors.Add("Workflow view is not the selected Market Condition revision.");
        if (c.RequestedAtUtc >= c.ExpiresAtUtc || c.ExpiresAtUtc > c.WorkflowView.ExpiresAtUtc ||
            c.ExpiresAtUtc > c.RequestedAtUtc.AddMilliseconds(c.ParameterSet.Execution.MaximumExecutionMilliseconds))
            errors.Add("Function deadline is invalid.");
        errors.AddRange(new MarketConditionParameterSetValidationRules().Execute(c.ParameterSet).Select(x => x.ErrorMessage));
        if (c.FundId != c.ParameterSet.FundId || c.FundId != c.WorkflowView.FundId || c.InstrumentRoot != "ES" ||
            c.TargetHorizon != c.ParameterSet.TargetHorizon || c.TargetHorizon != c.TriggerEvent.EntityId.TimePeriod)
            errors.Add("Fund, instrument, or horizon identity is inconsistent.");
        var computedParameterHash = MarketConditionParameterPayload.ComputeSha256(c.ParameterSet);
        if (c.ParameterPayloadSha256.Length != 64 || !c.ParameterPayloadSha256.All(Uri.IsHexDigit) ||
            !string.Equals(c.ParameterPayloadSha256, computedParameterHash, StringComparison.OrdinalIgnoreCase))
            errors.Add($"Parameter payload hash is invalid (expected {c.ParameterPayloadSha256}, computed {computedParameterHash}).");
        if (errors.Count != 0) throw new ArgumentException(string.Join("; ", errors), nameof(c));
    }
    static IMarketConditionFunctionContext Typed(IFunctionActorContext<MarketConditionFunctionActor> c)
        => c as IMarketConditionFunctionContext ?? throw new ArgumentException(
            $"{nameof(c)} must implement {nameof(IMarketConditionFunctionContext)}.", nameof(c));
}
