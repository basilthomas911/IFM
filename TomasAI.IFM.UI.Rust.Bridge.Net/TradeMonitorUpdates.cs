using System.ComponentModel;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

namespace TomasAI.IFM.UI.Rust.Bridge.Net;

/// <summary>Independent display revisions; a quote update does not invalidate plan history.</summary>
public readonly record struct TradeMonitorRevisions(long History, long Position, long Limits, long Presentation, long SavedPositions = 0);

/// <summary>Retains live plans for one open monitor and coalesces its display notifications.</summary>
public sealed class TradeMonitorUpdates : IDisposable
{
    readonly record struct Identity(StrategyPositionId Position, DateOnly ValueDate, long RouteGeneration, long PlanRevision);
    readonly INotifyPropertyChanged source;
    readonly Func<StrategyTradePlanSnapshot?> currentPlan;
    readonly Func<bool> isLive;
    readonly Action changed;
    readonly object gate = new();
    readonly Dictionary<Identity, StrategyTradePlanSnapshot> livePlans = new();
    StrategyTradePlanSnapshot[]? ordered;
    DateOnly? orderedDate;
    TradeMonitorRevisions revisions;
    bool disposed, collectingLivePlans;

    public TradeMonitorUpdates(INotifyPropertyChanged source, Func<StrategyTradePlanSnapshot?> currentPlan,
        Func<bool> isLive, Action changed)
    {
        this.source = source; this.currentPlan = currentPlan; this.isLive = isLive; this.changed = changed;
        source.PropertyChanged += OnChanged;
    }
    public TradeMonitorRevisions Revisions { get { lock (gate) return revisions; } }
    static Identity Key(StrategyTradePlanSnapshot plan)
        => new(plan.Position.Id, plan.ValueDate, plan.Position.RouteGeneration, plan.PlanRevision);

    void CapturePlan()
    {
        if ((!collectingLivePlans && !isLive()) || currentPlan() is not { } plan) return;
        var key = Key(plan);
        if (livePlans.TryGetValue(key, out var prior) && prior == plan) return;
        livePlans[key] = plan;
        while (livePlans.Count > 100)
            livePlans.Remove(livePlans.MinBy(pair => (pair.Value.Position.RouteGeneration, pair.Value.PlanRevision)).Key);
        ordered = null;
        revisions = revisions with { History = revisions.History + 1 };
    }
    void ClearPlans()
    { livePlans.Clear(); ordered = null; revisions = revisions with { History = revisions.History + 1 }; }
    void InvalidateAll()
        => revisions = new(revisions.History + 1, revisions.Position + 1, revisions.Limits + 1, revisions.Presentation + 1, revisions.SavedPositions + 1);

    void OnChanged(object? sender, PropertyChangedEventArgs args)
    {
        lock (gate)
        {
            if (disposed) return;
            switch (args.PropertyName)
            {
                case "IsLiveFeedEnabled":
                    if (!isLive()) { collectingLivePlans = false; ClearPlans(); }
                    InvalidateAll(); break;
                case "StrategyTradePlan":
                    CapturePlan(); revisions = revisions with { Presentation = revisions.Presentation + 1 }; break;
                case "IronCondorPlanHistory":
                    ordered = null; revisions = revisions with { History = revisions.History + 1 }; break;
                case "TradeHistory":
                    revisions = revisions with { SavedPositions = revisions.SavedPositions + 1 }; break;
                case "RealtimeSnapshot": case "SelectedSavedPosition": case "SavedLegObservations":
                    revisions = revisions with { Position = revisions.Position + 1 }; break;
                case "TradeLimitSnapshot":
                    revisions = revisions with { Limits = revisions.Limits + 1 }; break;
                case "LiveFeedStatusMessage": case "CanEnableLiveFeed": case "CanChangeLiveFeed": case "CanSelectPositionHistory":
                    revisions = revisions with { Presentation = revisions.Presentation + 1 }; break;
                case null: case "":
                    CapturePlan(); ordered = null; InvalidateAll(); break;
                default:
                    // Unknown notifications refresh mutable display sections without discarding immutable plan rows.
                    revisions = revisions with { Position = revisions.Position + 1, Limits = revisions.Limits + 1,
                        Presentation = revisions.Presentation + 1, SavedPositions = revisions.SavedPositions + 1 }; break;
            }
            // The callback only marks workspace revisions. Keeping it inside the lifetime gate fences disposal.
            changed();
        }
    }
    public void BeginLive()
    { lock (gate) { if (disposed) return; collectingLivePlans = true; ClearPlans(); InvalidateAll(); changed(); } }
    public void EndLive()
    { lock (gate) { if (disposed) return; collectingLivePlans = false; ClearPlans(); InvalidateAll(); changed(); } }

    /// <summary>Returns one immutable ordered display window until history itself changes.</summary>
    public IReadOnlyList<StrategyTradePlanSnapshot> Plans(IEnumerable<StrategyTradePlanSnapshot> saved, DateOnly? valueDate = null)
    {
        lock (gate)
        {
            if (disposed) return Array.Empty<StrategyTradePlanSnapshot>();
            if (!isLive()) return saved as IReadOnlyList<StrategyTradePlanSnapshot> ?? saved.ToArray();
            if (ordered is null || orderedDate != valueDate)
            {
                ordered = livePlans.Values.Where(plan => valueDate is null || plan.ValueDate == valueDate)
                    .OrderByDescending(plan => plan.Position.RouteGeneration).ThenByDescending(plan => plan.PlanRevision)
                    .ThenByDescending(plan => plan.CalculatedAtUtc).ToArray();
                orderedDate = valueDate;
            }
            return ordered;
        }
    }
    public void Dispose()
    { lock (gate) { disposed = true; source.PropertyChanged -= OnChanged; livePlans.Clear(); ordered = null; } }
}
