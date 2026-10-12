namespace TomasAI.IFM.Application.TradeBroker.Contracts;

/// <summary>Broker-neutral exact contract/ratio in an approved order.</summary>
public sealed record BrokerOrderLeg(Guid LegId, string ContractId, int SignedQuantity, decimal? Strike, DateOnly? Expiry, byte? PutCall, decimal CashMultiplier);
