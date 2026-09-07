using System.Collections.Frozen;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;

using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Function.Extensions;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Function.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Function.Actor;

/// <summary>Mapped, completed-only Trade Selection Function with deadline-bounded lifecycle stages.</summary>
public sealed class TradeSelectionFunctionActor(IFunctionActorContext<TradeSelectionFunctionActor> actorContext)
    : BaseEventSourceFunctionActor<TradeSelectionFunctionActor, ExecuteTradeSelectionPipelineCommand,
        TradeSelectionExecutionId, IntrinsicTimeStrategyWorkflowEntityId, TradeSelectionFunctionState,
        TradeSelectionFunctionCompletedEvent, TradeSelectionFunctionFailedEvent>(
            actorContext, Typed(actorContext).StateRepository, Typed(actorContext).FunctionProjector, Typed(actorContext).Logger)
{
    public const string ActorName = ExecuteTradeSelectionPipelineCommand.Actor;

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ExecuteTradeSelectionPipelineCommand>> _parseMap =
        new Dictionary<string, Func<IActorMessage, ExecuteTradeSelectionPipelineCommand>>(StringComparer.Ordinal)
        {
            [ExecuteTradeSelectionPipelineCommand.Verb] = static message =>
            {
                var request = message.AsCommand<ExecuteTradeSelectionPipelineCommand>()!;
                if (request is not null && message.Subject.EntityId != request.EntityId.Format())
                    throw new ArgumentException("Trade Selection transport subject mismatch.");
                return request!;
            }
        }.ToFrozenDictionary(StringComparer.Ordinal);

    static readonly IReadOnlyDictionary<Type, Func<ExecuteTradeSelectionPipelineCommand, List<ValidationError>>> _validationMap =
        new Dictionary<Type, Func<ExecuteTradeSelectionPipelineCommand, List<ValidationError>>>
        {
            [typeof(ExecuteTradeSelectionPipelineCommand)] = static request => new List<ValidationError>()
                .ValidateCommandId(request.CommandId, request.CommandName)
                .ValidateEntityId(request.EntityId, request.CommandName)
                .CaptureCommandValidation(() => TradeSelectionContracts.ValidateRequest(request))
        }.ToFrozenDictionary();

    static readonly IReadOnlyDictionary<Type, Func<ExecuteTradeSelectionPipelineCommand,
        ITradeSelectionFunctionContext, CancellationToken,
        ValueTask<FunctionResult<TradeSelectionFunctionCompletedEvent, TradeSelectionFunctionFailedEvent>>>> _receiveMap =
        new Dictionary<Type, Func<ExecuteTradeSelectionPipelineCommand, ITradeSelectionFunctionContext, CancellationToken,
            ValueTask<FunctionResult<TradeSelectionFunctionCompletedEvent, TradeSelectionFunctionFailedEvent>>>>
        {
            [typeof(ExecuteTradeSelectionPipelineCommand)] = static (request, context, token) => request.ExecuteAsync(context, token)
        }.ToFrozenDictionary();

    ITradeSelectionFunctionContext ActorContext => Typed(Context);

    protected override ExecuteTradeSelectionPipelineCommand ParseMessage(
        IFunctionActorContext<TradeSelectionFunctionActor> context, IActorMessage message)
        => ParseMappedFunction(context, message, _parseMap);

    protected override ValueTask ValidateAsync(IFunctionActorContext<TradeSelectionFunctionActor> context,
        ActorThreadId threadId, ExecuteTradeSelectionPipelineCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_validationMap.TryGetValue(request.GetType(), out var validate))
            throw new InvalidOperationException($"No validation is registered for {request.GetType().Name}.");
        var errors = validate(request);
        if (errors.Count != 0)
            throw new CommandValidationException(request.ErrorCode, string.Join(Environment.NewLine, errors.Select(error => error.ErrorMessage)));
        var graph=request.SelectionBinding.CatalogDefinitions.ToDictionary(x=>x.Key,SelectionCatalogTransport.ToSource);
        try
        {
            foreach(var node in graph.Values)
            {
                foreach(var capability in node.Definition.Capabilities)ActorContext.Capabilities.Validate(capability,node.Definition,graph);
                if(node.Definition.Key.Kind==TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog.StrategyCatalogKind.ParameterSet)
                    foreach(var validator in graph[node.Definition.Parent!].Definition.Capabilities.Where(x=>x.Role=="validator"))ActorContext.Capabilities.Validate(validator,node.Definition,graph);
            }
        }
        catch(TradeSelectionValidationException){throw;}
        catch(Exception ex) when(ex is ArgumentException or InvalidOperationException)
        { throw new TradeSelectionValidationException("TS.CONFIG.CAPABILITY_UNSUPPORTED",ex.Message); }
        return ValueTask.CompletedTask;
    }

    protected override async ValueTask<TradeSelectionFunctionState> LoadFunctionStateAsync(
        ExecuteTradeSelectionPipelineCommand request, CancellationToken cancellationToken)
    {
        // Completed replies can replay after expiry; the read still has a bounded execution budget.
        var deadline = ActorContext.TimeProvider.GetUtcNow().UtcDateTime.AddMilliseconds(
            TradeSelectionContracts.CommonPolicy(request.SelectionBinding).MaximumExecutionMilliseconds);
        var state = await WithinDeadlineAsync(deadline,
            token => base.LoadFunctionStateAsync(request, token), cancellationToken).ConfigureAwait(false);
        if (state.Matches(request)) TradeSelectionTelemetry.Replay();
        return state;
    }

    protected override ValueTask<FunctionResult<TradeSelectionFunctionCompletedEvent, TradeSelectionFunctionFailedEvent>> ExecuteFunctionAsync(
        IFunctionActorContext<TradeSelectionFunctionActor> context, TradeSelectionFunctionState state,
        ExecuteTradeSelectionPipelineCommand request, CancellationToken cancellationToken)
    {
        var receive = ResolveMappedFunctionHandler(request, _receiveMap);
        return WithinDeadlineAsync(request.ExpiresAtUtc, token => receive(request, ActorContext, token), cancellationToken);
    }

    protected override ValueTask ProjectFunctionResultAsync(ExecuteTradeSelectionPipelineCommand request,
        TradeSelectionFunctionCompletedEvent completed, CancellationToken cancellationToken)
        => WithinDeadlineAsync(request.ExpiresAtUtc, token => base.ProjectFunctionResultAsync(request, completed, token), cancellationToken);

    protected override async ValueTask SaveFunctionStateAsync(IFunctionActorContext<TradeSelectionFunctionActor> context,
        ActorThreadId threadId, TradeSelectionFunctionState state, ExecuteTradeSelectionPipelineCommand request,
        TradeSelectionFunctionCompletedEvent completedEvent, CancellationToken cancellationToken)
    {
        await WithinDeadlineAsync(request.ExpiresAtUtc,
            token => base.SaveFunctionStateAsync(context, threadId, state, request, completedEvent, token), cancellationToken).ConfigureAwait(false);
        TradeSelectionTelemetry.Record(TradeSelectionContracts.ReadResult(completedEvent.Result),
            Math.Max(0, (ActorContext.TimeProvider.GetUtcNow().UtcDateTime - request.RequestedAtUtc).TotalMilliseconds));
    }

    protected override TradeSelectionFunctionFailedEvent CreateConflictFailedEvent(ExecuteTradeSelectionPipelineCommand request)
    {
        TradeSelectionTelemetry.Failure("TS.CONTRACT.CONFLICTING_DUPLICATE");
        return ExecuteTradeSelectionPipeline.CreateFailedEvent(request,"TS.CONTRACT.CONFLICTING_DUPLICATE",ActorContext.TimeProvider);
    }

    protected override TradeSelectionFunctionFailedEvent CreateFailedEvent(ExecuteTradeSelectionPipelineCommand? request,
        Exception exception, FunctionFailureStage stage)
    {
        var reason = exception is TradeSelectionValidationException validation ? validation.ReasonCode
            : exception is TimeoutException ? "TS.TIME.EXPIRED" : stage switch
            {
                FunctionFailureStage.Loading or FunctionFailureStage.Persistence => "TS.PERSISTENCE.FAILED",
                FunctionFailureStage.Projection => "TS.PROJECTION.FAILED",
                FunctionFailureStage.Execution => "TS.CALCULATION.FAILED",
                _ => "TS.CONTRACT.INVALID"
            };
        TradeSelectionTelemetry.Failure(reason);
        return ExecuteTradeSelectionPipeline.CreateFailedEvent(request, reason, ActorContext.TimeProvider);
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

    static ITradeSelectionFunctionContext Typed(IFunctionActorContext<TradeSelectionFunctionActor> context)
        => context as ITradeSelectionFunctionContext
           ?? throw new ArgumentException($"Context must implement {nameof(ITradeSelectionFunctionContext)}.", nameof(context));
}
