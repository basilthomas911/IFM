namespace TomasAI.IFM.Application.MarketData.OperationsHealth;

/// <summary>Describes the latest bounded runtime outcome for Futures ITI processing.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "The serialized message enum has a fixed underlying width; preserve its wire representation.")]
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
    RouteDetached = 13
}
