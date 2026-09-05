using TomasAI.IFM.Domain.MarketData.Shared;

namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

/// <summary>
/// Supervisor-owned incident state. Generation replacement never resets its elapsed time or attempt
/// count; only the session policy's healthy qualification closes it.
/// </summary>
public sealed class DatasetIncidentStateMachine(
    string dataset,
    DateOnly valueDate,
    DatabentoStage3Options options,
    TimeProvider timeProvider)
{
    readonly object gate = new();
    readonly DatabentoStage3Options policy = options.Validate();
    long? openedTimestamp;
    long? healthyTimestamp;
    long? policyTimestamp;
    FuturesMarketState? policySession;
    long? replacementAllowedTimestamp;
    readonly Queue<long> replacementFailureTimes = new();
    Guid incidentId;
    Guid generationId;
    int attempts;
    int replacements;
    int replacementFailures;
    int replacementLatched;
    DatabentoDatasetFailureReason reason;
    DatasetRecoveryAction lastAction;

    public DatasetIncidentDecision ObserveScheduled(
        FuturesMarketState marketState,
        bool healthy,
        DatabentoDatasetFailureReason failureReason,
        Guid currentGeneration)
    {
        lock (gate)
        {
            generationId = currentGeneration;
            if (marketState == FuturesMarketState.Closed)
            {
                Close();
                lastAction = DatasetRecoveryAction.StopForClosure;
                return Decision(lastAction);
            }

            if (policySession is { } prior && prior != marketState)
            {
                policyTimestamp = timeProvider.GetTimestamp();
                healthyTimestamp = null;
                attempts = 0;
            }
            policySession = marketState;

            if (healthy)
                return ObserveHealthy(marketState);

            Open(failureReason);
            healthyTimestamp = null;
            if (Volatile.Read(ref replacementLatched) != 0)
                return Decision(DatasetRecoveryAction.None);
            if (ReplacementBackoffRemaining() > TimeSpan.Zero)
                return Decision(DatasetRecoveryAction.None);

            var unhealthy = Elapsed(policyTimestamp);
            if (marketState == FuturesMarketState.LiveTrading)
            {
                if (unhealthy >= policy.LiveTradingEscalationWindow
                    || attempts >= policy.LiveTradingMaximumCooperativeAttempts)
                {
                    lastAction = DatasetRecoveryAction.ReplaceProcess;
                    return Decision(lastAction);
                }

                attempts = checked(attempts + 1);
                lastAction = DatasetRecoveryAction.CooperativeReset;
                return Decision(lastAction);
            }

            if (unhealthy < policy.OffTradingStallTimeout)
                return Decision(DatasetRecoveryAction.None);

            if (attempts == 0)
            {
                attempts = 1;
                lastAction = DatasetRecoveryAction.CooperativeReset;
                return Decision(lastAction);
            }

            lastAction = DatasetRecoveryAction.ReplaceProcess;
            return Decision(lastAction);
        }
    }

    public DatasetIncidentDecision ObserveTerminal(
        bool processExited,
        DatabentoDatasetFailureReason failureReason,
        Guid currentGeneration)
    {
        lock (gate)
        {
            generationId = currentGeneration;
            Open(failureReason);
            healthyTimestamp = null;
            if (Volatile.Read(ref replacementLatched) != 0)
                return Decision(DatasetRecoveryAction.None);
            if (ReplacementBackoffRemaining() > TimeSpan.Zero)
                return Decision(DatasetRecoveryAction.None);
            if (processExited || attempts != 0)
            {
                lastAction = DatasetRecoveryAction.ReplaceProcess;
                return Decision(lastAction);
            }
            attempts = 1;
            lastAction = DatasetRecoveryAction.CooperativeReset;
            return Decision(lastAction);
        }
    }

    public DatasetIncidentSnapshot RecordCooperativeResult(bool succeeded, Guid replacementGeneration)
    {
        lock (gate)
        {
            if (replacementGeneration != Guid.Empty) generationId = replacementGeneration;
            lastAction = DatasetRecoveryAction.CooperativeReset;
            return Snapshot();
        }
    }

    public DatasetIncidentSnapshot RecordProcessReplacement(bool succeeded, Guid replacementGeneration)
    {
        lock (gate)
        {
            replacements = checked(replacements + 1);
            if (replacementGeneration != Guid.Empty) generationId = replacementGeneration;
            if (!succeeded)
            {
                var now = timeProvider.GetTimestamp();
                while (replacementFailureTimes.TryPeek(out var oldest)
                       && timeProvider.GetElapsedTime(oldest, now) >= policy.ProcessReplacementWindow)
                    replacementFailureTimes.Dequeue();
                replacementFailureTimes.Enqueue(now);
                while (replacementFailureTimes.Count > policy.MaximumProcessReplacementsPerIncident)
                    replacementFailureTimes.Dequeue();
                replacementFailures = replacementFailureTimes.Count;
                var delay = replacementFailures switch { 1 => TimeSpan.FromSeconds(5), 2 => TimeSpan.FromSeconds(30), _ => TimeSpan.FromMinutes(2) };
                replacementAllowedTimestamp = checked(now + ToTimestampDelta(delay));
                if (replacementFailures >= policy.MaximumProcessReplacementsPerIncident)
                    Volatile.Write(ref replacementLatched, 1);
            }
            else replacementAllowedTimestamp = null;
            lastAction = DatasetRecoveryAction.ReplaceProcess;
            return Snapshot();
        }
    }

    public DatasetIncidentSnapshot ClearLatch(string operatorReason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operatorReason);
        lock (gate)
        {
            Volatile.Write(ref replacementLatched, 0);
            replacements = 0;
            replacementFailures = 0;
            replacementFailureTimes.Clear();
            replacementAllowedTimestamp = null;
            return Snapshot();
        }
    }

    public DatasetIncidentSnapshot Current
    {
        get { lock (gate) return Snapshot(); }
    }

    public void Hydrate(DatasetIncidentSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!string.Equals(snapshot.Dataset, dataset, StringComparison.Ordinal)
            || snapshot.ValueDate != valueDate || !snapshot.IsOpen
            || snapshot.IncidentId == Guid.Empty)
            throw new InvalidDataException("Persisted dataset incident identity is invalid.");
        lock (gate)
        {
            var now = timeProvider.GetTimestamp();
            openedTimestamp = checked(now - ToTimestampDelta(snapshot.UnhealthyDuration));
            healthyTimestamp = snapshot.HealthyDuration > TimeSpan.Zero
                ? checked(now - ToTimestampDelta(snapshot.HealthyDuration)) : null;
            policySession = snapshot.PolicySession;
            policyTimestamp = checked(now - ToTimestampDelta(snapshot.PolicySession is null
                ? snapshot.UnhealthyDuration : snapshot.PolicyUnhealthyDuration));
            replacementAllowedTimestamp = snapshot.ReplacementBackoffRemaining > TimeSpan.Zero
                ? checked(now + ToTimestampDelta(snapshot.ReplacementBackoffRemaining)) : null;
            replacementFailureTimes.Clear();
            foreach (var age in (snapshot.ReplacementFailureAges ?? []).Where(age => age >= TimeSpan.Zero
                         && age < policy.ProcessReplacementWindow).Take(policy.MaximumProcessReplacementsPerIncident))
                replacementFailureTimes.Enqueue(checked(now - ToTimestampDelta(age)));
            incidentId = snapshot.IncidentId;
            generationId = snapshot.GenerationId;
            attempts = snapshot.CooperativeAttempts;
            replacements = snapshot.ProcessReplacements;
            replacementFailures = snapshot.ProcessReplacementLatched
                ? policy.MaximumProcessReplacementsPerIncident : replacementFailureTimes.Count;
            reason = snapshot.FailureReason;
            lastAction = snapshot.LastAction;
            Volatile.Write(ref replacementLatched, snapshot.ProcessReplacementLatched ? 1 : 0);
        }
    }

    DatasetIncidentDecision ObserveHealthy(FuturesMarketState marketState)
    {
        if (openedTimestamp is null)
            return Decision(DatasetRecoveryAction.None);
        if (healthyTimestamp is null)
            healthyTimestamp = timeProvider.GetTimestamp();
        var required = marketState == FuturesMarketState.LiveTrading
            ? policy.LiveTradingHealthyQualificationPeriod
            : policy.OffTradingPollInterval;
        if (Elapsed(healthyTimestamp) >= required)
            Close();
        return Decision(DatasetRecoveryAction.None);
    }

    void Open(DatabentoDatasetFailureReason failureReason)
    {
        if (openedTimestamp is null)
        {
            openedTimestamp = timeProvider.GetTimestamp();
            policyTimestamp = openedTimestamp;
            incidentId = Guid.CreateVersion7(timeProvider.GetUtcNow());
            attempts = 0;
            replacements = 0;
            replacementFailures = 0;
            replacementFailureTimes.Clear();
            replacementAllowedTimestamp = null;
            Volatile.Write(ref replacementLatched, 0);
        }
        reason = failureReason;
    }

    void Close()
    {
        openedTimestamp = null;
        healthyTimestamp = null;
        policyTimestamp = null;
        policySession = null;
        replacementAllowedTimestamp = null;
        replacementFailureTimes.Clear();
        attempts = 0;
        replacements = 0;
        replacementFailures = 0;
        reason = DatabentoDatasetFailureReason.None;
        lastAction = DatasetRecoveryAction.None;
        Volatile.Write(ref replacementLatched, 0);
    }

    DatasetIncidentDecision Decision(DatasetRecoveryAction action) => new(action, Snapshot());

    DatasetIncidentSnapshot Snapshot() => new()
    {
        Dataset = dataset,
        ValueDate = valueDate,
        IncidentId = incidentId,
        GenerationId = generationId,
        IsOpen = openedTimestamp.HasValue,
        ProcessReplacementLatched = Volatile.Read(ref replacementLatched) != 0,
        CooperativeAttempts = attempts,
        ProcessReplacements = replacements,
        UnhealthyDuration = Elapsed(openedTimestamp),
        HealthyDuration = Elapsed(healthyTimestamp),
        PolicySession = policySession,
        PolicyUnhealthyDuration = Elapsed(policyTimestamp),
        ReplacementBackoffRemaining = ReplacementBackoffRemaining(),
        ReplacementFailureAges = Array.AsReadOnly(replacementFailureTimes.Select(value => Elapsed(value)).ToArray()),
        FailureReason = reason,
        LastAction = lastAction,
        ObservedOnUtc = timeProvider.GetUtcNow().UtcDateTime
    };

    TimeSpan Elapsed(long? timestamp) => timestamp is { } value
        ? timeProvider.GetElapsedTime(value, timeProvider.GetTimestamp()) : TimeSpan.Zero;

    TimeSpan ReplacementBackoffRemaining() => replacementAllowedTimestamp is { } value
        && value > timeProvider.GetTimestamp()
            ? timeProvider.GetElapsedTime(timeProvider.GetTimestamp(), value) : TimeSpan.Zero;

    long ToTimestampDelta(TimeSpan duration) => checked((long)Math.Round(
        duration.TotalSeconds * timeProvider.TimestampFrequency));
}
