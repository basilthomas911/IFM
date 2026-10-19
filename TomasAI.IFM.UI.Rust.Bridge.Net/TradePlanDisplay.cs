using System.Globalization;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.UI.Net.Models;

namespace TomasAI.IFM.UI.Rust.Bridge.Net;

/// <summary>Display-only timestamps; persisted plan times remain UTC.</summary>
public static class TradePlanDisplay
{
    /// <summary>History grids need scalar plan fields; full leg evidence is sent once through the active position.</summary>
    public static byte[] EncodeHistory(StrategyTradePlanSnapshot plan)
    {
        var fields = BridgeDocument.Map(plan, camelCase: true, enumNames: true);
        fields.Remove("position");
        fields.Remove("parameters");
        if (fields.GetValueOrDefault("ironCondorTradePlanSnapshot") is Dictionary<string, object?> detail)
        {
            detail.Remove("position");
            detail.Remove("legs");
            detail.Remove("legacyLegs");
            detail.Remove("strategyRiskParameterSet");
        }
        return BridgeDocument.Encode(fields);
    }

    public static string? ActionTime(StrategyTradePlanSnapshot plan)
    {
        var utc = plan.IronCondorTradePlanSnapshot?.ActionDateTime ?? plan.CalculatedAtUtc;
        return utc == default ? null
            : EasternTime.FromUtc(utc).ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
    }
}
