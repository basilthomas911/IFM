using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Provides a completed-only event-sourced Function lifecycle for command-shaped Core NATS requests.
/// Calculation and projection failures are returned to the caller and never enter Function state.
/// </summary>
public abstract class BaseEventSourceFunctionActor<
    TActor,
    TRequest,
    TFunctionEntityId,
    TResultEntityId,
    TState,
    TCompletedEvent,
    TFailedEvent>(
        IFunctionActorContext<TActor> actorContext,
        IEventSourceFunctionStateRepository<TState, TRequest> stateRepository,
        IFunctionProjector<TCompletedEvent>? functionProjector,
        ILogger logger)
    : IFunctionActor<TActor>
    where TActor : IActor
    where TRequest : class, ICommand<TFunctionEntityId>
    where TFunctionEntityId : IActorEntityId
    where TResultEntityId : IActorEntityId
    where TState : class, IEventSourceFunctionState<TState, TRequest, TCompletedEvent>
    where TCompletedEvent : class, ICompleteEvent<TResultEntityId>
    where TFailedEvent : class, IErrorEvent<TResultEntityId>
{
    readonly IFunctionActorContext<TActor> _context = actorContext
        ?? throw new ArgumentNullException(nameof(actorContext));
    readonly IEventSourceFunctionStateRepository<TState, TRequest> _stateRepository = stateRepository
        ?? throw new ArgumentNullException(nameof(stateRepository));
    readonly IFunctionProjector<TCompletedEvent>? _functionProjector = functionProjector;
    readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    IActorSupervisor? _supervisor;
    int _lifecycle;

    public ActorMailboxId Id => _context.ActorId;
    protected IFunctionActorContext<TActor> Context => _context;
    protected ILogger Logger => _logger;
    public IActorMailbox Mailbox { get; private set; } = default!;
    public bool IsRunning => Volatile.Read(ref _lifecycle) == 2;

    public ValueTask StartAsync(IActorSupervisor supervisor)
        => StartAsync(supervisor, CancellationToken.None);

    public async ValueTask StartAsync(IActorSupervisor supervisor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(supervisor);
        cancellationToken.ThrowIfCancellationRequested();
        if (Interlocked.CompareExchange(ref _lifecycle, 1, 0) != 0)
            return;
        try
        {
            _supervisor = supervisor;
            Mailbox = supervisor.CreateMailbox(Id);
            var producer = supervisor.GetProducer(Id);
            await producer.StartAsync(Id, cancellationToken).ConfigureAwait(false);
            await OnStartupAsync(_context, cancellationToken).ConfigureAwait(false);
            Volatile.Write(ref _lifecycle, 2);
        }
        catch
        {
            Volatile.Write(ref _lifecycle, 0);
            throw;
        }
    }

    public ValueTask StopAsync() => StopAsync(CancellationToken.None);

    public async ValueTask StopAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Interlocked.CompareExchange(ref _lifecycle, 3, 2) != 2)
            return;
        try
        {
            if (_supervisor is not null)
                await _supervisor.GetProducer(Id).StopAsync(cancellationToken).ConfigureAwait(false);
            await OnShutdownAsync(_context, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Volatile.Write(ref _lifecycle, 0);
        }
    }

    public ValueTask HandleMessageAsync(IActorMessage message)
        => HandleMessageAsync(message, message.Subject.ThreadId, CancellationToken.None);

    public ValueTask HandleMessageAsync(IActorMessage message, ActorThreadId threadId)
        => HandleMessageAsync(message, threadId, CancellationToken.None);

    public virtual async ValueTask HandleMessageAsync(
        IActorMessage message,
        ActorThreadId threadId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        TRequest? request = null;
        var stage = FunctionFailureStage.Parsing;
        FunctionResult<TCompletedEvent, TFailedEvent> terminal;
        try
        {
            try
            {
                request = ParseMessage(_context, message);
            }
            finally
            {
                message.ReleasePayload();
            }

            _logger.LogInformation(
                "Executing Function request {CommandName} CommandId={CommandId} on {ActorId}",
                request.CommandName,
                request.CommandId,
                Id);

            stage = FunctionFailureStage.Validation;
            await ValidateAsync(_context, threadId, request, cancellationToken).ConfigureAwait(false);

            stage = FunctionFailureStage.Loading;
            var state = await RunFunctionStageAsync(request, stage, token => LoadFunctionStateAsync(request, token), cancellationToken).ConfigureAwait(false);
            state.Id = threadId;
            if (state.IsCompleted)
            {
                if (!state.Matches(request) || state.CompletedEvent is null)
                    terminal = HandleFunctionEvent(_context,
                        new(typeof(TFailedEvent), request, IsConflict: true));
                else
                {
                    terminal = FunctionResult<TCompletedEvent, TFailedEvent>.Complete(state.CompletedEvent);
                    ObserveCompletedFunction(request, state.CompletedEvent, FunctionEventPhase.Replayed);
                }
            }
            else
            {
                stage = FunctionFailureStage.Execution;
                terminal = await RunFunctionStageAsync(request, stage,
                    token => ExecuteFunctionAsync(_context, state, request, token), cancellationToken).ConfigureAwait(false);
                if (!terminal.IsTerminal)
                {
                    throw new InvalidOperationException(
                        "Function execution must return exactly one completed or failed terminal value.");
                }

                if (terminal.IsCompleted)
                {
                    var completed = terminal.Completed!;
                    try
                    {
                        var persistencePolicy = ResolveExecutionPolicy(request, FunctionFailureStage.Persistence);
                        if (persistencePolicy.CompletionMode == FunctionCompletionMode.AtomicBusinessAndEvent)
                        {
                            stage = FunctionFailureStage.Persistence;
                            if (_functionProjector is not null || _stateRepository is not
                                ITransactionalFunctionStateRepository<TState, TRequest, TCompletedEvent> transactional)
                                throw new InvalidOperationException("Atomic Function completion requires an enlisted repository and no independent projector.");
                            try
                            {
                                completed = await RunFunctionStageAsync(request, stage,
                                    token => transactional.CommitAsync(_context, request, completed, token),
                                    cancellationToken, preserveConfirmedCommit: true).ConfigureAwait(false);
                            }
                            catch (TimeoutException exception)
                            {
                                throw new FunctionCommitOutcomeUnknownException(
                                    "The commit stage timed out. Reconcile the original operation before assuming rollback.", exception);
                            }
                            terminal = FunctionResult<TCompletedEvent, TFailedEvent>.Complete(completed);
                            // The transaction has committed. Cache/observer failure cannot change financial truth.
                            try
                            {
                                if (!state.TryComplete(completed, request))
                                    throw new InvalidOperationException("Committed Function state rejected cache finalization.");
                            }
                            catch (Exception exception)
                            {
                                _logger.LogError(exception, "Committed Function {CommandId} requires state reload; durable completion is unchanged.", request.CommandId);
                            }
                        }
                        else
                        {
                            stage = FunctionFailureStage.Projection;
                            await RunFunctionStageAsync(request, stage, token => ProjectFunctionResultAsync(request, completed, token), cancellationToken)
                                .ConfigureAwait(false);
                            stage = FunctionFailureStage.Persistence;
                            await RunFunctionStageAsync(request, stage,
                                token => SaveFunctionStateAsync(_context, threadId, state, request, completed, token),
                                cancellationToken).ConfigureAwait(false);
                        }
                        ObserveCompletedFunction(request, completed, FunctionEventPhase.Committed);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        terminal = HandleFunctionEvent(_context,
                            new(typeof(TFailedEvent), request, Exception: exception, Stage: stage));
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception,
                "Function actor {ActorId} failed during {FailureStage}", Id, stage);
            terminal = HandleFunctionEvent(_context,
                new(typeof(TFailedEvent), request, Exception: exception, Stage: stage));
        }

        ServiceResult<FunctionResult<TCompletedEvent, TFailedEvent>> reply = terminal.IsCompleted
            ? new ServiceOk<FunctionResult<TCompletedEvent, TFailedEvent>>(terminal)
            : new ServiceFailed<FunctionResult<TCompletedEvent, TFailedEvent>>(
                terminal.Failed!.ErrorCode,
                terminal.Failed.ErrorMessage,
                terminal);
        await message.ReplyAsync(reply).ConfigureAwait(false);
    }

    /// <summary>Resolves a stage policy through the derived actor's exact-command policy map.</summary>
    protected abstract FunctionExecutionPolicy ResolveExecutionPolicy(TRequest request, FunctionFailureStage stage);

    /// <summary>Dispatches a typed policy extension and rejects unsupported stages, commands or missing policies.</summary>
    protected static FunctionExecutionPolicy DispatchMappedExecutionPolicy<TContext>(TRequest request,
        FunctionFailureStage stage, TContext context,
        IReadOnlyDictionary<Type, Func<TRequest, FunctionFailureStage, TContext, FunctionExecutionPolicy>> map)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(map);
        if (stage is not (FunctionFailureStage.Loading or FunctionFailureStage.Execution or FunctionFailureStage.Projection or FunctionFailureStage.Persistence))
            throw new ArgumentOutOfRangeException(nameof(stage), "Execution policy applies only to executable lifecycle stages.");
        if (!map.TryGetValue(request.GetType(), out var handler))
            throw new InvalidOperationException($"No Function execution policy is registered for exact command type {request.GetType()}.");
        return handler(request, stage, context) ?? throw new InvalidOperationException("The mapped Function execution policy is missing.");
    }

    /// <summary>Notifies the event map after a successful append or matching replay without changing the durable outcome.</summary>
    void ObserveCompletedFunction(TRequest request, TCompletedEvent completed, FunctionEventPhase phase)
    {
        try
        {
            var observation = HandleFunctionEvent(_context, new(typeof(TCompletedEvent), request, completed,
                Stage: phase == FunctionEventPhase.Committed ? FunctionFailureStage.Persistence : FunctionFailureStage.Loading,
                Phase: phase));
            if (!observation.IsCompleted || !ReferenceEquals(observation.Completed, completed))
                throw new InvalidOperationException("A completion observer must return the original completed event.");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Function {Phase} observation failed for {CommandId}; the completed outcome is unchanged.", phase, request.CommandId);
        }
    }

    /// <summary>Enforces stage cancellation and exact-boundary expiry without allowing late work to resume the lifecycle.</summary>
    async ValueTask<T> RunFunctionStageAsync<T>(TRequest request, FunctionFailureStage stage,
        Func<CancellationToken, ValueTask<T>> operation, CancellationToken callerToken,
        bool preserveConfirmedCommit = false)
    {
        callerToken.ThrowIfCancellationRequested();
        var policy = ResolveExecutionPolicy(request, stage)
            ?? throw new InvalidOperationException("Function execution policy is required.");
        var deadline = policy.DeadlineUtc;
        if (deadline is null) return await operation(callerToken).ConfigureAwait(false);
        var clock = policy.Clock;
        var remaining = deadline.Value - clock.GetUtcNow().UtcDateTime;
        if (remaining <= TimeSpan.Zero) throw new TimeoutException();
        using var workerCancellation = CancellationTokenSource.CreateLinkedTokenSource(callerToken);
        Task<T>? task = null;
        try
        {
            task = operation(workerCancellation.Token).AsTask();
            var result = await task.WaitAsync(remaining, clock, callerToken).ConfigureAwait(false);
            if (!preserveConfirmedCommit)
            {
                callerToken.ThrowIfCancellationRequested();
                if (clock.GetUtcNow().UtcDateTime >= deadline.Value) throw new TimeoutException();
            }
            return result;
        }
        catch
        {
            workerCancellation.Cancel();
            if (task is not null) _ = ObserveLateFunctionStageAsync(task);
            throw;
        }
    }

    /// <summary>Applies lifecycle timing to projection and persistence operations.</summary>
    async ValueTask RunFunctionStageAsync(TRequest request, FunctionFailureStage stage,
        Func<CancellationToken, ValueTask> operation, CancellationToken callerToken)
        => await RunFunctionStageAsync(request, stage, async token =>
        {
            await operation(token).ConfigureAwait(false);
            return true;
        }, callerToken).ConfigureAwait(false);

    /// <summary>Observes exceptions from a dependency that ignores cancellation after the request has ended.</summary>
    static async Task ObserveLateFunctionStageAsync(Task task)
    {
        try { await task.ConfigureAwait(false); }
        catch { /* The request has already failed or propagated caller cancellation. */ }
    }

    /// <summary>Persists the one completed Function event without invoking a denormalizer.</summary>
    protected virtual async ValueTask SaveFunctionStateAsync(
        IFunctionActorContext<TActor> context,
        ActorThreadId threadId,
        TState state,
        TRequest request,
        TCompletedEvent completedEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(completedEvent);
        if (!state.TryComplete(completedEvent, request))
            throw new InvalidOperationException("Function state rejected its completed transition.");
        await _stateRepository.SaveCompletedStateAsync(
            context,
            state,
            request,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Loads completed-only state within the loading policy enforced by the base lifecycle.</summary>
    protected virtual ValueTask<TState> LoadFunctionStateAsync(TRequest request, CancellationToken cancellationToken)
        => _stateRepository.LoadStateAsync(request, cancellationToken);

    /// <summary>Projects a candidate completion before persistence within the base-enforced stage policy.</summary>
    protected virtual ValueTask ProjectFunctionResultAsync(
        TRequest request, TCompletedEvent completed, CancellationToken cancellationToken)
        => _functionProjector?.ProjectAsync(completed, cancellationToken) ?? ValueTask.CompletedTask;

    protected TRequest ParseMappedFunction(
        IFunctionActorContext<TActor> context,
        IActorMessage message,
        IReadOnlyDictionary<string, Func<IActorMessage, TRequest>> parseMap)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(parseMap);
        var subject = message.Subject;
        if (subject.ActorType != ActorType.Function ||
            !string.Equals(subject.Name, Id.Name, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(subject.EntityId) ||
            !parseMap.TryGetValue(subject.Verb, out var parser))
            throw new InvalidOperationException($"Unable to resolve {Id.Name} function from message: {subject}");
        var request = parser(message)
            ?? throw new InvalidOperationException($"Parser for {Id.Name}.{subject.Verb} returned no request.");
        if (request.Subject != subject ||
            !string.Equals(subject.EntityId, request.EntityId.Format(), StringComparison.Ordinal))
            throw new InvalidOperationException($"Function request routing does not match message: {subject}");
        return request;
    }

    /// <summary>Dispatches exact-type command validation and throws one aggregate validation error.</summary>
    protected void ValidateMappedCommand(
        ICommand command,
        IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> validationMap)
        => MappedCommandValidation.Validate(Id.Name, command, validationMap);

    protected static THandler ResolveMappedFunctionHandler<THandler>(
        TRequest request,
        IReadOnlyDictionary<Type, THandler> receiveMap)
        where THandler : Delegate
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(receiveMap);
        if (!receiveMap.TryGetValue(request.GetType(), out var handler))
            throw new InvalidOperationException(
                $"No Function handler is registered for exact request type {request.GetType().FullName}.");
        return handler;
    }

    /// <summary>Resolves exactly one terminal-event handler and rejects missing or incompatible results.</summary>
    /// <typeparam name="TEventContext">Additional context needed by the domain event factories.</typeparam>
    /// <param name="input">The target event type and the data from which to construct it.</param>
    /// <param name="context">The context passed to the mapped handler.</param>
    /// <param name="eventMap">Exact CLR event types mapped to terminal-event factories.</param>
    /// <returns>The single terminal value produced by the matching handler.</returns>
    protected static FunctionResult<TCompletedEvent, TFailedEvent> DispatchMappedFunctionEvent<TEventContext>(
        FunctionEventContext<TRequest> input,
        TEventContext context,
        IReadOnlyDictionary<Type, Func<FunctionEventContext<TRequest>, TEventContext,
            FunctionResult<TCompletedEvent, TFailedEvent>>> eventMap)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(eventMap);
        if (input.EventType is null || !eventMap.TryGetValue(input.EventType, out var handler))
            throw new InvalidOperationException($"No Function event handler is registered for exact event type {input.EventType}.");
        var result = handler(input, context);
        if (result is null || !result.IsTerminal ||
            (result.IsCompleted ? result.Completed!.GetType() : result.Failed!.GetType()) != input.EventType)
            throw new InvalidOperationException($"Function event handler for {input.EventType.Name} returned an incompatible terminal result.");
        return result;
    }

    /// <summary>Dispatches lifecycle failures; mapped actors override this hook to resolve their event map.</summary>
    /// <param name="context">The current Function actor context.</param>
    /// <param name="input">The target event type and failure information, including parsing failures without a request.</param>
    /// <returns>The domain-specific terminal response.</returns>
    /// <remarks>The default adapter preserves existing Function actors during migration to mapped event factories.</remarks>
    protected virtual FunctionResult<TCompletedEvent, TFailedEvent> HandleFunctionEvent(
        IFunctionActorContext<TActor> context, FunctionEventContext<TRequest> input)
    {
        if (input.Phase is FunctionEventPhase.Committed or FunctionEventPhase.Replayed && input.Outcome is TCompletedEvent completed)
            return FunctionResult<TCompletedEvent, TFailedEvent>.Complete(completed);
        if (input.EventType != typeof(TFailedEvent))
            throw new InvalidOperationException($"No Function event handler is registered for {input.EventType}.");
        return FunctionResult<TCompletedEvent, TFailedEvent>.Fail(input.IsConflict
            ? CreateConflictFailedEvent(input.Request!)
            : CreateFailedEvent(input.Request, input.Exception!, input.Stage));
    }

    protected abstract TRequest ParseMessage(
        IFunctionActorContext<TActor> context,
        IActorMessage message);

    protected virtual ValueTask ValidateAsync(
        IFunctionActorContext<TActor> context,
        ActorThreadId threadId,
        TRequest request,
        CancellationToken cancellationToken)
        => ValueTask.CompletedTask;

    protected abstract ValueTask<FunctionResult<TCompletedEvent, TFailedEvent>> ExecuteFunctionAsync(
        IFunctionActorContext<TActor> context,
        TState state,
        TRequest request,
        CancellationToken cancellationToken);

    /// <summary>Compatibility factory for Function actors not yet using an event map.</summary>
    protected virtual TFailedEvent CreateConflictFailedEvent(TRequest request)
        => throw new InvalidOperationException("A mapped Function failure handler is required.");

    /// <summary>Compatibility factory for Function actors not yet using an event map.</summary>
    protected virtual TFailedEvent CreateFailedEvent(
        TRequest? request,
        Exception exception,
        FunctionFailureStage stage)
        => throw new InvalidOperationException("A mapped Function failure handler is required.");

    protected virtual ValueTask OnStartupAsync(
        IFunctionActorContext<TActor> context,
        CancellationToken cancellationToken) => ValueTask.CompletedTask;

    protected virtual ValueTask OnShutdownAsync(
        IFunctionActorContext<TActor> context,
        CancellationToken cancellationToken) => ValueTask.CompletedTask;
}
