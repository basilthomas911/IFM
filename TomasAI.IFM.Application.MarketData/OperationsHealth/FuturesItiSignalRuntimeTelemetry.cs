namespace TomasAI.IFM.Application.MarketData.OperationsHealth;

/// <summary>Describes the latest bounded runtime outcome for Futures ITI processing.</summary>
public enum FuturesItiRuntimeOutcome : byte
{
    Unknown = 0,
    RouteAttached = 1,
    Filtered = 2,
    InputUnavailable = 3,
    CommandRequested = 4,
    CommandAccepted = 5,
    CommandAcceptedNoChange = 6,
    SignalChanged = 7,
    EventCommitted = 8,
    ProjectionCompleted = 9,
    CompletionHandled = 10,
    WorkflowRequested = 11,
    Failed = 12,
    RouteDetached = 13,
    BusySkipped = 14
}

/// <summary>Immutable bounded snapshot of the live Futures ITI execution path.</summary>
public readonly record struct FuturesItiRuntimeSnapshot(
    long MarketPriceEvents,
    long FilteredEvents,
    long BusySkippedEvents,
    long EligibleEsTradeEvents,
    long CommandRequests,
    long AcceptedCommands,
    long NoChangeCommands,
    long ChangedSignals,
    long CommittedEvents,
    long ProjectionCompletions,
    long HandledCompletions,
    long WorkflowRequests,
    long Failures,
    DateTime? LastMarketPriceUtc,
    DateTime? LastEligibleEsTradeUtc,
    DateTime? LastCommandAcceptedUtc,
    DateTime? LastEventCommittedUtc,
    DateTime? LastProjectionCompletedUtc,
    DateTime? LastCompletionHandledUtc,
    DateTime? LastWorkflowRequestedUtc,
    FuturesItiRuntimeOutcome LastOutcome,
    string LastReason);

/// <summary>
/// Records fixed-cardinality, process-local Futures ITI health without retaining messages or polling storage.
/// </summary>
public sealed class FuturesItiSignalRuntimeTelemetry(TimeProvider timeProvider)
{
    long marketPriceEvents;
    long filteredEvents;
    long busySkippedEvents;
    long eligibleEsTradeEvents;
    long commandRequests;
    long acceptedCommands;
    long noChangeCommands;
    long changedSignals;
    long committedEvents;
    long projectionCompletions;
    long handledCompletions;
    long workflowRequests;
    long failures;
    long lastMarketPriceTicks;
    long lastEligibleEsTradeTicks;
    long lastCommandAcceptedTicks;
    long lastEventCommittedTicks;
    long lastProjectionCompletedTicks;
    long lastCompletionHandledTicks;
    long lastWorkflowRequestedTicks;
    int lastOutcome;
    int inputUnavailableActive;
    string lastReason = string.Empty;

    /// <summary>Records successful realtime route attachment.</summary>
    public void RecordRouteAttached() => SetOutcome(FuturesItiRuntimeOutcome.RouteAttached, "Market-price route attached.");

    /// <summary>Records orderly realtime route detachment.</summary>
    public void RecordRouteDetached() => SetOutcome(FuturesItiRuntimeOutcome.RouteDetached, "Market-price route detached.");

    /// <summary>Records receipt of one normalized market-price event.</summary>
    public void RecordMarketPriceReceived(DateTime eventTimeUtc)
    {
        Interlocked.Increment(ref marketPriceEvents);
        Interlocked.Exchange(ref lastMarketPriceTicks, Normalize(eventTimeUtc).Ticks);
    }

    /// <summary>Records an intentionally ignored market-price event.</summary>
    public void RecordFiltered(string reason)
    {
        Interlocked.Increment(ref filteredEvents);
    }

    /// <summary>Records a realtime tick ignored because one Generate operation is already active.</summary>
    public void RecordBusySkipped()
    {
        Interlocked.Increment(ref busySkippedEvents);
        SetOutcome(FuturesItiRuntimeOutcome.BusySkipped, "Generate operation already active.");
    }

    /// <summary>Records an eligible current ES trade that requires Daily ITI evaluation.</summary>
    public void RecordEligibleEsTrade(DateTime eventTimeUtc)
    {
        Interlocked.Increment(ref eligibleEsTradeEvents);
        Interlocked.Exchange(ref lastEligibleEsTradeTicks, Normalize(eventTimeUtc).Ticks);
    }

    /// <summary>Records a temporarily unavailable required market input.</summary>
    /// <returns><see langword="true"/> when the outcome changed and a warning should be emitted.</returns>
    public bool RecordInputUnavailable(string reason)
    {
        var firstOccurrence = Interlocked.Exchange(ref inputUnavailableActive, 1) == 0;
        SetOutcome(FuturesItiRuntimeOutcome.InputUnavailable, reason);
        return firstOccurrence;
    }

    /// <summary>Records dispatch of a Daily Generate command.</summary>
    public void RecordCommandRequested()
    {
        Interlocked.Exchange(ref inputUnavailableActive, 0);
        Interlocked.Increment(ref commandRequests);
        SetOutcome(FuturesItiRuntimeOutcome.CommandRequested, "Daily Generate command requested.");
    }

