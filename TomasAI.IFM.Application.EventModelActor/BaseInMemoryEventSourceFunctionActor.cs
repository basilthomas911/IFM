using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.EventModelActor;

/// <summary>Reuses immutable committed Function checkpoints while retaining the existing durable lifecycle.</summary>
public abstract class BaseInMemoryEventSourceFunctionActor<TActor, TRequest, TFunctionEntityId, TResultEntityId,
    TState, TCompletedEvent, TFailedEvent>(IFunctionActorContext<TActor> context,
    IEventSourceFunctionStateRepository<TState, TRequest> repository, IFunctionProjector<TCompletedEvent>? projector,
    ILogger logger, ResidentActorPolicy policy)
    : BaseEventSourceFunctionActor<TActor, TRequest, TFunctionEntityId, TResultEntityId, TState, TCompletedEvent, TFailedEvent>(
        context, repository, projector, logger)
    where TActor : IActor
    where TRequest : class, ICommand<TFunctionEntityId>
    where TFunctionEntityId : IActorEntityId
    where TResultEntityId : IActorEntityId
    where TState : class, IEventSourceFunctionState<TState, TRequest, TCompletedEvent>
    where TCompletedEvent : class, ICompleteEvent<TResultEntityId>
    where TFailedEvent : class, IErrorEvent<TResultEntityId>
{
    readonly ResidentActorCache<ResidentFunctionCheckpoint<TRequest, TState>> cache = new(typeof(TActor).Name, policy.Validate());
    readonly IResidentFunctionStateRepository<TState, TRequest>? replayRepository = repository as IResidentFunctionStateRepository<TState, TRequest>;
    readonly AsyncLocal<RequestOwnership?> invocation = new();

    public ResidentActorCacheStatistics ResidentCacheStatistics => cache.Statistics;

    /// <summary>Captures only reconstructable committed business state; empty/failed state returns null.</summary>
    protected abstract ResidentFunctionCheckpoint<TRequest, TState>? CaptureCommittedState(TState state);

    /// <summary>Validates a completion replay that would otherwise skip optimistic write validation.</summary>
    protected virtual ValueTask<bool> IsResidentReplayCurrentAsync(TState state, TRequest request,
        CancellationToken token) => replayRepository is { } guarded
            ? guarded.IsCurrentAsync(state, request, token)
            : ValueTask.FromException<bool>(new InvalidOperationException("A resident completion requires a stream-head validation contract."));

    public override async ValueTask HandleMessageAsync(IActorMessage message, ActorThreadId threadId,
        CancellationToken cancellationToken)
    {
        var previous = invocation.Value;
        var ownership = new RequestOwnership();
        invocation.Value = ownership;
        try { await base.HandleMessageAsync(message, threadId, cancellationToken).ConfigureAwait(false); }
        finally
        {
            ownership.Dispose();
            invocation.Value = previous;
        }
    }

    protected override async ValueTask<TState> LoadFunctionStateAsync(TRequest request, CancellationToken cancellationToken)
    {
        if (policy.Enabled && ResolveExecutionPolicy(request, FunctionFailureStage.Persistence).CompletionMode
            == FunctionCompletionMode.AtomicBusinessAndEvent)
            throw new InvalidOperationException("Resident atomic Functions require a qualified direct-commit cache hook.");
        // Capture the invocation before any await. A timed-out load must never attach to a later request.
        var ownership = invocation.Value ?? throw new InvalidOperationException("Missing resident Function invocation.");
        var key = request.Subject.ThreadId;
        var owner = await cache.AcquireAsync(key, policy.Enabled, cancellationToken).ConfigureAwait(false);
        if (owner is null) return await base.LoadFunctionStateAsync(request, cancellationToken).ConfigureAwait(false);
        if (!ownership.Attach(owner))
            throw new OperationCanceledException("The resident Function invocation has already ended.", cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (owner.Value is { } checkpoint)
        {
            var restored = checkpoint.Restore(request);
            if (!restored.IsCompleted || await IsResidentReplayCurrentAsync(restored, request, cancellationToken).ConfigureAwait(false))
                return restored;
            owner.Value = null;
        }
        var loaded = await base.LoadFunctionStateAsync(request, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        owner.Value = CaptureCommittedState(loaded);
        return loaded;
    }

    protected override async ValueTask SaveFunctionStateAsync(IFunctionActorContext<TActor> context,
        ActorThreadId threadId, TState state, TRequest request, TCompletedEvent completedEvent,
        CancellationToken cancellationToken)
    {
        var owner = invocation.Value?.Owner;
        try
        {
            await base.SaveFunctionStateAsync(context, threadId, state, request, completedEvent, cancellationToken)
                .ConfigureAwait(false);
        }
        catch { owner?.Invalidate(); throw; }
        // Persistence is confirmed. A failed cache capture must not turn a committed completion into a failure.
        if (owner is not null)
        {
            try { owner.Value = CaptureCommittedState(state); }
            catch (Exception exception)
            {
                owner.Invalidate();
                Logger.LogWarning(exception, "Resident Function checkpoint capture failed for {Actor}.", Id.Name);
            }
        }
    }

    protected override void OnFunctionStageAbandoned(TRequest request, FunctionFailureStage stage, Task task)
    {
        invocation.Value?.Abandon(task);
    }

    protected override async ValueTask OnShutdownAsync(IFunctionActorContext<TActor> context, CancellationToken token)
    {
        await cache.DrainAsync(token).ConfigureAwait(false);
        await base.OnShutdownAsync(context, token).ConfigureAwait(false);
    }

    /// <summary>Fences an invocation even when an abandoned load has not acquired its cache lease yet.</summary>
    sealed class RequestOwnership : IDisposable
    {
        readonly object sync = new();
        ResidentActorCache<ResidentFunctionCheckpoint<TRequest, TState>>.Lease? owner;
        Task? abandoned;
        bool finished;

        public ResidentActorCache<ResidentFunctionCheckpoint<TRequest, TState>>.Lease? Owner
        { get { lock (sync) return owner; } }

        public bool Attach(ResidentActorCache<ResidentFunctionCheckpoint<TRequest, TState>>.Lease acquired)
        {
            lock (sync)
            {
                if (owner is not null) throw new InvalidOperationException("A Function invocation already owns a lease.");
                owner = acquired;
                if (abandoned is not null) { acquired.Invalidate(); acquired.HoldUntil(abandoned); }
                if (finished)
                {
                    acquired.Invalidate();
                    acquired.Dispose();
                    return false;
                }
                return abandoned is null;
            }
        }

        public void Abandon(Task task)
        {
            lock (sync)
            {
                abandoned = task;
                owner?.Invalidate();
                owner?.HoldUntil(task);
            }
        }

        public void Dispose()
        {
            lock (sync)
            {
                finished = true;
                owner?.Dispose();
            }
        }
    }

}
