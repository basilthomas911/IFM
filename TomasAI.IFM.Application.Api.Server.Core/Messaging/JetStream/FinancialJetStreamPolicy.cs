namespace TomasAI.IFM.Application.Api.Server.Core.Messaging.JetStream;

/// <summary>Projectors whose committed effects can place trades or change financial state.</summary>
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
        "PortfolioEventProjector",
        "PortfolioFinancialPolicyEventProjector",
        "PortfolioFundEventProjector",
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
