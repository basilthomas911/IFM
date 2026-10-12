using MessagePack;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
// Preparation evidence never implies that a signal is warm or fresh.
public enum ParameterSignalPreparationStatus { NotRequested = 0, ExistingRoute = 1, Accepted = 2, Failed = 3, TimedOut = 4, MarketClosed = 5, Cancelled = 6 }
