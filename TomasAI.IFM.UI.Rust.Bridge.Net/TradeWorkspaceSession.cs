using Microsoft.Extensions.Logging;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.ViewModels.Extensions;
using System.Globalization;
using System.Text.Json;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.UI.Net.Models.Portfolio;
using TomasAI.IFM.UI.Net.Services.Reference;
using TomasAI.IFM.UI.Net.ViewModels.Trade;
using TomasAI.IFM.UI.Net.ViewModels.Trade.IronCondor;

namespace TomasAI.IFM.UI.Rust.Bridge.Net;
/// <summary>Adapts the unchanged trading editor and monitor models to immutable native UI publications.</summary>
sealed class TradeWorkspaceSession : IDisposable
{
    readonly RustClientServiceScope scope = new(Environment.GetEnvironmentVariable("IFM_UI_NATS_URL")??"nats://localhost:4222");
    readonly CancellationTokenSource stopping = new();
    TradeOrderEditorViewModel? model;
    sealed record OpenTrade(EstablishedTradeDefinition Trade, IronCondorViewModel? Monitor, string FundName, string TradeType, string TradeAction, TradeMonitorUpdates? Updates)
    {
        public TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.StrategyTradePlanHistoryPage? PlanPage { get; set; }
        public DateOnly PlanDate { get; set; }
        public string PlanError { get; set; } = "";
        public TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.StrategyTradePlanChartPoint[] ChartPoints { get; set; } = [];
        public DateTime? ChartEndUtc { get; set; }
        public string ChartError { get; set; } = "";
        public TradeMonitorRowSections Sections { get; } = new();
        public StrategyPositionId PositionId { get; } = StrategyPositionId.Create(Trade.Id, TradeStrategyKind.IronCondor);
    }
    readonly TradePlanRowCache planRows = new();
    readonly Dictionary<string, OpenTrade> openTrades = new(StringComparer.Ordinal);
    string? activeTradeKey;
    OpenTrade? ActiveTrade => activeTradeKey is not null ? openTrades.GetValueOrDefault(activeTradeKey) : null;
    IronCondorViewModel? monitor => ActiveTrade?.Monitor;
    EndOfDayProcessViewModel? endOfDay;
    EstablishedTradeDefinition? established => ActiveTrade?.Trade;
    FundOrderEditorViewModel? newOrder;
    int? preparedTradeId;
    DateOnly fromDate,toDate;
    TradeComposerSession? composer;
    ContractEditorResult? cachedWorkspace;
    ContractEditorResult? composerPublication;
    long monitorRevision, publishedMonitorRevision, lastMonitorRefresh;
    public bool RefreshPending => Volatile.Read(ref monitorRevision)!=Volatile.Read(ref publishedMonitorRevision)
        && Environment.TickCount64-Volatile.Read(ref lastMonitorRefresh)>=100;
    public Task<ContractEditorResult> RefreshPublicationAsync()
    {
        var observed=Volatile.Read(ref monitorRevision);
        var snapshot=Snapshot();
        // Preserve the independent editor when a live trade refreshes behind its dialog.
        if(composer is not null && composerPublication is {} composed) {
            cachedWorkspace=snapshot;
            var choices=new Dictionary<string,string[]>(snapshot.Choices);
            var context=new Dictionary<string,string>(snapshot.Context??[]);
            foreach(var pair in composed.Choices)choices[pair.Key]=pair.Value;
            foreach(var pair in composed.Context??[])context[pair.Key]=pair.Value;
            snapshot=snapshot with{Choices=choices,Context=context,Rows=[..snapshot.Rows,..composed.Rows]};
        }
        Volatile.Write(ref publishedMonitorRevision,observed);
        Volatile.Write(ref lastMonitorRefresh,Environment.TickCount64);
        return Task.FromResult(snapshot);
    }
    public byte[]? Confirmation => composer?.Confirmation;
    public Dictionary<string,string>? SubmissionContext => composer?.SubmissionContext;
    public int Decide(ContractEditorIntent intent)=>composer?.Decide(intent)==true?0:-5;
    public static readonly string[] Actions = ["load","portfolio","fund","order","trade","range","create-order","remove-order","prepare-order","cancel-draft","prepare-trade","create-trade","remove-trade","load-trade","plans-page","activate-trade","close-trade","position","live-on","live-off","compose","preview-draft","refresh-chain","clear-staged","chain","select-leg","remove-leg","account","qualification-submit","qualification-accept","qualification-revoke","qualification-hold","qualification-release","qualification-resync","submit-order","refresh-execution","update-limit","cancel-unfilled","close","eod-open","eod-load","eod-run"];
    public static bool Validate(ContractEditorIntent input) => input.Schema==1 && input.RequestId!=Guid.Empty && input.View=="trade-orders" && Actions.Contains(input.Intent)
        && input.Fields is not null && input.Fields.Count<=64 && input.Fields.All(x=>x.Key.Length<=64 && x.Value is not null && x.Value.Length<=2048) && (input.OriginalId?.Length??0)<=512;
    static string Field(ContractEditorIntent i,string key)=>i.Fields.GetValueOrDefault(key)??"";
    static int Index(ContractEditorIntent i)=>int.Parse(Field(i,"index"),CultureInfo.InvariantCulture);
    DateOnly apiValueDate;
    public async Task<ContractEditorResult> ExecuteAsync(ContractEditorIntent input)
    {
        if (model is null) await scope.StartAsync(stopping.Token);
        apiValueDate = await scope.GetValueDateAsync(stopping.Token);
        if(model is null){
            FuturesContractV3ReadModel[] contracts=[];
            await scope.Services.MarketDataQueries.ExecuteObservableAsync(api=>api.GetFuturesContractsAsync(rows=>contracts=rows));
            model=new(scope,apiValueDate,contracts,scope.Container.GetInstance<IReferenceDataService>(),brokerEnvironment:scope.AppEnvironment=="Development"?BrokerEnvironment.Emulator:BrokerEnvironment.Live);
            var today=apiValueDate;
            fromDate=new(today.Year,today.Month,1);toDate=fromDate.AddMonths(1).AddDays(-1);
            model.SetOrderDateRange(fromDate.AddMonths(-1).ToDateTime(TimeOnly.MinValue),toDate.ToDateTime(TimeOnly.MaxValue));
            await model.InitializeAsync(stopping.Token);
        }
        var m=model;
        if(input.Intent is "compose" or "submit-order" or "live-on" or "chain") {
            var session=await scope.Services.MarketDataQueries.QueryMarketSessionAsync(stopping.Token);
            m.ApplyMarketSessionSnapshot(session);
            foreach(var opened in openTrades.Values) opened.Monitor?.ApplyMarketSessionSnapshot(session);
        }
        if(input.Intent is "portfolio" or "fund" or "order" or "trade" or "load"){
            if(composer is not null)await composer.CloseAsync();composer=null;cachedWorkspace=null;composerPublication=null;
            preparedTradeId=null;
            if(newOrder is not null)await newOrder.DisposeAsync();newOrder=null;
        }
        if(input.Intent is "compose" or "preview-draft" or "refresh-chain" or "clear-staged" or "chain" or "select-leg" or "remove-leg" or "account" or "qualification-report" or "qualification-submit" or "qualification-accept" or "qualification-revoke" or "qualification-hold" or "qualification-release" or "qualification-resync" or "submit-order" or "refresh-execution" or "update-limit" or "cancel-unfilled"){
            if(input.Fields.TryGetValue("tradeId",out var inputTrade)&&inputTrade!=m.SelectedFundOrderTrade?.TradeId.ToString())throw new InvalidOperationException("Trade selection changed");
            if(input.OriginalId!=m.SelectedFundOrder?.OrderId.ToString())throw new InvalidOperationException("Order selection changed");
            if(input.Intent=="compose"){if(composer is not null)await composer.CloseAsync();composer=new(scope,m,stopping.Token);}
            if(composer is null)throw new InvalidOperationException("Open Trade Blotter first");
            if(input.Intent=="submit-order"&&!m.CanSubmitOrder)throw new InvalidOperationException("Submit Order is unavailable for this trade");
            await composer.ExecuteAsync(input);
            if (composer.CompletedTradeState is { } completedState && m.SelectedFundOrderTrade is { } selectedTrade
                && selectedTrade.TradeState != completedState)
            {
                var orderId = m.SelectedFundOrder!.OrderId;
                var tradeId = selectedTrade.TradeId;
                await m.LoadCanonicalOrdersAsync(stopping.Token);
                await m.SelectCanonicalOrderAsync(orderId, stopping.Token);
                m.SelectFundOrderTrade(m.FundOrderTrades.ToList().FindIndex(x => x.TradeId == tradeId));
                cachedWorkspace = null;
            }
            // Quote updates do not rebuild the fund/order/reference portion of this screen.
            var stable=input.Intent=="refresh-chain"&&cachedWorkspace is not null?cachedWorkspace:Snapshot();
            cachedWorkspace=stable;
            var basic=stable with{Choices=new(stable.Choices),Context=new(stable.Context??[])};
            var composed=await composer.SnapshotAsync();
            composerPublication=new("trade-orders",composed.Rows,composed.Choices,Context:composed.Context);
            foreach(var pair in composed.Choices)basic.Choices[pair.Key]=pair.Value;
            foreach(var pair in composed.Context)basic.Context![pair.Key]=pair.Value;
            return basic with{Rows=[..basic.Rows,..composed.Rows]};
        }
        switch(input.Intent){
            case "eod-open":
                var selected=m.SelectedFundOrderTrade??throw new InvalidOperationException("Select a trade");
                if(selected.ExecutionOrderId<=0||selected.ExecutionTradeId<=0)throw new InvalidOperationException("EOD requires an established trade");
                if(endOfDay is not null)await endOfDay.DisposeAsync();
                endOfDay=new(scope,new(){PortfolioId=m.SelectedPortfolio!.PortfolioId,FundId=m.SelectedFund!.FundId,OrderId=selected.ExecutionOrderId,TradeId=selected.ExecutionTradeId,TradeType=selected.TradeType,BaseContractId=selected.BaseContractId,ValueDate=apiValueDate});
                await endOfDay.InitializeAsync(stopping.Token);await endOfDay.LoadData();break;
            case "eod-load":if(endOfDay is null)throw new InvalidOperationException("Open End of Day first");endOfDay.SetValueDate(DateOnly.ParseExact(Field(input,"eodDate"),"yyyy-MM-dd",CultureInfo.InvariantCulture));endOfDay.SetReference(Field(input,"reference"));await endOfDay.LoadData();break;
            case "eod-run":if(endOfDay is null||!endOfDay.CanRun)throw new InvalidOperationException("End of Day is unavailable");await endOfDay.RunEndOfDayProcess();break;
            case "close":if(composer is not null)await composer.CloseAsync();composer=null;cachedWorkspace=null;composerPublication=null;break;
            case "load": await m.LoadFunds();if(m.SelectedFundOrder is {} initial)await m.SelectCanonicalOrderAsync(initial.OrderId,stopping.Token);break;
            case "portfolio":await m.SelectPortfolioAsync(Index(input),stopping.Token);break;
            case "fund":if(!m.SelectFund(Index(input)))throw new ArgumentException("Fund is no longer available");await m.LoadCanonicalOrdersAsync(stopping.Token);break;
            case "order":await m.SelectCanonicalOrderAsync(int.Parse(input.OriginalId!,CultureInfo.InvariantCulture),stopping.Token);break;
            case "trade":if(!m.SelectFundOrderTrade(Index(input)))throw new ArgumentException("Trade is no longer available");await m.RefreshSelectedTradeFillEvidenceAsync();break;
            case "range":
                var requestedFrom=DateOnly.ParseExact(Field(input,"from"),"yyyy-MM-dd",CultureInfo.InvariantCulture);var requestedTo=DateOnly.ParseExact(Field(input,"to"),"yyyy-MM-dd",CultureInfo.InvariantCulture);
                if(requestedTo<requestedFrom)throw new ArgumentException("To date must be on or after From date");
                fromDate=requestedFrom;toDate=requestedTo;
                m.SetOrderDateRange(fromDate.AddMonths(-1).ToDateTime(TimeOnly.MinValue),toDate.ToDateTime(TimeOnly.MaxValue));await m.LoadCanonicalOrdersAsync(stopping.Token);
                if(m.SelectedFundOrder is {} ranged)await m.SelectCanonicalOrderAsync(ranged.OrderId,stopping.Token);break;
            case "cancel-draft":if(newOrder is not null)await newOrder.DisposeAsync();newOrder=null;preparedTradeId=null;break;
            case "prepare-order":
                if(!m.CanCreateOrder)throw new InvalidOperationException("Create Order is unavailable");
                if(newOrder is not null)await newOrder.DisposeAsync();newOrder=new((m.SelectedFund??throw new InvalidOperationException("Select a Fund before creating an order")).FundId,scope.Container.GetInstance<IReferenceDataService>(),allocateOrderId:false);await newOrder.LoadOperation.ExecuteAsync(stopping.Token);break;
            case "create-order":
                if(!m.CanCreateOrder)throw new InvalidOperationException("Create Order is unavailable");
                if(newOrder is null)throw new InvalidOperationException("Open Create Order first");newOrder.SetReference(Field(input,"reference"));await m.CreateManualOrderAsync(newOrder.FundOrder,stopping.Token);await newOrder.DisposeAsync();newOrder=null;if(m.SelectedFundOrder is {} created)await m.SelectCanonicalOrderAsync(created.OrderId,stopping.Token);break;
            case "remove-order":
                if(!m.CanDeleteOrder)throw new InvalidOperationException("Remove Order is unavailable");
                await m.DeleteManualOrderAsync(CurrentOrder(input),Field(input,"reason"),stopping.Token);break;
            case "prepare-trade":if(!m.CanAddTrade)throw new InvalidOperationException("Add Trade is unavailable");preparedTradeId=await m.GetNewTradeIdAsync(stopping.Token);break;
            case "create-trade":
                if(!m.CanAddTrade)throw new InvalidOperationException("Add Trade is unavailable");
                var order=CurrentOrder(input);var opening=m.FundOrderTrades.FirstOrDefault(x=>x.PrimaryTrade);
                var type=opening is null?Enum.Parse<TradeType>(Field(input,"tradeType")):PortfolioFundOrderEditorPolicy.ClosingType(opening.TradeType);
                var contract=m.BaseContracts.Single(x=>x.ContractId==(opening?.BaseContractId??Field(input,"baseContract")));
                var effective=DateOnly.ParseExact(Field(input,"effectiveDate"),"yyyy-MM-dd",CultureInfo.InvariantCulture);
                await m.AddManualTradeAsync(order,new(){TradeId=preparedTradeId??throw new InvalidOperationException("Open Add Trade first"),TradeFamily=type.ToString(),TradeType=type,EffectiveDate=effective,TradeDate=null,MaturityDate=null,RequestedMaturityDate=null,
                    TradeState=TradeState.NewTrade,TradeAction=type is TradeType.ShortIronCondor or TradeType.PutCreditSpread or TradeType.CallCreditSpread?TradeAction.Sell:TradeAction.Buy,
                    PrimaryTrade=true,BaseContractId=contract.ContractId,BaseContractSymbol=contract.Symbol,UnderlyingRoot=contract.Symbol,CreatedBy=Environment.UserName,CreatedOnUtc=DateTime.UtcNow},stopping.Token);break;
            case "remove-trade":
                if(!m.CanRemoveTrade)throw new InvalidOperationException("Remove Trade is unavailable");
                await m.RemoveManualTradeAsync(CurrentOrder(input),m.SelectedFundOrderTrade!.TradeId,stopping.Token);break;
            case "load-trade":
                var loaded=await m.LoadTradeAsync(stopping.Token);
                if(composer is not null)await composer.CloseAsync();composer=null;cachedWorkspace=null;composerPublication=null;
                var key=loaded.Id.Format();
                if(!openTrades.ContainsKey(key)) {
                    IronCondorViewModel? candidate=null;
                    try {
                        if(m.SelectedFundOrderTrade!.TradeType is TradeType.ShortIronCondor or TradeType.LongIronCondor) {
                            candidate=new(scope,m.SelectedFund!,m.SelectedFundOrder!,m.SelectedFundOrderTrade,m.ValueDate,m.BaseContracts.ToArray(),portfolioId:m.SelectedPortfolio!.PortfolioId,establishedTrade:loaded,brokerEnvironment:scope.AppEnvironment=="Development"?BrokerEnvironment.Emulator:BrokerEnvironment.Live);
                            candidate.VirtualTradePlanHistory = true;
                            await candidate.LoadEstablishedTradeAsync(stopping.Token);
                            if(!candidate.IsLiveFeedEnabled && candidate.TradeHistory.Length>0)await candidate.LoadSavedPositionDataAsync(candidate.TradeHistory.Length-1);
                        }
                        var observedMonitor=candidate;
                        var updates=observedMonitor is null?null:new TradeMonitorUpdates(observedMonitor,()=>observedMonitor.StrategyTradePlan,()=>observedMonitor.IsLiveFeedEnabled,()=>Interlocked.Increment(ref monitorRevision));
                        openTrades.Add(key,new(loaded,candidate,m.SelectedFund!.Name,m.SelectedFundOrderTrade!.TradeType.ToString(),m.SelectedFundOrderTrade.TradeAction.ToString(),updates) { PlanDate = candidate?.SelectedSavedPosition?.ValueDate ?? apiValueDate });
                    } catch {if(candidate is not null)await candidate.DisposeAsync();throw;}
                }
                activeTradeKey=key;
                if (ActiveTrade is { Monitor: not null } loadedChart) {
                    if (loadedChart.Monitor.IsLiveFeedEnabled) loadedChart.PlanDate = loadedChart.Monitor.ValueDate ?? apiValueDate;
                    await LoadChartAsync(loadedChart);
                }
                break;
            case "plans-page":
                var active = ActiveTrade ?? throw new InvalidOperationException("Load a trade first.");
                if (Field(input,"tradeTabId") != activeTradeKey) throw new InvalidOperationException("The trade tab changed.");
                var offset = int.Parse(Field(input,"offset"), CultureInfo.InvariantCulture);
                var size = int.Parse(Field(input,"pageSize"), CultureInfo.InvariantCulture);
                ArgumentOutOfRangeException.ThrowIfNegative(offset);
                if (size is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(size));
                await LoadPlanHistoryPageAsync(active, offset, size, Field(input,"refresh")=="true");
                break;
            case "activate-trade":
                var activateKey=Field(input,"tradeTabId");
                if(!openTrades.ContainsKey(activateKey))throw new InvalidOperationException("The trade tab is no longer open.");
                activeTradeKey=activateKey;break;
            case "close-trade":
                var closeKey=Field(input,"tradeTabId");
                if(openTrades.TryGetValue(closeKey,out var closingTrade)) {
                    closingTrade.Updates?.Dispose();
                    closingTrade.Sections.Dispose();
                    planRows.Clear(closingTrade.PositionId);
                    if(closingTrade.Monitor is not null)await closingTrade.Monitor.DisposeAsync();
                    openTrades.Remove(closeKey);
                    if(activeTradeKey==closeKey)activeTradeKey=openTrades.Keys.LastOrDefault();
                }
                break;
            case "position":if(monitor is null||!monitor.CanSelectPositionHistory)throw new InvalidOperationException("Position selection is unavailable while live");await monitor.LoadSavedPositionDataAsync(Index(input));if(ActiveTrade is { } selectedHistory){selectedHistory.PlanDate=monitor.SelectedSavedPosition?.ValueDate??apiValueDate;selectedHistory.PlanPage=null;selectedHistory.PlanError="";await LoadChartAsync(selectedHistory);}break;
            case "live-on":if(monitor is null||!monitor.CanEnableLiveFeed)throw new InvalidOperationException("Live feed is unavailable for this trade or session");
                // Refresh the six-hour database baseline on every live start; subsequent points use notifications only.
                if (ActiveTrade is {} startingTrade) {
                    if (startingTrade.PlanDate != apiValueDate) {
                        startingTrade.PlanDate = apiValueDate;
                        startingTrade.PlanPage = null;
                    }
                    await LoadChartAsync(startingTrade);
                }
                ActiveTrade?.Updates?.BeginLive();
                try { await monitor.EnableLiveFeedAsync(stopping.Token); }
                catch { ActiveTrade?.Updates?.EndLive(); throw; }
                if (ActiveTrade is {} liveTrade) { liveTrade.PlanDate = monitor.RealtimeSnapshot?.ValueDate ?? apiValueDate;
                    liveTrade.PlanPage = null; liveTrade.PlanError = ""; }
                break;
            case "live-off":
                if(monitor is not null)await monitor.DisableLiveFeedAsync(stopping.Token);
                if(ActiveTrade is {} stoppedTrade) {
                    stoppedTrade.Updates?.EndLive(); stoppedTrade.PlanPage=null; stoppedTrade.PlanError="";
                    await LoadChartAsync(stoppedTrade);
                    await LoadPlanHistoryPageAsync(stoppedTrade,0,100,true);
                }
                break;
        }
        return Snapshot();
    }
    /// <summary>Loads only database history; queued paging intents become no-ops once live monitoring is enabled.</summary>
    async Task LoadPlanHistoryPageAsync(OpenTrade active, int offset, int size, bool refresh)
    {
        if(active.Monitor?.IsLiveFeedEnabled==true)return;
        active.PlanError="";
        try {
            TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.StrategyTradePlanHistoryPage? page=null;
            await scope.Services.StrategyTradePlanQueries.GetIronCondorHistoryPageAsync(
                StrategyPositionId.Create(active.Trade.Id,TradeStrategyKind.IronCondor),active.PlanDate,
                offset,size,refresh?null:active.PlanPage?.MaximumRevision,result=>page=result,stopping.Token);
            if(active.Monitor?.IsLiveFeedEnabled==true)return;
            active.PlanPage=page??throw new InvalidOperationException("Trade plan page was not returned.");

        } catch(Exception error) when(error is not OperationCanceledException){active.PlanError=error.Message;}
    }