    /// <summary>Records an accepted Generate command reply.</summary>
    public void RecordCommandAccepted()
    {
        Interlocked.Increment(ref acceptedCommands);
        Interlocked.Exchange(ref lastCommandAcceptedTicks, UtcNowTicks());
        SetOutcome(FuturesItiRuntimeOutcome.CommandAccepted, "Generate command accepted.");
    }

    /// <summary>Records valid evaluation that produced no material signal change.</summary>
    public void RecordNoChange()
    {
        Interlocked.Increment(ref noChangeCommands);
        SetOutcome(FuturesItiRuntimeOutcome.CommandAcceptedNoChange, "Generate command completed with no material signal change.");
    }

    /// <summary>Records creation of a material signal state transition before durable save.</summary>
    public void RecordSignalChanged()
    {
        Interlocked.Increment(ref changedSignals);
        SetOutcome(FuturesItiRuntimeOutcome.SignalChanged, "Material ITI signal change created.");
    }

    /// <summary>Records successful durable commit of an ITI source event.</summary>
    public void RecordEventCommitted()
    {
        Interlocked.Increment(ref committedEvents);
        Interlocked.Exchange(ref lastEventCommittedTicks, UtcNowTicks());
        SetOutcome(FuturesItiRuntimeOutcome.EventCommitted, "ITI source event committed.");
    }

    /// <summary>Records successful durable projection and terminal completion publication.</summary>
    public void RecordProjectionCompleted()
    {
        Interlocked.Increment(ref projectionCompletions);
        Interlocked.Exchange(ref lastProjectionCompletedTicks, UtcNowTicks());
        SetOutcome(FuturesItiRuntimeOutcome.ProjectionCompleted, "ITI projection completed.");
    }

    /// <summary>Records successful processing of an ITI Generate completion.</summary>
    public void RecordCompletionHandled()
    {
        Interlocked.Increment(ref handledCompletions);
        Interlocked.Exchange(ref lastCompletionHandledTicks, UtcNowTicks());
        SetOutcome(FuturesItiRuntimeOutcome.CompletionHandled, "ITI completion handled.");
    }

    /// <summary>Records a timeframe-driven strategy workflow request.</summary>
    public void RecordWorkflowRequested()
    {
        Interlocked.Increment(ref workflowRequests);
        Interlocked.Exchange(ref lastWorkflowRequestedTicks, UtcNowTicks());
        SetOutcome(FuturesItiRuntimeOutcome.WorkflowRequested, "Strategy workflow requested.");
    }

    /// <summary>Records one classified processing failure.</summary>
    public void RecordFailure(string reason)
    {
        Interlocked.Increment(ref failures);
        SetOutcome(FuturesItiRuntimeOutcome.Failed, reason);
    }

    /// <summary>Reads a consistent-enough immutable operational snapshot without taking a lock.</summary>
    public FuturesItiRuntimeSnapshot GetSnapshot() => new(
        Volatile.Read(ref marketPriceEvents),
        Volatile.Read(ref filteredEvents),
        Volatile.Read(ref busySkippedEvents),
        Volatile.Read(ref eligibleEsTradeEvents),
        Volatile.Read(ref commandRequests),
        Volatile.Read(ref acceptedCommands),
        Volatile.Read(ref noChangeCommands),
        Volatile.Read(ref changedSignals),
        Volatile.Read(ref committedEvents),
        Volatile.Read(ref projectionCompletions),
        Volatile.Read(ref handledCompletions),
        Volatile.Read(ref workflowRequests),
        Volatile.Read(ref failures),
        ReadTime(Volatile.Read(ref lastMarketPriceTicks)),
        ReadTime(Volatile.Read(ref lastEligibleEsTradeTicks)),
        ReadTime(Volatile.Read(ref lastCommandAcceptedTicks)),
        ReadTime(Volatile.Read(ref lastEventCommittedTicks)),
        ReadTime(Volatile.Read(ref lastProjectionCompletedTicks)),
        ReadTime(Volatile.Read(ref lastCompletionHandledTicks)),
        ReadTime(Volatile.Read(ref lastWorkflowRequestedTicks)),
        (FuturesItiRuntimeOutcome)Volatile.Read(ref lastOutcome),
        Volatile.Read(ref lastReason));

    void SetOutcome(FuturesItiRuntimeOutcome outcome, string reason)
    {
        Volatile.Write(ref lastReason, reason);
        Volatile.Write(ref lastOutcome, (int)outcome);
    }

    long UtcNowTicks() => timeProvider.GetUtcNow().UtcDateTime.Ticks;

    static DateTime Normalize(DateTime value) => value.Kind == DateTimeKind.Utc
        ? value
        : value.ToUniversalTime();

    static DateTime? ReadTime(long ticks)
    {
        return ticks <= 0 ? null : new DateTime(ticks, DateTimeKind.Utc);
    }
}
