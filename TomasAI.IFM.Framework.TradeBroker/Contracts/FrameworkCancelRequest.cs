namespace TomasAI.IFM.Framework.TradeBroker.Contracts;

/// <summary>An individual cancel request for a known logical order.</summary>
public sealed record FrameworkCancelRequest(
    string AccountAlias,
    string BrokerOrderId,
    Guid OperationId,
    int ExpectedRevision);
