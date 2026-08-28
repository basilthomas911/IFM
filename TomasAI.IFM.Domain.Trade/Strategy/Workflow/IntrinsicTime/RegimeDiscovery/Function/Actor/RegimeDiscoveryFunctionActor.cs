using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Function.Extensions;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Function.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Function.Actor;

/// <summary>Executes and synchronously returns one completed or failed Regime Discovery pipeline result.</summary>
public sealed class RegimeDiscoveryFunctionActor(
    IFunctionActorContext<RegimeDiscoveryFunctionActor> actorContext)
    : BaseEventSourceFunctionActor<
        RegimeDiscoveryFunctionActor,
        ExecuteRegimeDiscoveryPipelineCommand,
        RegimeDiscoveryExecutionEntityId,
        IntrinsicTimeStrategyWorkflowEntityId,
        RegimeDiscoveryFunctionState,
        RegimeDiscoveryPipelineCompletedEvent,
        RegimeDiscoveryPipelineFailedEvent>(
            actorContext,
            Typed(actorContext).StateRepository,
            Typed(actorContext).FunctionProjector,
            Typed(actorContext).Logger)
{
    public const string ActorName = ExecuteRegimeDiscoveryPipelineCommand.Actor;

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ExecuteRegimeDiscoveryPipelineCommand>>
        _parseMap = new Dictionary<string, Func<IActorMessage, ExecuteRegimeDiscoveryPipelineCommand>>(
            StringComparer.Ordinal)
        {
            [ExecuteRegimeDiscoveryPipelineCommand.Verb] =
                message => message.AsCommand<ExecuteRegimeDiscoveryPipelineCommand>()!
        };

    static readonly IReadOnlyDictionary<Type, Func<ExecuteRegimeDiscoveryPipelineCommand, List<ValidationError>>>
        _validationMap = new Dictionary<Type, Func<ExecuteRegimeDiscoveryPipelineCommand, List<ValidationError>>>
        {
            [typeof(ExecuteRegimeDiscoveryPipelineCommand)] = request =>
                new List<ValidationError>()
                    .ValidateCommandId(request.CommandId, request.CommandName)
                    .ValidateEntityId(request.EntityId, request.CommandName)
                    .CaptureCommandValidation(() => Validate(request))
        };

    static readonly IReadOnlyDictionary<Type, Func<
        ExecuteRegimeDiscoveryPipelineCommand,
        IFunctionActorContext<RegimeDiscoveryFunctionActor>,
        CancellationToken,
        ValueTask<FunctionResult<RegimeDiscoveryPipelineCompletedEvent, RegimeDiscoveryPipelineFailedEvent>>>>
        _receiveMap = new Dictionary<Type, Func<
            ExecuteRegimeDiscoveryPipelineCommand,
            IFunctionActorContext<RegimeDiscoveryFunctionActor>,
            CancellationToken,
            ValueTask<FunctionResult<RegimeDiscoveryPipelineCompletedEvent, RegimeDiscoveryPipelineFailedEvent>>>>
        {
            [typeof(ExecuteRegimeDiscoveryPipelineCommand)] =
                (request, context, cancellationToken) => request.ExecuteAsync(context, cancellationToken)
        };

    IRegimeDiscoveryFunctionContext ActorContext => Typed(Context);

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
        cancellationToken.ThrowIfCancellationRequested();
        if (!_validationMap.TryGetValue(request.GetType(), out var validator))
            throw new InvalidOperationException($"No validation is registered for {request.GetType().Name}.");
        var errors = validator(request);
        if (errors.Count != 0)
            throw new CommandValidationException(
                request.ErrorCode,
                string.Join(Environment.NewLine, errors.Select(error => error.ErrorMessage)));
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
        return receive(request, context, cancellationToken);
    }

    protected override RegimeDiscoveryPipelineFailedEvent CreateConflictFailedEvent(
        ExecuteRegimeDiscoveryPipelineCommand request)
        => ExecuteRegimeDiscoveryPipeline.CreateFailedEvent(
            request,
            RegimeDiscoveryPipelineFailedEvent.ErrorId,
            "A conflicting completed Regime Discovery input already exists for this execution.",
            "FunctionConflict",
            string.Empty,
            ActorContext.TimeProvider.GetUtcNow().UtcDateTime);

    protected override RegimeDiscoveryPipelineFailedEvent CreateFailedEvent(
        ExecuteRegimeDiscoveryPipelineCommand? request,
        Exception exception,
        FunctionFailureStage stage)
    {
        var now = ActorContext.TimeProvider.GetUtcNow().UtcDateTime;
        if (request is null)
        {
            return new RegimeDiscoveryPipelineFailedEvent
            {
                Subject = new ActorSubject(ActorType.Function, ActorName,
                    RegimeDiscoveryPipelineFailedEvent.Verb, string.Empty),
                Id = Guid.CreateVersion7(new DateTimeOffset(now, TimeSpan.Zero)),
                ErrorDate = now,
                ReceivedOn = now,
                ErrorCode = RegimeDiscoveryPipelineFailedEvent.ErrorId,
                ErrorMessage = "Regime Discovery Function request could not be processed.",
                ErrorType = ErrorType.Command,
                ErrorData = stage.ToString(),
                EventSource = $"{ActorName}Actor",
                CommandName = nameof(ExecuteRegimeDiscoveryPipelineCommand),
                PipelineStage = Shared.Strategy.Workflow.IntrinsicTime.Model.StrategyWorkflowStage.RegimeDiscovery
            };
        }

        return ExecuteRegimeDiscoveryPipeline.CreateFailedEvent(
            request,
            request.ErrorCode,
            stage == FunctionFailureStage.Projection
                ? "Regime Discovery result projection failed."
                : stage == FunctionFailureStage.Persistence
                    ? "Regime Discovery completed state could not be persisted."
                    : "Regime Discovery Function execution failed.",
            stage.ToString(),
            exception.GetType().Name,
            now);
    }

    static IRegimeDiscoveryFunctionContext Typed(
        IFunctionActorContext<RegimeDiscoveryFunctionActor> context)
        => context as IRegimeDiscoveryFunctionContext
           ?? throw new ArgumentException(
               $"{nameof(context)} must implement {nameof(IRegimeDiscoveryFunctionContext)}.",
               nameof(context));

    static void Validate(ExecuteRegimeDiscoveryPipelineCommand command)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(command.Subject.EntityId))
            errors.Add("Subject.EntityId is required.");
        if (command.Subject.ActorType != ActorType.Function)
            errors.Add("Subject.ActorType must be Function.");
        if (command.InputWorkflowRevision <= 0)
            errors.Add("InputWorkflowRevision must be positive.");
        errors.AddRange(new RegimeDiscoveryExecutionEntityIdValidationRules().Execute(command.EntityId)
            .Select(value => value.ErrorMessage));
        if (!string.Equals(command.Subject.EntityId, command.EntityId.Format(), StringComparison.Ordinal))
            errors.Add("Subject.EntityId must match the composite Regime Discovery execution identity.");
        if (command.WorkflowView.EntityId != command.WorkflowEntityId ||
            command.WorkflowView.WorkflowId != command.WorkflowId ||
            command.WorkflowView.WorkflowRevision != command.InputWorkflowRevision)
            errors.Add("WorkflowView identity and revision must match the Regime Discovery execution.");
        if (command.ExpiresAtUtc <= command.RequestedAtUtc)
            errors.Add("ExpiresAtUtc must be later than RequestedAtUtc.");
        errors.AddRange(new RegimeDiscoveryParameterSetValidationRules().Execute(command.ParameterSet)
            .Select(value => value.ErrorMessage));
        if (command.TargetHorizon != command.ParameterSet.TargetHorizon ||
            command.TargetHorizon != command.TriggerEvent.EntityId.TimePeriod ||
            command.WorkflowEntityId.ItiSignalEntityId != command.TriggerEvent.EntityId)
            errors.Add("TargetHorizon must match the parameter set and trigger ITI timeframe.");
        if (!IsSha256(command.ParameterPayloadSha256))
            errors.Add("ParameterPayloadSha256 must contain exactly 64 hexadecimal characters.");
        else if (!string.Equals(command.ParameterPayloadSha256,
                     RegimeDiscoveryParameterPayload.ComputeSha256(command.ParameterSet),
                     StringComparison.OrdinalIgnoreCase))
            errors.Add("ParameterPayloadSha256 must match the canonical parameter payload.");
        if (errors.Count != 0)
            throw new ArgumentException(string.Join("; ", errors), nameof(command));
    }

    static bool IsSha256(string value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
}
