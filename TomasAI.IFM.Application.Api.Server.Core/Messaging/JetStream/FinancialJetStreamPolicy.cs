namespace TomasAI.IFM.Application.Api.Server.Core.Messaging.JetStream;

/// <summary>Projectors whose committed effects require durable delivery.</summary>
internal static class FinancialJetStreamPolicy
{
    internal static readonly string[] DurableProjectors =
    [
        "BrokerOrderEventProjector",
        "CapacityLifecycleProjector",
        "EmulatorExecutionProjector",
        "FundEventProjector",
        "FundTransactionEventProjector",
        "FuturesExitPositionWorkflowEventProjector",
        "FuturesOptionTradeEventProjector",
        "FuturesPositionEventProjector",
        "FuturesTradeEventProjector",
        "GeneralLedgerProjector",
        "IntrinsicTimeStrategyWorkflowEventProjector",
        "IronCondorExitPositionWorkflowEventProjector",
        "IronCondorPositionEventProjector",
        "LedgerConfigurationProjector",
        "OptionTradeEventProjector",
        "OrderExecutionEventProjector",
        "ParameterAssignmentEventProjector",
        "ParameterSetEventProjector",
        "ParameterStartupEventProjector",
        "PortfolioEventProjector",
        "PortfolioFinancialPolicyEventProjector",
        "PortfolioFundEventProjector",
        "RegimeDiscoveryConfigurationEventProjector",
        "StrategyCatalogEventProjector",
        "TradeOrderEventProjector",
        "VerticalSpreadExitPositionWorkflowEventProjector",
        "VerticalSpreadPositionEventProjector"
    ];

    static readonly HashSet<string> DurableNames = new(DurableProjectors, StringComparer.Ordinal);

    internal static bool ShouldPurgeProjectorStream(string streamName)
    {
        if (!streamName.StartsWith("IFM_", StringComparison.Ordinal))
            return false;
        var suffix = streamName.EndsWith("_PROCESS", StringComparison.Ordinal) ? "_PROCESS"
            : streamName.EndsWith("_REPLAY", StringComparison.Ordinal) ? "_REPLAY" : null;
        if (suffix is null) return false;
        var projectorName = streamName[4..^suffix.Length];
        return projectorName.EndsWith("Projector", StringComparison.Ordinal)
            && !DurableNames.Contains(projectorName);
    }
}
