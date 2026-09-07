using Microsoft.Extensions.Logging;
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
            var state = await _stateRepository.LoadStateAsync(request, cancellationToken).ConfigureAwait(false);
            state.Id = threadId;
            if (state.IsCompleted)
            {
                if (!state.Matches(request) || state.CompletedEvent is null)
                    terminal = FunctionResult<TCompletedEvent, TFailedEvent>.Fail(
                        CreateConflictFailedEvent(request));
                else
                    terminal = FunctionResult<TCompletedEvent, TFailedEvent>.Complete(state.CompletedEvent);
            }
            else
            {
                stage = FunctionFailureStage.Execution;
                terminal = await ExecuteFunctionAsync(
                    _context,
                    state,
                    request,
                    cancellationToken).ConfigureAwait(false);
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
                        stage = FunctionFailureStage.Projection;
                        if (_functionProjector is not null)
                        {
                            await _functionProjector.ProjectAsync(completed, cancellationToken)
                                .ConfigureAwait(false);
                        }

                        stage = FunctionFailureStage.Persistence;
                        await SaveFunctionStateAsync(
                            _context,
                            threadId,
                            state,
                            request,
                            completed,
                            cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        terminal = FunctionResult<TCompletedEvent, TFailedEvent>.Fail(
                            CreateFailedEvent(request, exception, stage));
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
            terminal = FunctionResult<TCompletedEvent, TFailedEvent>.Fail(
                CreateFailedEvent(request, exception, stage));
        }

        ServiceResult<FunctionResult<TCompletedEvent, TFailedEvent>> reply = terminal.IsCompleted
            ? new ServiceOk<FunctionResult<TCompletedEvent, TFailedEvent>>(terminal)
            : new ServiceFailed<FunctionResult<TCompletedEvent, TFailedEvent>>(
                terminal.Failed!.ErrorCode,
                terminal.Failed.ErrorMessage,
                terminal);
        await message.ReplyAsync(reply).ConfigureAwait(false);
    }

    /// <summary>Persists the one completed Function event without invoking a denormalizer.</summary>
    protected async ValueTask SaveFunctionStateAsync(
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
            !parseMap.TryGetValue(subject.Verb, out var parser))
            throw new InvalidOperationException($"Unable to resolve {Id.Name} function from message: {subject}");
        return parser(message)
            ?? throw new InvalidOperationException($"Parser for {Id.Name}.{subject.Verb} returned no request.");
    }

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

    protected abstract TFailedEvent CreateConflictFailedEvent(TRequest request);

    protected abstract TFailedEvent CreateFailedEvent(
        TRequest? request,
        Exception exception,
        FunctionFailureStage stage);

    protected virtual ValueTask OnStartupAsync(
        IFunctionActorContext<TActor> context,
        CancellationToken cancellationToken) => ValueTask.CompletedTask;

    protected virtual ValueTask OnShutdownAsync(
        IFunctionActorContext<TActor> context,
        CancellationToken cancellationToken) => ValueTask.CompletedTask;
}
