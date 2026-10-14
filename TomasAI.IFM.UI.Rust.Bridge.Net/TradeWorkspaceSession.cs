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
    IronCondorViewModel? monitor;
    EndOfDayProcessViewModel? endOfDay;
    EstablishedTradeDefinition? established;
    FundOrderEditorViewModel? newOrder;
    int? preparedTradeId;
    DateOnly fromDate,toDate;
    TradeComposerSession? composer;
    int? reconciledFilledTrade;
    public string? Confirmation => composer?.Confirmation;
    public int Decide(ContractEditorIntent intent)=>composer?.Decide(intent)==true?0:-5;
    public static readonly string[] Actions = ["load","portfolio","fund","order","trade","range","create-order","remove-order","prepare-order","cancel-draft","prepare-trade","create-trade","remove-trade","load-trade","position","live-on","live-off","compose","preview-draft","refresh-chain","clear-staged","chain","select-leg","remove-leg","account","qualification-submit","qualification-accept","qualification-revoke","qualification-hold","qualification-release","qualification-resync","submit-order","refresh-execution","update-limit","cancel-unfilled","close","eod-open","eod-load","eod-run"];
    public static bool Validate(ContractEditorIntent input) => input.Schema==1 && input.RequestId!=Guid.Empty && input.View=="trade-orders" && Actions.Contains(input.Intent)
        && input.Fields is not null && input.Fields.Count<=64 && input.Fields.All(x=>x.Key.Length<=64 && x.Value is not null && x.Value.Length<=2048) && (input.OriginalId?.Length??0)<=512;
    static string Field(ContractEditorIntent i,string key)=>i.Fields.GetValueOrDefault(key)??"";
    static int Index(ContractEditorIntent i)=>int.Parse(Field(i,"index"),CultureInfo.InvariantCulture);
    public async Task<ContractEditorResult> ExecuteAsync(ContractEditorIntent input)
    {
        if(model is null){
            await scope.StartAsync(stopping.Token);
            FuturesContractV3ReadModel[] contracts=[];
            await scope.Services.MarketDataQueries.ExecuteObservableAsync(api=>api.GetFuturesContractsAsync(rows=>contracts=rows));
            model=new(scope,scope.ValueDates.ValueDate,contracts,scope.Container.GetInstance<IReferenceDataService>(),brokerEnvironment:scope.AppEnvironment=="Development"?BrokerEnvironment.Emulator:BrokerEnvironment.Live);
            var today=DateOnly.FromDateTime(EasternTime.GetNow(TimeProvider.System).Date);
            fromDate=new(today.Year,today.Month,1);toDate=fromDate.AddMonths(1).AddDays(-1);
            model.SetOrderDateRange(fromDate.AddMonths(-1).ToDateTime(TimeOnly.MinValue),toDate.ToDateTime(TimeOnly.MaxValue));
            await model.InitializeAsync(stopping.Token);
        }
        var m=model;
        if(input.Intent is "portfolio" or "fund" or "order" or "trade" or "load"){
            if(composer is not null)await composer.CloseAsync();composer=null;
            if(monitor is not null)await monitor.DisposeAsync();monitor=null;established=null;preparedTradeId=null;reconciledFilledTrade=null;
            if(newOrder is not null)await newOrder.DisposeAsync();newOrder=null;
        }
        if(input.Intent is "compose" or "preview-draft" or "refresh-chain" or "clear-staged" or "chain" or "select-leg" or "remove-leg" or "account" or "qualification-report" or "qualification-submit" or "qualification-accept" or "qualification-revoke" or "qualification-hold" or "qualification-release" or "qualification-resync" or "submit-order" or "refresh-execution" or "update-limit" or "cancel-unfilled"){
            if(input.Fields.TryGetValue("tradeId",out var inputTrade)&&inputTrade!=m.SelectedFundOrderTrade?.TradeId.ToString())throw new InvalidOperationException("Trade selection changed");
            if(input.OriginalId!=m.SelectedFundOrder?.OrderId.ToString())throw new InvalidOperationException("Order selection changed");
            if(input.Intent=="compose"){if(composer is not null)await composer.CloseAsync();composer=new(scope,m,stopping.Token);}
            if(composer is null)throw new InvalidOperationException("Open Trade Blotter first");
            if(input.Intent=="submit-order"&&!m.CanSubmitOrder)throw new InvalidOperationException("Submit Order is unavailable for this trade");
            await composer.ExecuteAsync(input);
            if(composer.OpeningFilled&&reconciledFilledTrade!=m.SelectedFundOrderTrade!.TradeId){var orderId=m.SelectedFundOrder!.OrderId;var tradeId=m.SelectedFundOrderTrade!.TradeId;await m.LoadCanonicalOrdersAsync(stopping.Token);await m.SelectCanonicalOrderAsync(orderId,stopping.Token);m.SelectFundOrderTrade(m.FundOrderTrades.ToList().FindIndex(x=>x.TradeId==tradeId));reconciledFilledTrade=tradeId;}
            var basic=Snapshot();var composed=await composer.SnapshotAsync();
            foreach(var pair in composed.Choices)basic.Choices[pair.Key]=pair.Value;
            foreach(var pair in composed.Context)basic.Context![pair.Key]=pair.Value;
            return basic with{Rows=[..basic.Rows,..composed.Rows]};
        }
        switch(input.Intent){
            case "eod-open":
                var selected=m.SelectedFundOrderTrade??throw new InvalidOperationException("Select a trade");
                if(selected.ExecutionOrderId<=0||selected.ExecutionTradeId<=0)throw new InvalidOperationException("EOD requires an established trade");
                if(endOfDay is not null)await endOfDay.DisposeAsync();
                endOfDay=new(scope,new(){PortfolioId=m.SelectedPortfolio!.PortfolioId,FundId=m.SelectedFund!.FundId,OrderId=selected.ExecutionOrderId,TradeId=selected.ExecutionTradeId,TradeType=selected.TradeType,BaseContractId=selected.BaseContractId,ValueDate=scope.ValueDates.ValueDate});
                await endOfDay.InitializeAsync(stopping.Token);await endOfDay.LoadData();break;
            case "eod-load":if(endOfDay is null)throw new InvalidOperationException("Open End of Day first");endOfDay.SetValueDate(DateOnly.ParseExact(Field(input,"eodDate"),"yyyy-MM-dd",CultureInfo.InvariantCulture));endOfDay.SetReference(Field(input,"reference"));await endOfDay.LoadData();break;
            case "eod-run":if(endOfDay is null||!endOfDay.CanRun)throw new InvalidOperationException("End of Day is unavailable");await endOfDay.RunEndOfDayProcess();break;
            case "close":if(composer is not null)await composer.CloseAsync();composer=null;if(monitor is not null)await monitor.DisposeAsync();monitor=null;established=null;break;
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
                established=await m.LoadTradeAsync(stopping.Token);
                if(monitor is not null)await monitor.DisposeAsync();monitor=null;
                if(m.SelectedFundOrderTrade!.TradeType is TradeType.ShortIronCondor or TradeType.LongIronCondor){
                    monitor=new(scope,m.SelectedFund!,m.SelectedFundOrder!,m.SelectedFundOrderTrade,m.ValueDate,m.BaseContracts.ToArray(),portfolioId:m.SelectedPortfolio!.PortfolioId,establishedTrade:established,brokerEnvironment:scope.AppEnvironment=="Development"?BrokerEnvironment.Emulator:BrokerEnvironment.Live);
                    await monitor.LoadEstablishedTradeAsync(stopping.Token);if(monitor.TradeHistory.Length>0)await Task.WhenAll(monitor.LoadSavedPositionDataAsync(monitor.TradeHistory.Length-1),monitor.LoadTradePlans(monitor.TradeHistory.Length-1));
                }break;
            case "position":if(monitor is null||!monitor.CanSelectPositionHistory)throw new InvalidOperationException("Position selection is unavailable while live");await Task.WhenAll(monitor.LoadSavedPositionDataAsync(Index(input)),monitor.LoadTradePlans(Index(input)));break;
            case "live-on":if(monitor is null||!monitor.CanEnableLiveFeed)throw new InvalidOperationException("Live feed is unavailable for this trade or session");await monitor.EnableLiveFeedAsync(stopping.Token);break;
            case "live-off":if(monitor is not null)await monitor.DisableLiveFeedAsync(stopping.Token);break;
        }
        return Snapshot();
    }
    TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.FundOrderProjectionReadModel CurrentOrder(ContractEditorIntent i){
        if(model!.SelectedFundOrder is not {} selected || i.OriginalId!=selected.OrderId.ToString(CultureInfo.InvariantCulture))throw new InvalidOperationException("The selected order changed; reload before continuing");
        return model.CanonicalOrders.Single(x=>x.OrderId==selected.OrderId);
    }
    static readonly JsonSerializerOptions JsonOptions=new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase,Converters={new System.Text.Json.Serialization.JsonStringEnumConverter()}};
    static ContractEditorRow Row(string kind,string id,object value)=>new(kind+":"+id,new(){["kind"]=kind,["data"]=JsonSerializer.Serialize(value,JsonOptions)});
    ContractEditorResult Snapshot(){
        var m=model!;var rows=new List<ContractEditorRow>();
        rows.AddRange(m.FundOrders.Select(x=>Row("order",x.OrderId.ToString(),new{x.OrderId,x.Status,CreatedOnUtc=EasternTime.FromUtc(x.CreatedOnUtc).ToString("yyyy-MMM-dd",CultureInfo.InvariantCulture),x.OperatorReference,source=x.Origin.ToString()=="ManualUi"?"Manual":"Strategy Workflow"})));
        rows.AddRange(m.FundOrderTrades.Select((x,n)=>Row("trade",x.TradeId.ToString(),new{index=n,x.TradeId,x.TradeType,x.EffectiveDate,x.TradeDate,x.MaturityDate,x.TradeState,x.TradeAction,x.BaseContractId})));
        if(established is not null){rows.Add(Row("established",established.Id.Format(),established));}
        if(monitor is not null){rows.AddRange(monitor.TradeHistory.Select((x,n)=>Row("position",n.ToString(),x)));rows.AddRange(monitor.IronCondorPlanHistory.Select((x,n)=>Row("plan",n.ToString(),x)));rows.Add(Row("limits","0",monitor.TradeLimitSnapshot));rows.Add(Row("position-details","0",monitor.SelectedSavedPosition));rows.AddRange(monitor.SavedLegObservations.Select((x,n)=>Row("observation",n.ToString(),x)));}
        if(newOrder is not null)rows.Add(Row("order-draft","0",new{newOrder.OrderId,newOrder.OrderDate,newOrder.OrderStatus}));
        if(endOfDay is not null)rows.Add(Row("eod","0",new{endOfDay.PortfolioId,endOfDay.FundId,endOfDay.OrderId,endOfDay.TradeId,endOfDay.ValueDate,endOfDay.OpenPrice,endOfDay.HighPrice,endOfDay.LowPrice,endOfDay.ClosePrice,endOfDay.Volume,endOfDay.TradePnl,endOfDay.FundBalance,endOfDay.CanRun,endOfDay.CommandId,endOfDay.IsCompleted,endOfDay.LastStatusMessage}));
        return new("trade-orders",rows.ToArray(),new(){["portfolio"]=m.Portfolios.Select(x=>x.PortfolioId+" - "+x.Name).ToArray(),["fund"]=m.Funds.Select(x=>x.FundId+" - "+x.Name).ToArray(),["baseContract"]=m.BaseContracts.Select(x=>x.ContractId).ToArray(),["tradeType"]=["ShortIronCondor","LongIronCondor","PutCreditSpread","PutDebitSpread","CallCreditSpread","CallDebitSpread","FuturesOutright"]},Context:new(){
            ["portfolioIndex"]=m.PortfolioSelectedIndex.ToString(),["fundIndex"]=m.FundSelectedIndex.ToString(),["orderId"]=m.SelectedFundOrder?.OrderId.ToString()??"",["tradeId"]=m.SelectedFundOrderTrade?.TradeId.ToString()??"",["tradeState"]=m.SelectedFundOrderTrade?.TradeState.ToString()??"",["valueDate"]=scope.ValueDates.ValueDate.ToString("yyyy-MM-dd"),["from"]=fromDate.ToString("yyyy-MM-dd"),["to"]=toDate.ToString("yyyy-MM-dd"),["preparedTradeId"]=preparedTradeId?.ToString()??"",["fundId"]=m.SelectedFund?.FundId.ToString()??"",["portfolioId"]=m.SelectedPortfolio?.PortfolioId.ToString()??"",
            ["closingType"]=m.FundOrderTrades.FirstOrDefault(x=>x.PrimaryTrade) is {} opening?PortfolioFundOrderEditorPolicy.ClosingType(opening.TradeType).ToString():"",["closingContract"]=m.FundOrderTrades.FirstOrDefault(x=>x.PrimaryTrade)?.BaseContractId??"",
            ["canCreateOrder"]=m.CanCreateOrder.ToString(),["canRemoveOrder"]=m.CanDeleteOrder.ToString(),["canAddTrade"]=m.CanAddTrade.ToString(),["canRemoveTrade"]=m.CanRemoveTrade.ToString(),["canLoadTrade"]=m.CanLoadOrder.ToString(),["canSubmit"]=m.CanSubmitOrder.ToString(),["canEndOfDay"]=m.CanEndOfDay.ToString(),["canLive"]=monitor?.CanEnableLiveFeed.ToString()??"False",["live"]=monitor?.IsLiveFeedEnabled.ToString()??"False",["canSelectPosition"]=monitor?.CanSelectPositionHistory.ToString()??"False",["status"]=m.LastStatusMessage});
    }
    public void CancelPending()=>stopping.Cancel();
    public void Dispose(){stopping.Cancel();monitor?.DisposeAsync().AsTask().GetAwaiter().GetResult();endOfDay?.DisposeAsync().AsTask().GetAwaiter().GetResult();newOrder?.DisposeAsync().AsTask().GetAwaiter().GetResult();model?.DisposeAsync().AsTask().GetAwaiter().GetResult();scope.DisposeAsync().AsTask().GetAwaiter().GetResult();stopping.Dispose();}
}
