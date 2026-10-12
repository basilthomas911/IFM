namespace TomasAI.IFM.Framework.TradeBroker.Contracts;

/// <summary>One approved, separately executable order component.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized message/configuration contract, including enum widths, numeric values, and collection shape.")]
public sealed record FrameworkOrderRequest(
    string AccountAlias,
    string BrokerOrderId,
    Guid OperationId,
    Guid ComponentId,
    FrameworkOrderShape Shape,
    bool IsClosing,
    FrameworkOrderLeg[] Legs,
    decimal SignedNetDebitLimit,
    decimal MinimumLimit,
    decimal MaximumLimit,
    decimal TickIncrement,
    DateTime ValidUntilUtc,
    string ApprovalHash,
    string ContractReferenceHash,
    decimal RequiredCapital,
    decimal MaximumLoss,
    FrameworkOrderType OrderType = FrameworkOrderType.Limit,
    FrameworkOrderAlgorithm Algorithm = FrameworkOrderAlgorithm.None,
    string TimeInForce = "Day", string AlgorithmPace = "Normal");
