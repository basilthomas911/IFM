namespace TomasAI.IFM.Application.TradeBroker.Contracts;
public sealed record BrokerCancelRequest(string AccountAlias, string BrokerOrderId, Guid OperationId, int ExpectedRevision);
