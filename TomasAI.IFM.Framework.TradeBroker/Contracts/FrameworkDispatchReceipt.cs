namespace TomasAI.IFM.Framework.TradeBroker.Contracts;

/// <summary>The local result of passing an operation to the provider boundary.</summary>
public sealed record FrameworkDispatchReceipt(
    FrameworkDispatchOutcome Outcome,
    Guid OperationId,
    string BrokerOrderId,
    string Category,
    string Detail,
    DateTime RecordedAtUtc);
