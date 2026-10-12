namespace TomasAI.IFM.Application.MarketData.OperationsHealth;

/// <summary>Immutable bounded snapshot of the live Futures ITI execution path.</summary>
public readonly record struct FuturesItiRuntimeSnapshot(
    long MarketPriceEvents,
    long FilteredEvents,
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
