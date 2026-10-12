namespace TomasAI.IFM.Domain.Trade.Order.Broker.Model;
/// <summary>Versioned deterministic controls for opening or closing execution.</summary>
public sealed record MicroExecutionProfile(string Id, int Version, string ContentHash, TimeSpan MinimumUpdateCadence, TimeSpan MaximumQuoteAge, int MaximumPriceChanges, bool UrgentClosing);
