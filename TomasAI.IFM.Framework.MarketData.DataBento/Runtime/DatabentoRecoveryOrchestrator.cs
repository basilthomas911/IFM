namespace TomasAI.IFM.Framework.MarketData.DataBento;

public sealed class DatabentoRecoveryOrchestrator
{
    private static readonly TimeSpan[] Backoff =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30)
    ];

    private readonly IDatabentoRecoveryAttemptExecutor _executor;
    private readonly IDatabentoRecoveryDelay _delay;
    private FeedReadinessState _state = FeedReadinessState.Closed;

    /// <summary>Initializes a new DatabentoRecoveryOrchestrator instance.</summary>
    /// <param name="executor">The component that recreates and restarts the feed for each recovery attempt.</param>
    /// <param name="delay">The service applying recovery backoff delays.</param>
    public DatabentoRecoveryOrchestrator(
        IDatabentoRecoveryAttemptExecutor executor,
        IDatabentoRecoveryDelay delay)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _delay = delay ?? throw new ArgumentNullException(nameof(delay));
    }

    public FeedReadinessState State => _state;

    public bool EntryGateOpen => _state == FeedReadinessState.Ready;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1003", Justification = "Preserve the published recovery state Action subscriber contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0046", Justification = "Preserve the published recovery state Action subscriber contract.")]
    public event Action<FeedReadinessState>? StateChanged;

    /// <summary>Attempts bounded feed recovery, opening admission only after readiness is verified.</summary>
    /// <param name="fault">The feed fault that triggered recovery.</param>
    /// <param name="schema">The provider schema or recovery baseline required for the operation.</param>
    /// <returns>The recover result.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Capture each failed recovery attempt and preserve the final cause when retries are exhausted.")]
    public FeedRecoveryResult Recover(
        FeedRecoveryFaultKind fault,
        FeedRecoverySchema schema)
    {
        if (!Enum.IsDefined(fault) || !Enum.IsDefined(schema))
        {
            throw new ArgumentOutOfRangeException(nameof(fault));
        }

        Transition(FeedReadinessState.Suspect);
        if (IsPermanent(fault))
        {
            Transition(FeedReadinessState.Faulted);
            throw new DatabentoRecoveryException(
                fault,
                0,
                $"Databento recovery is not retryable for {fault}; the entry gate remains closed.");
        }

        Transition(FeedReadinessState.Recovering);
        FeedRecoveryResult? last = null;
        Exception? lastException = null;
        for (var index = 0; index < Backoff.Length; index++)
        {
            _delay.Delay(Backoff[index]);
            try
            {
                last = _executor.StopDisposeRecreateAndStart(
                    new FeedRecoveryAttempt(
                        index + 1,
                        TimeSpan.FromSeconds(30),
                        fault,
                        schema));
                if (last.IsReady(schema))
                {
                    Transition(FeedReadinessState.Ready);
                    return last;
                }
            }
            catch (Exception exception)
            {
                lastException = exception;
            }
        }

        Transition(FeedReadinessState.Faulted);
        var detail = last?.Failure ?? lastException?.Message ?? "readiness verification failed";
        throw new DatabentoRecoveryException(
            fault,
            Backoff.Length,
            $"Databento recovery exhausted {Backoff.Length} attempts: {detail}",
            lastException);
    }

    /// <summary>Verifies initial feed readiness before opening the entry gate.</summary>
    /// <param name="readiness">The observed feed readiness requirements to verify.</param>
    /// <param name="schema">The provider schema or recovery baseline required for the operation.</param>
    public void EstablishInitialReadiness(
        FeedRecoveryResult readiness,
        FeedRecoverySchema schema)
    {
        ArgumentNullException.ThrowIfNull(readiness);
        if (!Enum.IsDefined(schema))
        {
            throw new ArgumentOutOfRangeException(nameof(schema));
        }
        if (!readiness.IsReady(schema))
        {
            Transition(FeedReadinessState.Closed);
            throw new InvalidOperationException(
                "The Databento entry gate cannot open until connection, subscriptions, replay, continuity, baselines, and definitions are ready.");
        }
        Transition(FeedReadinessState.Ready);
    }

    private static bool IsPermanent(FeedRecoveryFaultKind fault) =>
        fault is FeedRecoveryFaultKind.Authentication
            or FeedRecoveryFaultKind.InvalidRequest
            or FeedRecoveryFaultKind.SymbolResolution
            or FeedRecoveryFaultKind.ProviderError;

    private void Transition(FeedReadinessState state)
    {
        _state = state;
        StateChanged?.Invoke(state);
    }
}
