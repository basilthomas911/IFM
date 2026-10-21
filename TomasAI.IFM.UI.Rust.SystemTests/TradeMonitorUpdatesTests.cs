using System.ComponentModel;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.UI.Rust.Bridge.Net;

public sealed class TradeMonitorUpdatesTests
{
    sealed class Monitor : INotifyPropertyChanged
    {
        public bool Live=true;
        public StrategyTradePlanSnapshot? Current;
        public event PropertyChangedEventHandler? PropertyChanged;
        public void Send(StrategyTradePlanSnapshot plan) {Current=plan;Raise("StrategyTradePlan");}
        public void Raise(string name)=>PropertyChanged?.Invoke(this,new(name));
    }
    static StrategyTradePlanSnapshot Plan(long revision,int trade=1501,long route=1)=>new()
    {
        Position=new(){Id=StrategyPositionId.Create(new(101,701,1901,trade),TradeStrategyKind.IronCondor),RouteGeneration=route},
        ValueDate=new(2026,10,16),PlanRevision=revision,CalculatedAtUtc=new DateTime(2026,10,16,15,0,0,DateTimeKind.Utc).AddSeconds(revision),
        Explanation=$"Live revision {revision}"
    };
    [Fact]
    public void Live_notifications_exclude_database_rows_and_stopped_mode_excludes_live_rows()
    {
        var monitor=new Monitor();var changes=0;
        using var updates=new TradeMonitorUpdates(monitor,()=>monitor.Current,()=>monitor.Live,()=>changes++);
        monitor.Send(Plan(2));monitor.Send(Plan(3));monitor.Send(Plan(3));
        Assert.Equal(new long[]{3,2},updates.Plans([Plan(1),Plan(2)]).Select(p=>p.PlanRevision));
        Assert.Equal(3,changes);
        monitor.Live=false;monitor.Send(Plan(4));
        Assert.Equal(new long[]{1},updates.Plans([Plan(1)]).Select(p=>p.PlanRevision));
        monitor.Raise("IronCondorPlanHistory");
        Assert.Single(updates.Plans([Plan(1)]));
    }
    [Fact]
    public void Separate_trade_tabs_remain_isolated_and_close_detaches_notifications()
    {
        var first=new Monitor();var second=new Monitor();var changes=0;
        using var a=new TradeMonitorUpdates(first,()=>first.Current,()=>first.Live,()=>changes++);
        using var b=new TradeMonitorUpdates(second,()=>second.Current,()=>second.Live,()=>changes++);
        first.Send(Plan(2));second.Send(Plan(7,1502));
        Assert.Equal(1501,Assert.Single(a.Plans([])).Position.Id.Trade.TradeId);
        Assert.Equal(1502,Assert.Single(b.Plans([])).Position.Id.Trade.TradeId);
        a.Dispose();first.Send(Plan(3));
        Assert.Equal(2,changes);
        second.Send(Plan(8,1502));Assert.Equal(3,changes);
    }
    [Fact]
    public void Enabling_live_feed_captures_a_plan_received_during_startup()
    {
        var monitor=new Monitor{Live=false};
        using var updates=new TradeMonitorUpdates(monitor,()=>monitor.Current,()=>monitor.Live,()=>{});
        updates.BeginLive();
        monitor.Send(Plan(1));Assert.Empty(updates.Plans([]));
        monitor.Live=true;monitor.Raise("IsLiveFeedEnabled");
        Assert.Equal(1,Assert.Single(updates.Plans([])).PlanRevision);
    }
    [Fact]
    public void Starting_live_never_seeds_a_previously_loaded_plan_and_each_live_cycle_starts_empty()
    {
        var monitor=new Monitor { Live=false,Current=Plan(100) };
        using var updates=new TradeMonitorUpdates(monitor,()=>monitor.Current,()=>monitor.Live,()=>{});
        Assert.Equal(100,Assert.Single(updates.Plans([Plan(100)])).PlanRevision);
        updates.BeginLive(); monitor.Live=true; monitor.Raise("IsLiveFeedEnabled");
        Assert.Empty(updates.Plans([Plan(100)]));
        monitor.Send(Plan(101)); Assert.Equal(101,Assert.Single(updates.Plans([Plan(100)])).PlanRevision);
        monitor.Live=false;monitor.Raise("IsLiveFeedEnabled");updates.EndLive();
        Assert.Equal(100,Assert.Single(updates.Plans([Plan(100)])).PlanRevision);
        updates.BeginLive();monitor.Live=true;monitor.Raise("IsLiveFeedEnabled");
        Assert.Empty(updates.Plans([Plan(100)]));
    }

