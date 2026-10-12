using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Plan.Model;
public static class FuturesTradePlanComparisonModel
{
    public static bool IsMaterial(StrategyTradePlanSnapshot? previous, StrategyTradePlanSnapshot current)
    {
        ArgumentNullException.ThrowIfNull(current);
        return previous is null || current.RequiresExit || previous.State != current.State || previous.Action != current.Action || previous.Position.Phase != current.Position.Phase || Math.Abs(previous.TotalPnl - current.TotalPnl) >= current.Parameters.MaterialPnlChange || Math.Abs(previous.CurrentValue - current.CurrentValue) >= current.Parameters.MaterialPriceChange;
    }
}
