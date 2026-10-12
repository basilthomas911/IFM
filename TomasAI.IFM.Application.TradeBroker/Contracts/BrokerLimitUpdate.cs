namespace TomasAI.IFM.Application.TradeBroker.Contracts;

public sealed record BrokerLimitUpdate(string AccountAlias, string BrokerOrderId, Guid OperationId, decimal NewSignedNetDebitLimit, int ExpectedRevision);
