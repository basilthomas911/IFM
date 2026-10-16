using System.Globalization;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.UI.Net.Models;

namespace TomasAI.IFM.UI.Rust.Bridge.Net;

/// <summary>Display-only timestamps; persisted plan times remain UTC.</summary>
public static class TradePlanDisplay
{
    public static string? ActionTime(StrategyTradePlanSnapshot plan)
    {
        var utc = plan.IronCondorTradePlanSnapshot?.ActionDateTime ?? plan.CalculatedAtUtc;
        return utc == default ? null
            : EasternTime.FromUtc(utc).ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
    }
}
