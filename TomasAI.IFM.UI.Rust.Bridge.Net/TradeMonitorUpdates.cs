using System.ComponentModel;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

namespace TomasAI.IFM.UI.Rust.Bridge.Net;

/// <summary>Retains live plans for one open monitor and coalesces its display notifications.</summary>
public sealed class TradeMonitorUpdates : IDisposable
{
    readonly INotifyPropertyChanged source;
    readonly Func<StrategyTradePlanSnapshot?> currentPlan;
    readonly Func<bool> isLive;
    readonly Action changed;
    readonly object gate = new();
    readonly Dictionary<string, StrategyTradePlanSnapshot> livePlans = new(StringComparer.Ordinal);
    bool disposed;

    public TradeMonitorUpdates(INotifyPropertyChanged source, Func<StrategyTradePlanSnapshot?> currentPlan,
        Func<bool> isLive, Action changed)
    {
        this.source=source;this.currentPlan=currentPlan;this.isLive=isLive;this.changed=changed;
        source.PropertyChanged+=OnChanged;
    }
    static string Identity(StrategyTradePlanSnapshot plan)
        => $"{plan.Position.Id.Format()}/{plan.ValueDate}/{plan.Position.RouteGeneration}/{plan.PlanRevision}";

    void OnChanged(object? sender, PropertyChangedEventArgs args)
    {
        lock(gate)
        {
            if(disposed)return;
            if(args.PropertyName=="IronCondorPlanHistory" && !isLive())livePlans.Clear();
            if(isLive() && args.PropertyName is "StrategyTradePlan" or "IsLiveFeedEnabled" or null or "")
            {
                if(currentPlan() is {} plan)livePlans.TryAdd(Identity(plan),plan);
            }
            changed();
        }
    }
    public IReadOnlyList<StrategyTradePlanSnapshot> Plans(IEnumerable<StrategyTradePlanSnapshot> saved)
    {
        lock(gate)
        {
            // Prefer the live observation when persisted history catches up with the same version.
            return saved.Concat(livePlans.Values).GroupBy(Identity,StringComparer.Ordinal).Select(group=>group.Last())
                .OrderByDescending(plan=>plan.Position.RouteGeneration).ThenByDescending(plan=>plan.PlanRevision)
                .ThenByDescending(plan=>plan.CalculatedAtUtc).ToArray();
        }
    }
    public void Dispose()
    {
        lock(gate)
        {
            if(disposed)return;disposed=true;source.PropertyChanged-=OnChanged;livePlans.Clear();
        }
    }
}
