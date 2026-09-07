using System.Collections.Frozen;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Function.Extensions;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Function.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Function.Actor;

/// <summary>Mapped, completed-only assessment Function with deadline-bounded lifecycle stages.</summary>
public sealed class MarketConditionFunctionActor(IFunctionActorContext<MarketConditionFunctionActor> actorContext)
    : BaseEventSourceFunctionActor<MarketConditionFunctionActor, ExecuteMarketConditionAssessmentCommand,
        MarketConditionAssessmentExecutionId, IntrinsicTimeStrategyWorkflowEntityId, MarketConditionAssessmentState,
        MarketConditionAssessmentCompletedEvent, MarketConditionAssessmentFailedEvent>(
            actorContext, Typed(actorContext).StateRepository, Typed(actorContext).FunctionProjector, Typed(actorContext).Logger)
{
    public const string ActorName = ExecuteMarketConditionAssessmentCommand.Actor;

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ExecuteMarketConditionAssessmentCommand>> _parseMap =
        new Dictionary<string, Func<IActorMessage, ExecuteMarketConditionAssessmentCommand>>(StringComparer.Ordinal)
        {
            [ExecuteMarketConditionAssessmentCommand.Verb] = static message =>
            {
                var request = message.AsCommand<ExecuteMarketConditionAssessmentCommand>()!;
                if (request is not null && message.Subject.EntityId != request.EntityId.Format())
                    throw new ArgumentException("Assessment transport subject mismatch.");
                return request!;
            }
        }.ToFrozenDictionary(StringComparer.Ordinal);

    static readonly IReadOnlyDictionary<Type, Func<ExecuteMarketConditionAssessmentCommand, List<ValidationError>>> _validationMap =
        new Dictionary<Type, Func<ExecuteMarketConditionAssessmentCommand, List<ValidationError>>>
        {
            [typeof(ExecuteMarketConditionAssessmentCommand)] = static request => new List<ValidationError>()
                .ValidateCommandId(request.CommandId, request.CommandName)
                .ValidateEntityId(request.EntityId, request.CommandName)
                .CaptureCommandValidation(() => MarketConditionAssessmentContracts.ValidateRequest(request))
        }.ToFrozenDictionary();

    static readonly IReadOnlyDictionary<Type, Func<ExecuteMarketConditionAssessmentCommand,
        IMarketConditionFunctionContext, CancellationToken,
        ValueTask<FunctionResult<MarketConditionAssessmentCompletedEvent, MarketConditionAssessmentFailedEvent>>>> _receiveMap =
        new Dictionary<Type, Func<ExecuteMarketConditionAssessmentCommand, IMarketConditionFunctionContext, CancellationToken,
            ValueTask<FunctionResult<MarketConditionAssessmentCompletedEvent, MarketConditionAssessmentFailedEvent>>>>
        {
            [typeof(ExecuteMarketConditionAssessmentCommand)] = static (request, context, token) => request.ExecuteAsync(context, token)
        }.ToFrozenDictionary();

    IMarketConditionFunctionContext ActorContext => Typed(Context);

    protected override ExecuteMarketConditionAssessmentCommand ParseMessage(
        IFunctionActorContext<MarketConditionFunctionActor> context, IActorMessage message)
        => ParseMappedFunction(context, message, _parseMap);

    protected override ValueTask ValidateAsync(IFunctionActorContext<MarketConditionFunctionActor> context,
        ActorThreadId threadId, ExecuteMarketConditionAssessmentCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_validationMap.TryGetValue(request.GetType(), out var validate))
            throw new InvalidOperationException($"No validation is registered for {request.GetType().Name}.");
        var errors = validate(request);
        if (errors.Count != 0)
            throw new CommandValidationException(request.ErrorCode, string.Join(Environment.NewLine, errors.Select(error => error.ErrorMessage)));
        return ValueTask.CompletedTask;
    }

    protected override ValueTask<MarketConditionAssessmentState> LoadFunctionStateAsync(
        ExecuteMarketConditionAssessmentCommand request, CancellationToken cancellationToken)
        // Replay is allowed after market expiry; the read still has a bounded execution budget.
        => WithinDeadlineAsync(ActorContext.TimeProvider.GetUtcNow().UtcDateTime.AddMilliseconds(request.ParameterSet.MaximumExecutionMilliseconds),
            token => base.LoadFunctionStateAsync(request, token), cancellationToken);

    protected override ValueTask<FunctionResult<MarketConditionAssessmentCompletedEvent, MarketConditionAssessmentFailedEvent>> ExecuteFunctionAsync(
        IFunctionActorContext<MarketConditionFunctionActor> context, MarketConditionAssessmentState state,
        ExecuteMarketConditionAssessmentCommand request, CancellationToken cancellationToken)
    {
        var receive = ResolveMappedFunctionHandler(request, _receiveMap);
        return WithinDeadlineAsync(request.ExpiresAtUtc, token => receive(request, ActorContext, token), cancellationToken);
    }

    protected override ValueTask ProjectFunctionResultAsync(ExecuteMarketConditionAssessmentCommand request,
        MarketConditionAssessmentCompletedEvent completed, CancellationToken cancellationToken)
        => WithinDeadlineAsync(request.ExpiresAtUtc, token => base.ProjectFunctionResultAsync(request, completed, token), cancellationToken);

    protected override async ValueTask SaveFunctionStateAsync(IFunctionActorContext<MarketConditionFunctionActor> context,
        ActorThreadId threadId, MarketConditionAssessmentState state, ExecuteMarketConditionAssessmentCommand request,
        MarketConditionAssessmentCompletedEvent completedEvent, CancellationToken cancellationToken)
    {
        await WithinDeadlineAsync(request.ExpiresAtUtc,
            token => base.SaveFunctionStateAsync(context, threadId, state, request, completedEvent, token), cancellationToken).ConfigureAwait(false);
        MarketConditionTelemetry.RecordAssessment(MarketConditionAssessmentContracts.ReadResult(completedEvent.Result),
            Math.Max(0, (ActorContext.TimeProvider.GetUtcNow().UtcDateTime - request.RequestedAtUtc).TotalMilliseconds));
    }

    protected override MarketConditionAssessmentFailedEvent CreateConflictFailedEvent(ExecuteMarketConditionAssessmentCommand request)
        => ExecuteMarketConditionAssessment.CreateFailedEvent(request, MarketConditionFailureCategory.ContractInvalid,
            "MC.ASSESSMENT.CONFLICTING_DUPLICATE", ActorContext.TimeProvider);

    protected override MarketConditionAssessmentFailedEvent CreateFailedEvent(ExecuteMarketConditionAssessmentCommand? request,
        Exception exception, FunctionFailureStage stage)
    {
        var category = exception is TimeoutException ? MarketConditionFailureCategory.Timeout : stage switch
        {
            FunctionFailureStage.Loading or FunctionFailureStage.Persistence => MarketConditionFailureCategory.PersistenceFailed,
            FunctionFailureStage.Projection => MarketConditionFailureCategory.ProjectionFailed,
            FunctionFailureStage.Execution => MarketConditionFailureCategory.CalculationFailed,
            _ => MarketConditionFailureCategory.ContractInvalid
        };
        var reason = $"MC.ASSESSMENT.{category.ToString().ToUpperInvariant()}";
        if (request is not null)
            MarketConditionTelemetry.RecordFailure(category, reason, request.TargetHorizon,
                Math.Max(0, (ActorContext.TimeProvider.GetUtcNow().UtcDateTime - request.RequestedAtUtc).TotalMilliseconds));
        return ExecuteMarketConditionAssessment.CreateFailedEvent(request, category, reason, ActorContext.TimeProvider);
    }

    /// <summary>Bounds one lifecycle operation and observes a dependency that finishes after cancellation.</summary>
    async ValueTask<T> WithinDeadlineAsync<T>(DateTime deadline, Func<CancellationToken, ValueTask<T>> operation, CancellationToken callerToken)
    {
        callerToken.ThrowIfCancellationRequested();
        var clock = ActorContext.TimeProvider;
        var remaining = deadline - clock.GetUtcNow().UtcDateTime;
        if (remaining <= TimeSpan.Zero) throw new TimeoutException();
        using var workerCancellation = CancellationTokenSource.CreateLinkedTokenSource(callerToken);
        var task = operation(workerCancellation.Token).AsTask();
        try
        {
            var result = await task.WaitAsync(remaining, clock, callerToken).ConfigureAwait(false);
            callerToken.ThrowIfCancellationRequested();
            if (clock.GetUtcNow().UtcDateTime >= deadline) throw new TimeoutException();
            return result;
        }
        catch
        {
            workerCancellation.Cancel();
            _ = ObserveAsync(task);
            throw;
        }
    }

    /// <summary>Applies the same deadline to projection and completed append.</summary>
    async ValueTask WithinDeadlineAsync(DateTime deadline, Func<CancellationToken, ValueTask> operation, CancellationToken callerToken)
        => await WithinDeadlineAsync(deadline, async token =>
        {
            await operation(token).ConfigureAwait(false);
            return true;
        }, callerToken).ConfigureAwait(false);

    /// <summary>Consumes a late dependency exception without resuming later lifecycle stages.</summary>
    static async Task ObserveAsync(Task task)
    {
        try { await task.ConfigureAwait(false); }
        catch { /* The request already returned failure or propagated caller cancellation. */ }
    }

    static IMarketConditionFunctionContext Typed(IFunctionActorContext<MarketConditionFunctionActor> context)
        => context as IMarketConditionFunctionContext
           ?? throw new ArgumentException($"Context must implement {nameof(IMarketConditionFunctionContext)}.", nameof(context));
}
