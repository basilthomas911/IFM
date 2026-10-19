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
    bool collectingLivePlans;

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
            if(args.PropertyName=="IsLiveFeedEnabled" && !isLive()) { collectingLivePlans=false; livePlans.Clear(); }
            if((collectingLivePlans || isLive()) && args.PropertyName=="StrategyTradePlan")
            {
                if(currentPlan() is {} plan)livePlans.TryAdd(Identity(plan),plan);
                while(livePlans.Count>100)livePlans.Remove(livePlans.MinBy(pair=>(pair.Value.Position.RouteGeneration,pair.Value.PlanRevision)).Key);
            }
            changed();
        }
    }
    /// <summary>Starts a new live collection before subscribing; a previously loaded database plan is never seeded.</summary>
    public void BeginLive()
    {
        lock(gate) { collectingLivePlans=true; livePlans.Clear(); changed(); }
    }
    /// <summary>Releases live rows when monitoring stops or startup fails.</summary>
    public void EndLive()
    {
        lock(gate) { collectingLivePlans=false; livePlans.Clear(); changed(); }
    }
    /// <summary>Selects exactly one source: generated notifications while live, the supplied database page while stopped.</summary>
    public IReadOnlyList<StrategyTradePlanSnapshot> Plans(IEnumerable<StrategyTradePlanSnapshot> saved)
    {
        lock(gate)
        {
            if(!isLive())return saved.ToArray();
            return livePlans.Values.OrderByDescending(plan=>plan.Position.RouteGeneration).ThenByDescending(plan=>plan.PlanRevision)
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
