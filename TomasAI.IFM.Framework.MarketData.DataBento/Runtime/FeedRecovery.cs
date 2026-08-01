namespace TomasAI.IFM.Framework.MarketData.DataBento;

public enum FeedReadinessState : byte
{
    Closed = 0,
    Ready = 1,
    Suspect = 2,
    Recovering = 3,
    Faulted = 4
}

public enum FeedRecoveryFaultKind : byte
{
    ConnectionHung = 1,
    Disconnected = 2,
    RingOverrun = 3,
    SlowReader = 4,
    SkippedRecords = 5,
    SequenceGap = 6,
    SequenceReversal = 7,
    Authentication = 8,
    InvalidRequest = 9,
    SymbolResolution = 10,
    ProviderError = 11
}

public enum FeedRecoverySchema : byte
{
    Trades = 1,
    Mbp1 = 2,
    Mbo = 3,
    Definitions = 4
}

public sealed record FeedRecoveryAttempt(
    int AttemptNumber,
    TimeSpan Timeout,
    FeedRecoveryFaultKind Fault,
    FeedRecoverySchema Schema);

public sealed record FeedRecoveryResult
{
    public bool ConnectionAuthenticated { get; init; }
    public bool SubscriptionsAcknowledged { get; init; }
    public bool ReplayComplete { get; init; }
    public bool ContinuityVerified { get; init; }
    public bool RequiredBaselinesReady { get; init; }
    public bool DefinitionsComplete { get; init; } = true;
    public string? Failure { get; init; }

    public bool IsReady(FeedRecoverySchema schema) =>
        ConnectionAuthenticated
        && SubscriptionsAcknowledged
        && ReplayComplete
        && ContinuityVerified
        && RequiredBaselinesReady
        && (schema != FeedRecoverySchema.Definitions || DefinitionsComplete);
}

public interface IDatabentoRecoveryAttemptExecutor
{
    FeedRecoveryResult StopDisposeRecreateAndStart(FeedRecoveryAttempt attempt);
}

public interface IDatabentoRecoveryDelay
{
    void Delay(TimeSpan duration);
}

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

    public DatabentoRecoveryOrchestrator(
        IDatabentoRecoveryAttemptExecutor executor,
        IDatabentoRecoveryDelay delay)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _delay = delay ?? throw new ArgumentNullException(nameof(delay));
    }

    public FeedReadinessState State => _state;

    public bool EntryGateOpen => _state == FeedReadinessState.Ready;

    public event Action<FeedReadinessState>? StateChanged;

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

public sealed class DatabentoRecoveryException : Exception
{
    public DatabentoRecoveryException(
        FeedRecoveryFaultKind fault,
        int attempts,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Fault = fault;
        Attempts = attempts;
    }

    public FeedRecoveryFaultKind Fault { get; }
    public int Attempts { get; }
}

public sealed class TimestampReplayCursor
{
    private readonly ulong _savedTimestamp;
    private readonly uint _savedCount;
    private uint _seenAtSavedTimestamp;

    public TimestampReplayCursor(ulong savedTimestamp, uint savedCount)
    {
        _savedTimestamp = savedTimestamp;
        _savedCount = savedCount;
    }

    public bool ShouldAccept(ulong eventTimestamp)
    {
        if (eventTimestamp < _savedTimestamp)
        {
            return false;
        }
        if (eventTimestamp > _savedTimestamp)
        {
            return true;
        }
        _seenAtSavedTimestamp++;
        return _seenAtSavedTimestamp > _savedCount;
    }
}

public sealed class MboRecoveryBaseline
{
    private ulong _lastSequence;

    public bool SnapshotStarted { get; private set; }
    public bool SnapshotComplete { get; private set; }
    public bool LiveBoundaryReached { get; private set; }
    public bool IsReady => SnapshotComplete && LiveBoundaryReached;

    public void Reset()
    {
        _lastSequence = 0;
        SnapshotStarted = false;
        SnapshotComplete = false;
        LiveBoundaryReached = false;
    }

    public void BeginSnapshot()
    {
        Reset();
        SnapshotStarted = true;
    }

    public void ApplySnapshotRecord(ulong sequence)
    {
        if (!SnapshotStarted || SnapshotComplete)
        {
            throw new InvalidOperationException("MBO snapshot records require an active snapshot.");
        }
        RequireNext(sequence);
    }

    public void CompleteSnapshot()
    {
        if (!SnapshotStarted)
        {
            throw new InvalidOperationException("An MBO snapshot was not started.");
        }
        SnapshotComplete = true;
    }

    public void ApplyLiveRecord(ulong sequence)
    {
        if (!SnapshotComplete)
        {
            throw new InvalidOperationException("MBO live records require a complete snapshot.");
        }
        RequireNext(sequence);
        LiveBoundaryReached = true;
    }

    private void RequireNext(ulong sequence)
    {
        if (_lastSequence != 0 && sequence != _lastSequence + 1)
        {
            throw new InvalidDataException(
                $"MBO sequence continuity failed: expected {_lastSequence + 1}, received {sequence}.");
        }
        _lastSequence = sequence;
    }
}
