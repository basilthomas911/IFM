namespace TomasAI.IFM.Application.TradeBroker.Contracts;
public sealed record BrokerDispatchReceipt(BrokerDispatchOutcome Outcome, Guid OperationId, string BrokerOrderId, string Category, string Detail, DateTime RecordedAtUtc);
