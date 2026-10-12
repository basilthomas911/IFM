namespace TomasAI.IFM.Framework.TradeBroker.Contracts;

/// <summary>The bounded price-only change authorized by the current order envelope.</summary>
public sealed record FrameworkLimitUpdate(
    string AccountAlias,
    string BrokerOrderId,
    Guid OperationId,
    decimal NewSignedNetDebitLimit,
    int ExpectedRevision);
