namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>Owns cancellation and synchronous mutation fencing for one disposable realtime actor lifetime.</summary>
public sealed class RealtimeActorGeneration
{
    static readonly AsyncLocal<RealtimeActorGeneration?> active = new();
    readonly CancellationTokenSource cancellation = new();
    readonly object gate = new();
    bool retired;
    /// <summary>Identifies this actor lifetime independently of provider generations.</summary>
    public Guid Id { get; } = Guid.NewGuid();
    /// <summary>Signals immediate abandonment of this generation.</summary>
    public CancellationToken Token => cancellation.Token;
    /// <summary>Gets the generation inherited by actor-owned asynchronous work.</summary>
    public static RealtimeActorGeneration? Current => active.Value;
    /// <summary>Checks publication or dispatch authority immediately before starting an operation.</summary>
    public static void ThrowIfRetired() => active.Value?.Token.ThrowIfCancellationRequested();
    /// <summary>Serializes a short synchronous state mutation with generation retirement.</summary>
    public static IDisposable EnterMutation()
    {
        var generation = active.Value;
        if (generation is null) return EmptyScope.Instance;
        Monitor.Enter(generation.gate);
        if (generation.retired)
        {
            Monitor.Exit(generation.gate);
            throw new OperationCanceledException(generation.Token);
        }
        return new MutationScope(generation.gate);
    }
    /// <summary>Binds this generation to the current asynchronous execution flow.</summary>
    public IDisposable Enter()
    {
        Token.ThrowIfCancellationRequested();
        var previous = active.Value;
        active.Value = this;
        return new ExecutionScope(previous);
    }
    /// <summary>Detaches independent persistence services from a disposable actor lifetime.</summary>
    public static IDisposable EnterIndependentPersistence()
    {
        var previous = active.Value;
        active.Value = null;
        return new ExecutionScope(previous);
    }
    /// <summary>Invalidates future mutations before requesting cancellation; never waits for a handler to drain.</summary>
    public void Retire()
    {
        lock (gate) retired = true;
        // Callback code must not hold up recovery. Cancellation state is signalled synchronously.
        _ = cancellation.CancelAsync().ContinueWith(static task => { _ = task.Exception; },
            CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }
    sealed class MutationScope(object gate) : IDisposable
    {
        public void Dispose() => Monitor.Exit(gate);
    }
    sealed class ExecutionScope(RealtimeActorGeneration? previous) : IDisposable
    {
        public void Dispose() => active.Value = previous;
    }
    sealed class EmptyScope : IDisposable
    {
        internal static readonly EmptyScope Instance = new();
        public void Dispose() { }
    }
}

/// <summary>Allows the supervisor to retire a disposable actor without invoking draining shutdown hooks.</summary>
public interface IReplaceableRealtimeActor
{
    /// <summary>Gets the cancellation and fencing authority for this instance.</summary>
    RealtimeActorGeneration RealtimeGeneration { get; }
    /// <summary>Retires this instance; its context is never reused by its replacement.</summary>
    void RetireRealtimeGeneration();
}

/// <summary>Restricts non-draining replacement to market-data setup actors; financial actors retain durable recovery.</summary>
public static class RealtimeActorResetPolicy
{
    /// <summary>Checks the explicit domain boundary for disposable actor replacement.</summary>
    public static bool CanReplace(Type type, ActorType actorType)
        => actorType == ActorType.Realtime
            && (type.Namespace?.StartsWith("TomasAI.IFM.Domain.MarketData.Feed.", StringComparison.Ordinal) == true
                || type.Namespace?.StartsWith("TomasAI.IFM.Domain.MarketData.Analytics.", StringComparison.Ordinal) == true);
}
