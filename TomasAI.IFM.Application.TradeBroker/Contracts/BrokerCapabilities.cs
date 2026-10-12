namespace TomasAI.IFM.Application.TradeBroker.Contracts;

/// <summary>Explicit capabilities loaded for one adapter/account binding.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "This published actor message uses arrays as its existing serialized payload contract.")]
public sealed record BrokerCapabilities(string Adapter, string AccountAlias, BrokerEnvironment Environment,
    BrokerOrderShape[] Shapes, BrokerOrderType[] OrderTypes, BrokerAlgorithm[] Algorithms)
{
    public static BrokerCapabilities Emulator(string accountAlias) => new("InteractiveBrokersEmulator", accountAlias,
        BrokerEnvironment.Emulator,
        [BrokerOrderShape.FuturesOutright, BrokerOrderShape.VerticalSpread, BrokerOrderShape.IronCondor],
        [BrokerOrderType.Market, BrokerOrderType.Limit],
        [BrokerAlgorithm.None, BrokerAlgorithm.Adaptive]);

    public static BrokerCapabilities InteractiveBrokers(string accountAlias, BrokerEnvironment environment)
    {
        if (environment is not (BrokerEnvironment.Paper or BrokerEnvironment.Live))
            throw new ArgumentOutOfRangeException(nameof(environment), "IBKR accounts must be explicitly Paper or Live.");
        return new("InteractiveBrokers", accountAlias, environment,
            [BrokerOrderShape.FuturesOutright, BrokerOrderShape.VerticalSpread, BrokerOrderShape.IronCondor],
            [BrokerOrderType.Market, BrokerOrderType.Limit],
            [BrokerAlgorithm.None, BrokerAlgorithm.Adaptive]);
    }

    public string? Validate(BrokerOrderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!string.Equals(request.AccountAlias, AccountAlias, StringComparison.Ordinal)
            || request.Environment != Environment)
            return "Broker environment or account does not match the loaded adapter/account.";
        if (!Shapes.Contains(request.Shape)) return $"Order shape {request.Shape} is not supported.";
        if (!OrderTypes.Contains(request.OrderType)) return $"Order type {request.OrderType} is not supported.";
        if (request.TimeInForce is not ("Day" or "GTC") || request.AlgorithmPace is not ("Patient" or "Normal" or "Urgent"))
            return "Unsupported time in force or algorithm pace.";
        if (!Algorithms.Contains(request.Algorithm)) return $"Algorithm {request.Algorithm} is not supported.";
        if (request.Algorithm == BrokerAlgorithm.Adaptive
            && request.OrderType is not (BrokerOrderType.Market or BrokerOrderType.Limit))
            return "Adaptive is supported only for basic Market and Limit orders.";
        return null;
    }
}