    async Task LoadChartAsync(OpenTrade active)
    {
        try {
            var series = await scope.Services.StrategyTradePlanQueries.GetIronCondorChartAsync(
                StrategyPositionId.Create(active.Trade.Id,TradeStrategyKind.IronCondor),active.PlanDate,stopping.Token);
            active.ChartPoints = series.Points;
            active.ChartEndUtc = series.EndUtc;
            active.ChartError = "";
        } catch (Exception error) when (error is not OperationCanceledException) {
            active.ChartError = error.Message;
            scope.DiagnosticLogger.LogWarning(error,"Iron Condor chart baseline load failed; TradeId={TradeId}; ValueDate={ValueDate}",active.Trade.Id.Format(),active.PlanDate);
        }
    }

    TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.FundOrderProjectionReadModel CurrentOrder(ContractEditorIntent i){
        if(model!.SelectedFundOrder is not {} selected || i.OriginalId!=selected.OrderId.ToString(CultureInfo.InvariantCulture))throw new InvalidOperationException("The selected order changed; reload before continuing");
        return model.CanonicalOrders.Single(x=>x.OrderId==selected.OrderId);
    }
    static ContractEditorRow Row(string kind,string id,object value) {
        var fields=new Dictionary<string,string>{{"kind",kind}};
        if(value is TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.StrategyTradePlanSnapshot plan)
            fields["actionDateTimeEastern"]=TradePlanDisplay.ActionTime(plan)??"";
        return new(kind+":"+id,fields){Data=kind=="plan" && value is TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.StrategyTradePlanSnapshot historyPlan ? TradePlanDisplay.EncodeHistory(historyPlan) : BridgeDocument.Encode(value,camelCase:true,enumNames:true)};
    }
    ContractEditorResult Snapshot(){
        var m=model!;var rows=new List<ContractEditorRow>();
        rows.AddRange(m.FundOrders.Select(x=>Row("order",x.OrderId.ToString(),new{x.OrderId,x.Status,CreatedOnUtc=EasternTime.FromUtc(x.CreatedOnUtc).ToString("yyyy-MMM-dd",CultureInfo.InvariantCulture),x.OperatorReference,source=x.Origin.ToString()=="ManualUi"?"Manual":"Strategy Workflow"})));
        rows.AddRange(m.FundOrderTrades.Select((x,n)=>Row("trade",x.TradeId.ToString(),new{index=n,x.TradeId,x.TradeType,x.EffectiveDate,x.TradeDate,x.MaturityDate,x.TradeState,x.TradeAction,x.BaseContractId})));
        rows.AddRange(openTrades.Select(pair=>Row("workspace-tab",pair.Key,new {key=pair.Key,title=$"{pair.Value.Trade.Id.OrderId}:{pair.Value.Trade.Id.TradeId}",kind="trade",active=pair.Key==activeTradeKey})));
        if(ActiveTrade is { } establishedTab) rows.AddRange(establishedTab.Sections.Rows(
            TradeMonitorSection.Established, establishedTab.Trade, 0, establishedTab.Trade,
            static trade => [Row("established", trade.Id.Format(), trade)]));
        if(monitor is {} activeMonitor){
            // Capture once: every live row in this publication comes from this revision.
            var live=activeMonitor.IsLiveFeedEnabled ? activeMonitor.RealtimeSnapshot : null;
            if(live is not null) rows.Add(Row("realtime","0",live));
            var active = ActiveTrade!;
            var revisions = active.Updates?.Revisions ?? default;
            rows.AddRange(active.Sections.PositionRows(activeMonitor.TradeHistory));
            var savedPlans = active.PlanPage?.Items ?? [];
            var plans = active.Updates?.Plans(savedPlans, active.PlanDate)
                ?? (activeMonitor.IsLiveFeedEnabled ? Array.Empty<TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.StrategyTradePlanSnapshot>() : savedPlans);
            rows.AddRange(planRows.Rows(plans, new(active.PositionId, active.PlanDate, activeMonitor.IsLiveFeedEnabled)));
            rows.AddRange(active.Sections.Rows(TradeMonitorSection.Limits, activeMonitor.TradeLimitSnapshot, revisions.Limits,
                activeMonitor.TradeLimitSnapshot, static limits => [Row("limits", "0", limits)]));
            rows.AddRange(active.Sections.Rows(TradeMonitorSection.Chart, active.ChartPoints, active.ChartEndUtc?.Ticks ?? 0,
                (active.ChartEndUtc, active.ChartPoints), static chart => [Row("chart", "0", new { ChartEndUtc = chart.ChartEndUtc, Points = chart.ChartPoints })]));
            var position = live?.Position ?? activeMonitor.SelectedSavedPosition;
            rows.AddRange(active.Sections.Rows(TradeMonitorSection.PositionDetails, position, revisions.Position,
                position, static selected => [Row("position-details", "0", selected)]));
            if(live is null) rows.AddRange(active.Sections.Rows(TradeMonitorSection.Observations, activeMonitor.SavedLegObservations, revisions.Position,
                activeMonitor.SavedLegObservations, static observations => observations.Select((item, index) => Row("observation", index.ToString(), item)).ToArray()));
        }
        if(newOrder is not null)rows.Add(Row("order-draft","0",new{newOrder.OrderId,newOrder.OrderDate,newOrder.OrderStatus}));
        if(endOfDay is not null)rows.Add(Row("eod","0",new{endOfDay.PortfolioId,endOfDay.FundId,endOfDay.OrderId,endOfDay.TradeId,endOfDay.ValueDate,endOfDay.OpenPrice,endOfDay.HighPrice,endOfDay.LowPrice,endOfDay.ClosePrice,endOfDay.Volume,endOfDay.TradePnl,endOfDay.FundBalance,endOfDay.CanRun,endOfDay.CommandId,endOfDay.IsCompleted,endOfDay.LastStatusMessage}));
        return new("trade-orders",rows.ToArray(),new(){["portfolio"]=m.Portfolios.Select(x=>x.PortfolioId+" - "+x.Name).ToArray(),["fund"]=m.Funds.Select(x=>x.FundId+" - "+x.Name).ToArray(),["baseContract"]=m.BaseContracts.Select(x=>x.ContractId).ToArray(),["tradeType"]=["ShortIronCondor","LongIronCondor","PutCreditSpread","PutDebitSpread","CallCreditSpread","CallDebitSpread","FuturesOutright"]},Context:new(){
            ["portfolioIndex"]=m.PortfolioSelectedIndex.ToString(),["fundIndex"]=m.FundSelectedIndex.ToString(),["orderId"]=m.SelectedFundOrder?.OrderId.ToString()??"",["tradeId"]=m.SelectedFundOrderTrade?.TradeId.ToString()??"",["tradeState"]=m.SelectedFundOrderTrade?.TradeState.ToString()??"",["valueDate"]=apiValueDate.ToString("yyyy-MM-dd"),["from"]=fromDate.ToString("yyyy-MM-dd"),["to"]=toDate.ToString("yyyy-MM-dd"),["preparedTradeId"]=preparedTradeId?.ToString()??"",["fundId"]=m.SelectedFund?.FundId.ToString()??"",["portfolioId"]=m.SelectedPortfolio?.PortfolioId.ToString()??"",
            ["closingType"]=m.FundOrderTrades.FirstOrDefault(x=>x.PrimaryTrade) is {} opening?PortfolioFundOrderEditorPolicy.ClosingType(opening.TradeType).ToString():"",["closingContract"]=m.FundOrderTrades.FirstOrDefault(x=>x.PrimaryTrade)?.BaseContractId??"",
            ["latestPlanRevision"]=(monitor?.IsLiveFeedEnabled==true?monitor.StrategyTradePlan?.PlanRevision??0:0).ToString(),["planMaximumRevision"]=(ActiveTrade?.PlanPage?.MaximumRevision??0).ToString(),["planOffset"]=(ActiveTrade?.PlanPage?.Offset??0).ToString(),["planTotal"]=(ActiveTrade?.PlanPage?.TotalCount??1).ToString(),["planLoaded"]=(monitor?.IsLiveFeedEnabled==true || ActiveTrade?.PlanPage is not null).ToString(),["planDate"]=ActiveTrade?.PlanDate.ToString("yyyy-MM-dd")??"",["chartError"]=ActiveTrade?.ChartError??"",["planError"]=ActiveTrade?.PlanError??"",["monitorStatus"]=monitor?.LiveFeedStatusMessage??"",["monitorLive"]=monitor?.IsLiveFeedEnabled.ToString()??"False",["monitorCanLive"]=monitor?.CanEnableLiveFeed.ToString()??"False",["monitorCanSelectPosition"]=monitor?.CanSelectPositionHistory.ToString()??"False",["activeTradeTab"]=activeTradeKey??"",["fundName"]=ActiveTrade?.FundName??m.SelectedFund?.Name??"",["compositionType"]=ActiveTrade?.TradeType??m.SelectedFundOrderTrade?.TradeType.ToString()??"",["tradeAction"]=ActiveTrade?.TradeAction??m.SelectedFundOrderTrade?.TradeAction.ToString()??"",["canCreateOrder"]=m.CanCreateOrder.ToString(),["canRemoveOrder"]=m.CanDeleteOrder.ToString(),["canAddTrade"]=m.CanAddTrade.ToString(),["canRemoveTrade"]=m.CanRemoveTrade.ToString(),["canLoadTrade"]=m.CanLoadOrder.ToString(),["canSubmit"]=m.CanSubmitOrder.ToString(),["canEndOfDay"]=m.CanEndOfDay.ToString(),["canLive"]=monitor?.CanEnableLiveFeed.ToString()??"False",["live"]=monitor?.IsLiveFeedEnabled.ToString()??"False",["canSelectPosition"]=monitor?.CanSelectPositionHistory.ToString()??"False",["status"]=m.LastStatusMessage});
    }
    public void CancelPending()=>stopping.Cancel();
    public void Dispose(){stopping.Cancel();foreach(var trade in openTrades.Values){trade.Updates?.Dispose();trade.Sections.Dispose();trade.Monitor?.DisposeAsync().AsTask().GetAwaiter().GetResult();}openTrades.Clear();planRows.Dispose();endOfDay?.DisposeAsync().AsTask().GetAwaiter().GetResult();newOrder?.DisposeAsync().AsTask().GetAwaiter().GetResult();model?.DisposeAsync().AsTask().GetAwaiter().GetResult();scope.DisposeAsync().AsTask().GetAwaiter().GetResult();stopping.Dispose();}
}
