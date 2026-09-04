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
    long openedTimestamp = -1;
    long healthyTimestamp = -1;
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

            if (healthy)
                return ObserveHealthy(marketState);

            Open(failureReason);
            healthyTimestamp = -1;
            if (Volatile.Read(ref replacementLatched) != 0)
                return Decision(DatasetRecoveryAction.None);

            var unhealthy = Elapsed(openedTimestamp);
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
            healthyTimestamp = -1;
            if (Volatile.Read(ref replacementLatched) != 0)
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
            generationId = replacementGeneration;
            lastAction = DatasetRecoveryAction.CooperativeReset;
            return Snapshot();
        }
    }

    public DatasetIncidentSnapshot RecordProcessReplacement(bool succeeded, Guid replacementGeneration)
    {
        lock (gate)
        {
            replacements = checked(replacements + 1);
            generationId = replacementGeneration;
            if (!succeeded)
            {
                replacementFailures = checked(replacementFailures + 1);
                if (replacementFailures >= policy.MaximumProcessReplacementsPerIncident)
                    Volatile.Write(ref replacementLatched, 1);
            }
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
                ? checked(now - ToTimestampDelta(snapshot.HealthyDuration)) : -1;
            incidentId = snapshot.IncidentId;
            generationId = snapshot.GenerationId;
            attempts = snapshot.CooperativeAttempts;
            replacements = snapshot.ProcessReplacements;
            replacementFailures = snapshot.ProcessReplacementLatched
                ? policy.MaximumProcessReplacementsPerIncident : 0;
            reason = snapshot.FailureReason;
            lastAction = snapshot.LastAction;
            Volatile.Write(ref replacementLatched, snapshot.ProcessReplacementLatched ? 1 : 0);
        }
    }

    DatasetIncidentDecision ObserveHealthy(FuturesMarketState marketState)
    {
        if (openedTimestamp < 0)
            return Decision(DatasetRecoveryAction.None);
        if (healthyTimestamp < 0)
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
        if (openedTimestamp < 0)
        {
            openedTimestamp = timeProvider.GetTimestamp();
            incidentId = Guid.CreateVersion7(timeProvider.GetUtcNow());
            attempts = 0;
            replacements = 0;
            replacementFailures = 0;
            Volatile.Write(ref replacementLatched, 0);
        }
        reason = failureReason;
    }

    void Close()
    {
        openedTimestamp = -1;
        healthyTimestamp = -1;
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
        IsOpen = openedTimestamp >= 0,
        ProcessReplacementLatched = Volatile.Read(ref replacementLatched) != 0,
        CooperativeAttempts = attempts,
        ProcessReplacements = replacements,
        UnhealthyDuration = Elapsed(openedTimestamp),
        HealthyDuration = Elapsed(healthyTimestamp),
        FailureReason = reason,
        LastAction = lastAction,
        ObservedOnUtc = timeProvider.GetUtcNow().UtcDateTime
    };

    TimeSpan Elapsed(long timestamp) => timestamp < 0
        ? TimeSpan.Zero
        : timeProvider.GetElapsedTime(timestamp, timeProvider.GetTimestamp());

    long ToTimestampDelta(TimeSpan duration) => checked((long)Math.Round(
        duration.TotalSeconds * timeProvider.TimestampFrequency));
}
