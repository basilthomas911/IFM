namespace TomasAI.IFM.Domain.Trade.Order.Broker.Model;
/// <summary>Immutable evidence consumed by one pure micro-execution decision.</summary>
public sealed record MicroExecutionInput(MicroExecutionProfile Profile, bool Opening, bool GateAllowsNewRisk, bool BrokerAcknowledged, bool MutationInFlight, bool OutcomeUnknown, decimal CurrentLimit, decimal MinimumLimit, decimal MaximumLimit, decimal TickIncrement, decimal Bid, decimal Ask, DateTime QuoteAtUtc, DateTime NowUtc, DateTime DeadlineUtc, DateTime? LastMutationAtUtc, int PriceChangeCount);