    [Fact]
    public async Task Notifications_refresh_the_gateway_publication_without_user_actions()
    {
        var monitor=new Monitor();var changed=false;
        using var updates=new TradeMonitorUpdates(monitor,()=>monitor.Current,()=>monitor.Live,()=>changed=true);
        ContractEditorResult Snapshot()=>new("trade-orders",updates.Plans([Plan(1)]).Select(plan=>new ContractEditorRow($"plan:{plan.PlanRevision}",new(){["kind"]="plan"}){Data=BridgeDocument.Encode(plan,camelCase:true,enumNames:true)}).ToArray(),[],Context:new(){["activeTradeTab"]="101.701.1901.1501",["live"]="True"});
        await using var gateway=new ContractEditorGateway(_=>Task.FromResult(Snapshot()),_=>true);
        var request=new ContractEditorIntent(1,Guid.NewGuid(),"trade-orders","load",1,null,[]);
        Assert.Equal(0,gateway.Submit(request));await gateway.DrainAsync();
        var initial=gateway.Snapshot();Assert.Empty(initial.Result.Rows);
        monitor.Send(Plan(2));Assert.True(changed);
        Assert.True(gateway.TryRefresh("trade-orders",()=>Task.FromResult(Snapshot())));await gateway.DrainAsync();
        var refreshed=gateway.Snapshot();
        Assert.True(refreshed.Generation>initial.Generation);Assert.Single(refreshed.Result.Rows);
        Assert.Equal("plan:2",refreshed.Result.Rows[0].Id);
        Assert.Equal("101.701.1901.1501",refreshed.Result.Context!["activeTradeTab"]);
        Assert.Single(refreshed.Outcomes); // No synthetic refresh commands/outcomes accumulate.
    }
    [Fact]
    public void Live_price_notifications_preserve_history_window_and_do_not_mark_saved_positions_dirty()
    {
        var monitor = new Monitor();
        using var updates = new TradeMonitorUpdates(monitor, () => monitor.Current, () => monitor.Live, () => { });
        for (var i = 0; i < 150; i++) monitor.Send(Plan(i));
        var first = updates.Plans([]); var before = updates.Revisions;
        Assert.Equal(100, first.Count);
        monitor.Raise("RealtimeSnapshot");
        Assert.Same(first, updates.Plans([]));
        Assert.Equal(before.History, updates.Revisions.History);
        Assert.Equal(before.SavedPositions, updates.Revisions.SavedPositions);
        Assert.True(updates.Revisions.Position > before.Position);
        monitor.Raise(""); Assert.True(updates.Revisions.History > before.History);
        Assert.NotSame(first, updates.Plans([]));
    }

    [Fact]
    public async Task A_notification_arriving_during_snapshot_keeps_a_newer_revision_pending()
    {
        var monitor = new Monitor(); long revision = 0;
        using var updates = new TradeMonitorUpdates(monitor, () => monitor.Current, () => monitor.Live, () => Interlocked.Increment(ref revision));
        monitor.Send(Plan(1));
        var observed = Volatile.Read(ref revision);
        var captured = updates.Plans([]);
        await Task.Run(() => monitor.Send(Plan(2)));
        Assert.True(Volatile.Read(ref revision) > observed);
        Assert.Single(captured); Assert.Equal(2, updates.Plans([]).Count);
        updates.Dispose(); var closed = revision; monitor.Send(Plan(3)); Assert.Equal(closed, revision);
    }
}
