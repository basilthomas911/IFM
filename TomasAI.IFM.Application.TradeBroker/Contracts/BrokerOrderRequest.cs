namespace TomasAI.IFM.Application.TradeBroker.Contracts;

/// <summary>A single approved logical component and its signed net debit price envelope.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "This published actor message uses arrays as its existing serialized payload contract.")]
public sealed record BrokerOrderRequest(string AccountAlias, BrokerEnvironment Environment, string BrokerOrderId,
    Guid OperationId, Guid ComponentId, BrokerOrderShape Shape, bool IsClosing, BrokerOrderLeg[] Legs,
    decimal SignedNetDebitLimit, decimal MinimumLimit, decimal MaximumLimit, decimal TickIncrement,
    DateTime ValidUntilUtc, string ApprovalHash, string ContractReferenceHash, decimal RequiredCapital, decimal MaximumLoss,
    BrokerOrderType OrderType = BrokerOrderType.Limit, BrokerAlgorithm Algorithm = BrokerAlgorithm.None,
    string TimeInForce = "Day", string AlgorithmPace = "Normal");
