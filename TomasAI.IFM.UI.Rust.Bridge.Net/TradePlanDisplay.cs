using MessagePack;
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
        ArgumentNullException.ThrowIfNull(plan);
        using var buffer = new PooledBridgeBuffer();
        var writer = new MessagePackWriter(buffer);
        writer.WriteMapHeader(HistoryShape.Root.Length);
        foreach (var member in HistoryShape.Root)
        {
            writer.Write(member.CamelName);
            if (member.Name == nameof(StrategyTradePlanSnapshot.IronCondorTradePlanSnapshot))
                WriteDetail(ref writer, plan.IronCondorTradePlanSnapshot);
            else member.Write(ref writer, plan, true, true, 1, false);
        }
        writer.Flush();
        return buffer.ToOwnedArray();
    }

    static void WriteDetail(ref MessagePackWriter writer, IronCondorTradePlanSnapshot? detail)
    {
        if (detail is null) { writer.WriteNil(); return; }
        writer.WriteMapHeader(HistoryShape.Detail.Length);
        foreach (var member in HistoryShape.Detail)
        {
            writer.Write(member.CamelName);
            member.Write(ref writer, detail, true, true, 2, false);
        }
    }

    // Descriptor filtering happens once. Excluded evidence getters are never invoked.
    static class HistoryShape
    {
        internal static readonly DisplayProperty[] Root = DisplayDescriptors.For(typeof(StrategyTradePlanSnapshot))
            .Where(member => member.CamelName is not ("position" or "parameters")).ToArray();
        internal static readonly DisplayProperty[] Detail = DisplayDescriptors.For(typeof(IronCondorTradePlanSnapshot))
            .Where(member => member.CamelName is not ("position" or "legs" or "legacyLegs" or "strategyRiskParameterSet")).ToArray();
    }

    public static string? ActionTime(StrategyTradePlanSnapshot plan)
    {
        var utc = plan.IronCondorTradePlanSnapshot?.ActionDateTime ?? plan.CalculatedAtUtc;
        return utc == default ? null
            : EasternTime.FromUtc(utc).ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
    }
}
