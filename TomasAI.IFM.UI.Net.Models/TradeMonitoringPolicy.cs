using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.UI.Net.Models;

/// <summary>Defines the status and session rules for viewing established trades and starting monitoring.</summary>
public static class TradeMonitoringPolicy
{
    /// <summary>Open trades include daily MTM and sealed EOD positions; setup and execution states cannot be loaded.</summary>
    public static bool CanLoad(TradeState state) => state is TradeState.Open or TradeState.Closed;

    /// <summary>Allows production monitoring during futures trading hours and Development emulator monitoring at any time.</summary>
    public static bool CanStart(string environment, BrokerEnvironment broker, DateTimeOffset now)
        => string.Equals(environment, "Production", StringComparison.OrdinalIgnoreCase)
            ? FuturesTradingValueDate.TryGet(now, out _)
            : string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase) && broker == BrokerEnvironment.Emulator;
}
