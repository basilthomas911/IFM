namespace TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

/// <summary>Controls development monitoring notifications independently of plan calculation and persistence.</summary>
public static class TradePlanPublication
{
    /// <summary>Publishes the first session snapshot and subsequent changes in unrounded currency Daily PnL.</summary>
    /// <param name="previous">The preceding plan for this publication stream.</param>
    /// <param name="current">The newly calculated plan, including unavailable observations.</param>
    /// <returns>Whether a monitoring notification is required; missing PnL never replaces a known publication.</returns>
    public static bool ShouldPublish(StrategyTradePlanSnapshot? previous, StrategyTradePlanSnapshot current)
    {
        ArgumentNullException.ThrowIfNull(current);
        return previous is null || previous.Position.Id != current.Position.Id || previous.ValueDate != current.ValueDate
            || current.IronCondorTradePlanSnapshot?.DailyPnl is { } pnl
                && previous.IronCondorTradePlanSnapshot?.DailyPnl != pnl;
    }
}
