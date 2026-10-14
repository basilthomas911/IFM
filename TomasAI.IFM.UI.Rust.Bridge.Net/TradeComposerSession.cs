using System.Globalization;
using TomasAI.IFM.Domain.Trade.Shared.Order.Broker;
using System.Text.Json;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.TradeOrder.ViewModels;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models.Portfolio;
using TomasAI.IFM.UI.Net.ViewModels.Trade;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.UI.Rust.Bridge.Net;
/// <summary>Native host adapter for the same market selection, broker draft and account commands used in WinForms.</summary>
sealed class TradeComposerSession : ITradeOrderLegConfirmationService
{
    readonly RustClientServiceScope scope;
    readonly PortfolioFundOrderTradeEditorModel trade;
    readonly FuturesContractV3ReadModel underlying;
    readonly BrokerManualTradeOrderViewModel model;
    readonly CancellationToken token;
    readonly int portfolioId;
    readonly bool editable;
    readonly bool canLive;
    bool defaultsLoaded;
    BrokerTradeInitializationResult? initialization;
    int quantity=1;
    decimal? editedPrice;
    bool manualSelection;
    bool submitted;
    string reportPreview="",reportPath="",reportHash="";
    readonly Dictionary<BrokerOrderId,Guid> pendingMutations=[];
    public bool OpeningFilled=>submitted&&model.SubmittedTradeOrders.Count>0&&model.SubmittedTradeOrders.All(o=>executions.Any(e=>e.TradeOrderId==o.Id&&e.Status==OrderExecutionStatus.Filled&&e.OrderQuantity>0&&e.CumulativeFilledQuantity==e.OrderQuantity));
    string orderType="Limit",algorithm="None",timeInForce="Day",pace="Normal",executionAction="Open";
    static TomasAI.IFM.Application.TradeBroker.Contracts.BrokerCapabilities Capabilities=>TomasAI.IFM.Application.TradeBroker.Contracts.BrokerCapabilities.Emulator("IFM-EMULATOR-PAPER");
    readonly string sessionId=Guid.NewGuid().ToString("N");
    readonly string subscriptionOwner = Guid.NewGuid().ToString("N");
    readonly object confirmationGate=new();
    TaskCompletionSource<TradeOrderConfirmationResult>? decision;
    string? confirmation;
    Guid confirmationId;
    public string? Confirmation {get{lock(confirmationGate)return confirmation;}}
    public bool Decide(ContractEditorIntent intent){lock(confirmationGate){
        if(decision is null || intent.OriginalId!=confirmationId.ToString())return false;
        var confirmed=intent.Intent=="confirm-order";
        if(!Enum.TryParse<TradeFillType>(intent.Fields.GetValueOrDefault("fillType"),out var fill)|| fill is not (TradeFillType.Manual or TradeFillType.Broker))return false;
        decision.TrySetResult(new(confirmed,fill));confirmation=null;decision=null;return true;
    }}
    OptionContractExpiryReadModel[] expirations=[];
    EvaluatedOptionChainReadModel? chain;
    EvaluatedOptionChainReadModel? displayChain;
    BrokerTradeLegData[] legs=[];
    BrokerOrderDefinition[] brokerOrders=[];
    OrderExecutionDefinition[] executions=[];
    DateOnly expiry;
    bool live;
    string status="";
    decimal putDelta=16,callDelta=16,putWidth=50,callWidth=50;
    public TradeComposerSession(RustClientServiceScope root,TradeOrderEditorViewModel editor,CancellationToken cancellation){
        portfolioId=editor.SelectedPortfolio!.PortfolioId;editable=editor.CanSubmitOrder;canLive=editor.CanUseLiveFeed;
        scope=root;token=cancellation;trade=editor.SelectedFundOrderTrade??throw new InvalidOperationException("Select a trade");
        underlying=editor.BaseContracts.Single(x=>x.ContractId==trade.BaseContractId);
        model=new(root,editor.SelectedPortfolio!.PortfolioId,editor.SelectedFundOrder!,trade,underlying);expiry=trade.EffectiveDate;
    }
    static string F(ContractEditorIntent input,string key)=>input.Fields.GetValueOrDefault(key)??"";
    static decimal Number(ContractEditorIntent input,string key)=>decimal.Parse(F(input,key),CultureInfo.InvariantCulture);
    public async Task ExecuteAsync(ContractEditorIntent input){
        switch(input.Intent){
            case "compose":
                var initializing=BrokerTradeInitializationQuery.ExecuteAsync(scope,portfolioId,trade.FundId,trade.OrderId,trade.TradeId,underlying.ContractId,scope.ValueDates.ValueDate,legs,Capabilities,token);
                await LoadDefaults();
                if(model.StrategyKind==TradeStrategyKind.FuturesOutright){legs=[new(underlying.ContractId,trade.TradeAction==TradeAction.Sell?"-Future":"+Future",null,null,null,null,false,Multiplier:decimal.Parse(underlying.Multiplier,CultureInfo.InvariantCulture),IsFuture:true)];}
                else {var result=await scope.Services.MarketDataQueries.QueryDatabentoOptionChainRangeAsync(underlying.Symbol,scope.ValueDates.ValueDate,trade.RequestedMaturityDate??trade.RequestedTradeDate,token);Require(result);expirations=(result.Value??[]).Where(x=>x.ExpiryDate>=trade.RequestedTradeDate).OrderBy(x=>x.ExpiryDate).ToArray();if(expirations.Length>0){expiry=expirations.Min(x=>x.ExpiryDate);await LoadChain();}}
                initialization=await initializing;await LoadExecutions();if(trade.TradeState!=TradeState.NewTrade){manualSelection=true;legs=[];var stored=brokerOrders.OrderByDescending(x=>x.ChangedAtUtc).SelectMany(x=>x.Order.Components).FirstOrDefault(x=>x.ReservedTradeId==(trade.ExecutionTradeId>0?trade.ExecutionTradeId:trade.TradeId));if(stored is not null){legs=BrokerTradePreviewPresentation.StoredLegs(stored);quantity=Math.Max(1,stored.Legs.Select(x=>Math.Abs(x.SignedQuantity)).DefaultIfEmpty(1).Min());editedPrice=stored.SignedNetDebitLimit;if(stored.Legs.Any(x=>x.Expiry.HasValue)){var storedExpiry=stored.Legs.Where(x=>x.Expiry.HasValue).Max(x=>x.Expiry!.Value);if(storedExpiry!=expiry){await ReleaseChain();expiry=storedExpiry;}await LoadChain(false);}}}break;
            case "refresh-chain":if(live&&chain is not null)await LoadChain(select:!manualSelection&&legs.Length<(model.StrategyKind==TradeStrategyKind.IronCondor?4:2));break;
            case "clear-staged":if(!editable||submitted)throw new InvalidOperationException("Market selection is read-only");legs=[];manualSelection=true;editedPrice=null;break;
            case "preview-draft":
                if(F(input,"action") is "Open" or "Close")executionAction=F(input,"action");
                quantity=int.Parse(F(input,"quantity"),CultureInfo.InvariantCulture);if(quantity is <1 or >100000)throw new ArgumentException("Quantity must be between 1 and 100000");
                if(decimal.TryParse(F(input,"price"),NumberStyles.Number,CultureInfo.InvariantCulture,out var enteredPrice))editedPrice=enteredPrice;
                orderType=F(input,"orderType");algorithm=F(input,"algorithm");timeInForce=F(input,"timeInForce");pace=F(input,"pace");
                if(!Enum.TryParse<BrokerOrderType>(orderType,out _)||!Enum.TryParse<BrokerAlgorithm>(algorithm,out _)||timeInForce is not ("Day" or "GTC")||pace is not ("Normal" or "Patient" or "Urgent"))throw new ArgumentException("Invalid execution selection");break;
            case "chain":if(!editable||submitted)throw new InvalidOperationException("Market selection is read-only");var requestedExpiry=DateOnly.ParseExact(F(input,"expiry"),"yyyy-MM-dd",CultureInfo.InvariantCulture);if(expirations.Length>0&&!expirations.Any(x=>x.ExpiryDate==requestedExpiry))throw new ArgumentException("Select an available expiry");if(requestedExpiry!=expiry){await ReleaseChain();manualSelection=false;legs=[];editedPrice=null;}expiry=requestedExpiry;putDelta=Number(input,"putDelta");callDelta=Number(input,"callDelta");putWidth=Number(input,"putWidth");callWidth=Number(input,"callWidth");
                if(putDelta<=0||putDelta>100||callDelta<=0||callDelta>100||putWidth<=0||callWidth<=0||putWidth>100000||callWidth>100000)throw new ArgumentException("Deltas must be between 1 and 100; widths must be positive");
                var requestedLive=F(input,"live")=="true";if(requestedLive&&!canLive)throw new InvalidOperationException("Live Feed is unavailable outside the permitted trading session or Development emulator");if(requestedLive!=live){await ReleaseChain();manualSelection=false;legs=[];editedPrice=null;}live=requestedLive;if(model.StrategyKind==TradeStrategyKind.FuturesOutright)break;if(expirations.Length==0){status="No option expiries are available for the selected trade date range.";break;}status="";await LoadChain();break;
            case "remove-leg":if(!editable||submitted)throw new InvalidOperationException("Market selection is read-only");manualSelection=true;editedPrice=null;legs=legs.Where(x=>x.ContractId!=F(input,"contractId")).ToArray();break;
            case "select-leg":
                if(!editable||submitted)throw new InvalidOperationException("Market selection is read-only");manualSelection=true;editedPrice=null;
                var id=F(input,"contractId");var selected=chain?.Contracts.Single(x=>x.ContractId==id)??throw new InvalidOperationException("Reload option chains");
                var role=F(input,"role");if(role is not ("-SC" or "+LC" or "-SP" or "+LP"))throw new ArgumentException("Invalid leg role");
                if(selected.IsCall!=role.EndsWith('C'))throw new ArgumentException("Role must match call/put");
                if(model.StrategyKind==TradeStrategyKind.VerticalSpread&&selected.IsCall!=trade.TradeType.ToString().StartsWith("Call"))throw new InvalidOperationException("Select contracts on the call/put side of this vertical strategy");
                if(legs.Any(x=>x.ContractId==id)||legs.Any(x=>x.Role==role)||legs.Count(x=>x.IsCall==selected.IsCall)>=2||legs.Length>=(model.StrategyKind==TradeStrategyKind.VerticalSpread?2:4))throw new InvalidOperationException("Remove a selected leg before selecting another contract");
                legs=BrokerTradeLegSelection.Normalize(trade.TradeType,legs.Append(ToLeg(selected,role)).ToArray());break;
            case "account":reportPreview=reportPath=reportHash="";initialization=await BrokerTradeInitializationQuery.ExecuteAsync(scope,portfolioId,trade.FundId,trade.OrderId,trade.TradeId,underlying.ContractId,scope.ValueDates.ValueDate,legs,Capabilities,token);break;
            case "refresh-execution":await LoadExecutions();break;
            case "update-limit":case "cancel-unfilled":
                await LoadExecutions();
                var broker=brokerOrders.SingleOrDefault(x=>x.Id.Format()==F(input,"brokerOrderId"))??throw new InvalidOperationException("Select a current broker order");
                var selectedComponent=broker.Order.Components.Single(x=>x.ComponentId==broker.Id.ComponentId);
                if(!BrokerOrderMutationRules.CanMutate(broker.Status,executions.FirstOrDefault(x=>x.Id==broker.Id.Execution)?.Status,selectedComponent,pendingMutations.ContainsKey(broker.Id)))throw new InvalidOperationException("The selected broker order cannot be changed until it is working with valid bounds and no pending acknowledgement");
                if(pendingMutations.ContainsKey(broker.Id))throw new InvalidOperationException("Waiting for broker acknowledgement of the previous mutation");
                var operation=Guid.NewGuid();pendingMutations[broker.Id]=operation;
                try{
                if(input.Intent=="cancel-unfilled")Require(await scope.Services.BrokerOrderCommands.CancelAsync(broker.Id,operation));
                else{var limit=Number(input,"limitPrice");var component=broker.Order.Components.Single(x=>x.ComponentId==broker.Id.ComponentId);if(component.TickIncrement is not >0||component.MinimumSignedNetDebitLimit is null||component.MaximumSignedNetDebitLimit is null||limit<component.MinimumSignedNetDebitLimit||limit>component.MaximumSignedNetDebitLimit||limit%component.TickIncrement!=0)throw new ArgumentException("Limit price must be inside the component bounds and match its tick increment");Require(await scope.Services.BrokerOrderCommands.UpdatePriceAsync(broker.Id,limit,operation));}
                }catch{pendingMutations.Remove(broker.Id);throw;}
                status="Broker mutation accepted; refresh for execution acknowledgement";await LoadExecutions();break;
            case "qualification-report":
                reportPath=Path.GetFullPath(F(input,"reportPath"));var reportBytes=await File.ReadAllBytesAsync(reportPath,token);reportHash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(reportBytes)).ToLowerInvariant();reportPreview=System.Text.Encoding.UTF8.GetString(reportBytes);break;
            case "qualification-submit":Require(await model.SubmitQualificationEvidenceAsync(F(input,"manifestHash"),F(input,"evidenceReference"),token));break;
            case "qualification-accept":Require(await model.AcceptQualificationAsync(Guid.NewGuid(),F(input,"manifestHash"),F(input,"reviewer"),token));break;
            case "qualification-revoke":Require(await model.RevokeQualificationAsync(F(input,"reason"),F(input,"reviewer"),token));break;
            case "qualification-hold":Require(await model.SetManualHoldAsync(true,F(input,"reason"),token));break;
            case "qualification-release":Require(await model.SetManualHoldAsync(false,F(input,"reason"),token));break;
            case "qualification-resync":Require(await model.RequestResynchronizationAsync(token));break;
            case "submit-order":
                if(executionAction!="Open")throw new InvalidOperationException("This New Trade screen submits opening orders only.");
                quantity=int.Parse(F(input,"quantity"),CultureInfo.InvariantCulture);var price=Number(input,"price");
                if(price%(model.StrategyKind==TradeStrategyKind.FuturesOutright?.25m:.05m)!=0)throw new ArgumentException("Order price must match the futures/option tick increment");
                if(model.StrategyKind!=TradeStrategyKind.FuturesOutright){await LoadChain(false);if(legs.Any(l=>!chain!.Contracts.Any(x=>x.ContractId==l.ContractId&&x.Bid is >0&&x.Ask>=x.Bid)))throw new InvalidOperationException("Complete quotes are unavailable for the selected legs");}
                model.SetScreenLegSelection(expiry,legs,legs.ToDictionary(x=>x.ContractId,_=>quantity));
                model.SetExecutionSelection(Enum.Parse<BrokerOrderType>(F(input,"orderType")),Enum.Parse<BrokerAlgorithm>(F(input,"algorithm")),F(input,"timeInForce"),F(input,"pace"));
                var command=await model.SubmitAsync(quantity,price,this,token);submitted=command!=Guid.Empty;status=command==Guid.Empty?"Submission cancelled":$"Order submission completed: {command}";await LoadExecutions();break;
        }
    }
    async Task LoadDefaults(){
        if(model.StrategyKind==TradeStrategyKind.FuturesOutright){defaultsLoaded=true;return;}
        var component=model.StrategyKind==TradeStrategyKind.IronCondor?TomasAI.IFM.Domain.Reference.Shared.ParameterSets.ParameterSchemaRegistry.IronCondorMarketSelectionComponent:TomasAI.IFM.Domain.Reference.Shared.ParameterSets.ParameterSchemaRegistry.VerticalSpreadMarketSelectionComponent;
        var result=await scope.Services.ParameterSets.StartupRunsAsync();Require(result);var run=result.Value?.OrderByDescending(x=>x.CreatedAtUtc).FirstOrDefault(x=>x.Scopes.Any(y=>y.Enabled&&y.Scope.ComponentCode==component));var assignment=run?.Scopes.SingleOrDefault(x=>x.Enabled&&x.Scope.ComponentCode==component);var version=assignment is null?null:run!.Versions.SingleOrDefault(x=>x.Reference==assignment.Reference);if(version is null){status="No applied startup defaults are available for "+component;return;}defaultsLoaded=true;
        if(model.StrategyKind==TradeStrategyKind.IronCondor){var defaults=JsonSerializer.Deserialize<TomasAI.IFM.Domain.Reference.Shared.ParameterSets.IronCondorMarketSelectionParameterSet>(version.PayloadJson)!;var value=defaults.Symbols.Single(x=>x.Symbol.Equals(underlying.Symbol,StringComparison.OrdinalIgnoreCase));callDelta=value.ShortCallDelta;putDelta=value.ShortPutDelta;callWidth=value.CallSpreadWidth;putWidth=value.PutSpreadWidth;}
        else{var defaults=JsonSerializer.Deserialize<TomasAI.IFM.Domain.Reference.Shared.ParameterSets.VerticalSpreadMarketSelectionParameterSet>(version.PayloadJson)!;var value=defaults.Symbols.Single(x=>x.Symbol.Equals(underlying.Symbol,StringComparison.OrdinalIgnoreCase));callDelta=putDelta=value.ShortLegDelta;callWidth=putWidth=value.SpreadWidth;}
    }
    async Task LoadExecutions(){
        var orders=new List<BrokerOrderDefinition>();var evidence=new Dictionary<OrderExecutionId,OrderExecutionDefinition>();
        foreach(var orderId in model.SubmittedTradeOrders.Select(x=>x.Id).Append(new TradeOrderId(portfolioId,trade.FundId,trade.OrderId)).Append(new TradeOrderId(portfolioId,trade.FundId,trade.ExecutionOrderId>0?trade.ExecutionOrderId:trade.OrderId)).Distinct()){var result=await scope.Services.BrokerOrders.ListAsync(orderId);Require(result);foreach(var order in result.Value??[]){orders.Add(order);var execution=await scope.Services.OrderExecutions.GetAsync(orderId,order.Id.Execution.ExecutionAttemptId);Require(execution);if(execution.Value is {} value)evidence[value.Id]=value;}}
        brokerOrders=orders.ToArray();executions=evidence.Values.ToArray();foreach(var order in brokerOrders)if(pendingMutations.TryGetValue(order.Id,out var operation)&&order.OperationId==operation)pendingMutations.Remove(order.Id);
    }
    static void Require<T>(ServiceResult<T> result){if(!result.Success)throw new InvalidOperationException($"{result.ErrorCode}: {result.ErrorMessage}");}
    async Task LoadChain(bool select=true){
        var result=await scope.Services.MarketDataQueries.QueryEvaluatedOptionChainAsync(new(){UnderlyingContractId=underlying.ContractId,UnderlyingSymbol=underlying.Symbol,ProviderRoots=expirations.Where(x=>x.ExpiryDate==expiry).Select(x=>x.ProviderRoot).Distinct().ToArray(),ExpiryDate=expiry,SpreadWingWidth=Math.Max(putWidth,callWidth),RequiredContractIds=legs.Select(x=>x.ContractId).ToArray(),SubscriptionOwnerId=subscriptionOwner,AllowFrozenEmulatorPreview=!live&&scope.AppEnvironment=="Development",FrozenEmulatorPreviewOnly=!live&&scope.AppEnvironment=="Development"},token);
        Require(result);chain=result.Value??throw new InvalidOperationException("Option-chain snapshot is unavailable");
        displayChain=OptionChainDisplaySnapshot.Merge(displayChain,chain);
        if(select&&!manualSelection&&defaultsLoaded&&trade.TradeState==TradeState.NewTrade){editedPrice=null;var pairs=new List<BrokerTradeLegData>();foreach(var isCall in new[]{true,false}){
            if(model.StrategyKind==TradeStrategyKind.VerticalSpread && isCall!=trade.TradeType.ToString().StartsWith("Call"))continue;
            var credit=trade.TradeType is TradeType.ShortIronCondor or TradeType.PutCreditSpread or TradeType.CallCreditSpread;
            var up=isCall?credit:!credit;var width=isCall?callWidth:putWidth;
            foreach(var shortLeg in OptionSpreadLegSelection.ShortCandidates(chain.Contracts,isCall,(isCall?callDelta:putDelta)/100m,chain.UnderlyingPrice,allowFrozenEmulatorPreview:!live&&scope.AppEnvironment=="Development"&&chain.WindowMethod.StartsWith("Frozen emulator preview",StringComparison.Ordinal))){
                var wing=OptionSpreadLegSelection.Wing(chain.Contracts.Where(x=>x.IsCall==isCall).Select(x=>(x.ContractId,x.Strike)),shortLeg.Strike,width,up);
                if(wing is null)continue;pairs.Add(ToLeg(shortLeg,isCall?"-SC":"-SP"));pairs.Add(ToLeg(chain.Contracts.Single(x=>x.ContractId==wing),isCall?"+LC":"+LP"));break;
            }
        }legs=pairs.ToArray();}else{legs=legs.Select(l=>chain.Contracts.FirstOrDefault(x=>x.ContractId==l.ContractId) is {} c?ToLeg(c,l.Role):l).ToArray();}
    }
    BrokerTradeLegData ToLeg(EvaluatedOptionContractReadModel c,string role)=>new(c.ContractId,role,c.Strike,c.Bid,c.Ask,c.Delta,c.IsCall,c.Vega,decimal.Parse(underlying.Multiplier,CultureInfo.InvariantCulture),c.QuoteAtUtc,Frozen:!live);
    public async Task<(ContractEditorRow[] Rows,Dictionary<string,string[]> Choices,Dictionary<string,string> Context)> SnapshotAsync(){
        var rows=new List<ContractEditorRow>();if(displayChain is not null)rows.Add(Row("market","0",displayChain));rows.AddRange(legs.OrderByDescending(x=>x.Strike).ThenByDescending(x=>x.IsCall).Select((x,n)=>Row("leg",n.ToString(),x)));rows.AddRange(model.SubmittedTradeOrders.Select((x,n)=>Row("submitted-order",n.ToString(),x)));rows.AddRange(brokerOrders.Select(x=>Row("broker-order",x.Id.Format(),x)));rows.AddRange(executions.Select(x=>Row("execution",x.Id.Format(),x)));rows.AddRange(executions.SelectMany(x=>x.Fills).Select((x,n)=>Row("fill",n.ToString(),x)));
        foreach(var order in brokerOrders){rows.Add(Row("broker-caption",order.Id.Format(),new{Text=OrderCaption(order)}));var execution=executions.FirstOrDefault(x=>x.Id==order.Id.Execution);var summary=new{OrderId=order.Id.Format(),Status=execution?.Status is OrderExecutionStatus.Filled or OrderExecutionStatus.Cancelled or OrderExecutionStatus.Rejected?execution.Status.ToString():order.Status.ToString(),BrokerStatus=order.Status,ExecutionStatus=execution?.Status,Price=order.CurrentSignedNetDebitLimit,order.BrokerRevision,order.Order.TimeInForce,order.Order.BrokerAlgorithm,order.Order.AlgorithmPace,Requested=execution?.OrderQuantity,Filled=execution?.CumulativeFilledQuantity,Remaining=execution is null?(int?)null:Math.Max(0,execution.OrderQuantity-execution.CumulativeFilledQuantity),order.DispatchCategory,order.DispatchDetail,order.ChangedAtUtc};rows.Add(Row("broker-summary",order.Id.Format(),summary));rows.Add(Row("broker-properties",order.Id.Format(),Properties(summary)));var component=order.Order.Components.Single(x=>x.ComponentId==order.Id.ComponentId);var terminal=executions.Any(x=>x.Id==order.Id.Execution&&x.Status is OrderExecutionStatus.Filled or OrderExecutionStatus.Cancelled or OrderExecutionStatus.Rejected);rows.Add(Row("broker-mutation",order.Id.Format(),new{canMutate=BrokerOrderMutationRules.CanMutate(order.Status,execution?.Status,component,pendingMutations.ContainsKey(order.Id)),minimum=component.MinimumSignedNetDebitLimit,maximum=component.MaximumSignedNetDebitLimit,tick=component.TickIncrement,pending=pendingMutations.ContainsKey(order.Id)}));}
        rows.AddRange(executions.SelectMany(x=>x.Fills).DistinctBy(x=>x.ExecutionFillId).Select(x=>Row("fill-properties",x.ExecutionFillId.ToString(),Properties(x))));
        if(chain is not null)rows.Add(Row("market-metrics","0",MarketMetrics()));
        var preview=BrokerTradePreviewPresentation.Calculate(legs,legs.ToDictionary(x=>x.ContractId,_=>quantity),quantity,editedPrice,initialization);rows.Add(Row("broker-preview","0",preview));
        rows.Add(Row("qualification-report","0",new{path=reportPath,hash=reportHash,preview=reportPreview}));
        var account=await model.GetAccountAsync(token);if(!account.Success)status=$"Account: {account.ErrorCode}: {account.ErrorMessage}";if(account.Value is {} a){rows.Add(Row("account","0",a));var displayFields=new Dictionary<string,string>(preview.Fields){["Route check"]=a.DevelopmentQualificationsExempt?"Development emulator - qualification exempt":$"{a.QualificationStatus} / {a.Gate}"};rows.RemoveAll(x=>x.Id=="broker-preview:0");rows.Add(Row("broker-preview","0",new{Fields=displayFields,preview.OrderPrice}));}
        return(rows.ToArray(),new(){["expiry"]=expirations.Select(x=>x.ExpiryDate.ToString("yyyy-MM-dd")).Distinct().Order().ToArray()},new(){["composerSessionId"]=sessionId,["expiry"]=expiry.ToString("yyyy-MM-dd"),["compositionType"]=trade.TradeType.ToString(),["brokerMode"]=scope.AppEnvironment=="Development"?"Emulator":"Live",["composerStatus"]=status,["effectiveDate"]=trade.EffectiveDate.ToString("yyyy-MM-dd"),["editable"]=(editable&&!submitted&&defaultsLoaded).ToString(),["submitted"]=submitted.ToString(),["live"]=live.ToString(),["quantity"]=quantity.ToString(CultureInfo.InvariantCulture),["price"]=preview.OrderPrice?.ToString(CultureInfo.InvariantCulture)??"",["previewKey"]=string.Join("|",legs.Select(x=>x.ContractId+x.Role)),["callDelta"]=callDelta.ToString(CultureInfo.InvariantCulture),["putDelta"]=putDelta.ToString(CultureInfo.InvariantCulture),["callWidth"]=callWidth.ToString(CultureInfo.InvariantCulture),["putWidth"]=putWidth.ToString(CultureInfo.InvariantCulture),["underlyingReferencePrice"]=initialization?.UnderlyingPrice?.ClosePrice.ToString(CultureInfo.InvariantCulture)??"",["underlying"]=underlying.ContractId,["symbol"]=underlying.Symbol,["multiplier"]=underlying.Multiplier,["venue"]=underlying.Exchange,["valueDate"]=scope.ValueDates.ValueDate.ToString("yyyy-MM-dd"),["executionAction"]=executionAction,["canPlaceOrder"]=(editable&&!submitted&&legs.Length==(model.StrategyKind==TradeStrategyKind.FuturesOutright?1:model.StrategyKind==TradeStrategyKind.VerticalSpread?2:4)&&legs.All(x=>x.IsFuture||(x.Sign!=0&&x.Bid.HasValue&&x.Ask.HasValue))).ToString()});
    }
    static object Properties(object value)=>new{Properties=System.ComponentModel.TypeDescriptor.GetProperties(value).Cast<System.ComponentModel.PropertyDescriptor>().Select(p=>new{Name=p.Name,Display=p.Converter.ConvertToString(null,CultureInfo.CurrentCulture,p.GetValue(value))??""}).ToArray()};
    static string OrderCaption(BrokerOrderDefinition order){
        var component=order.Order.Components.Single(x=>x.ComponentId==order.Id.ComponentId);
        string Leg(TradeLegDefinition leg)=>leg.AssetFamily==TradeAssetFamily.FuturesOption?leg.Strike?.ToString("0.########",CultureInfo.InvariantCulture)??"":$"{(leg.SignedQuantity>0?"buy":"sell")} {leg.ContractId}";
        var calls=component.Legs.Where(x=>x.PutCall==1).OrderByDescending(x=>x.SignedQuantity).Select(Leg);var puts=component.Legs.Where(x=>x.PutCall==2).OrderBy(x=>x.SignedQuantity).Select(Leg);var groups=new[]{string.Join("-",calls),string.Join("-",puts)}.Where(x=>x.Length>0);var legs=component.Legs.All(x=>x.AssetFamily==TradeAssetFamily.FuturesOption)?string.Join(":",groups):string.Join("-",component.Legs.Select(Leg));
        var type=component.StrategyKind switch{TradeStrategyKind.IronCondor=>component.Legs.Any(x=>x.PutCall==1&&x.SignedQuantity>0&&x.Strike==component.Legs.Where(y=>y.PutCall==1).Max(y=>y.Strike))?"ShortIronCondor":"LongIronCondor",TradeStrategyKind.VerticalSpread=>"Vertical Spread",TradeStrategyKind.FuturesOutright=>"Futures",_=>component.StrategyKind.ToString()};return $"{type} = {legs} @ {order.CurrentSignedNetDebitLimit.ToString("0.########",CultureInfo.InvariantCulture)}";
    }
    Dictionary<string,string> MarketMetrics(){
        var c=chain!;var nearest=c.UnderlyingPrice is {} price?c.Contracts.Where(x=>x.ImpliedVolatility is >0&&double.IsFinite(x.ImpliedVolatility.Value)).OrderBy(x=>Math.Abs(x.Strike-price)).ThenBy(x=>x.Strike).FirstOrDefault()?.Strike:null;
        var iv=nearest.HasValue?c.Contracts.Where(x=>x.Strike==nearest&&x.ImpliedVolatility is >0&&double.IsFinite(x.ImpliedVolatility.Value)).Average(x=>x.ImpliedVolatility):null;
        var latest=c.Contracts.Where(x=>x.QuoteAtUtc.HasValue).Select(x=>x.QuoteAtUtc).Max();
        return new(){["DTE"]=(expiry.DayNumber-scope.ValueDates.ValueDate.DayNumber).ToString(CultureInfo.InvariantCulture),["Underlying"]=underlying.ContractId,["Multiplier"]=underlying.Multiplier,["Tick"]=model.StrategyKind==TradeStrategyKind.FuturesOutright?"0.25":"0.05",["Settlement"]="Unavailable",["Daily Std Dev"]=c.BollingerStandardDeviation20?.ToString("0.00")??"N/A",["Last"]=c.LastUnderlyingPrice?.ToString("0.00")??"N/A",["Daily Price Change"]=c.LastUnderlyingPrice.HasValue&&c.PreviousSessionClose is >0?$"{c.LastUnderlyingPrice.Value-c.PreviousSessionClose.Value:+0.00;-0.00;0.00} ({(c.LastUnderlyingPrice.Value/c.PreviousSessionClose.Value-1):+0.00%;-0.00%;0.00%})":"N/A",["IV"]=iv?.ToString("P1",CultureInfo.InvariantCulture)??"N/A",["IV Rank"]=c.IvRank?.ToString("0.0")??(c.IvRankStatus.StartsWith("InsufficientHistory",StringComparison.Ordinal)?"History needed":"N/A"),["Quote Age"]=latest.HasValue?$"{Math.Max(0,(c.AsOfUtc-latest.Value).TotalMilliseconds):0} ms":!live?"Frozen preview":"N/A"};
    }
    static ContractEditorRow Row(string kind,string id,object v)=>new(kind+":"+id,new(){["kind"]=kind,["data"]=JsonSerializer.Serialize(v,new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase,Converters={new System.Text.Json.Serialization.JsonStringEnumConverter()}})});
    public ValueTask<TradeOrderConfirmationResult> ConfirmAsync(TradeOrderReadModel order,CancellationToken cancellationToken=default)=>ConfirmAsync(order,[],cancellationToken);
    public async ValueTask<TradeOrderConfirmationResult> ConfirmAsync(TradeOrderReadModel order,IReadOnlyList<TradeOrderConfirmationLeg> legs,CancellationToken cancellationToken=default){
        Task<TradeOrderConfirmationResult> pending;lock(confirmationGate){if(decision is not null)throw new InvalidOperationException("A confirmation is already pending");confirmationId=Guid.NewGuid();decision=new(TaskCreationOptions.RunContinuationsAsynchronously);pending=decision.Task;confirmation=JsonSerializer.Serialize(new{id=confirmationId,order,legs},new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase});}
        try{return await pending.WaitAsync(cancellationToken);}finally{lock(confirmationGate){confirmation=null;decision=null;}}
    }
    async Task ReleaseChain(){displayChain=null;if(chain is not null){Require(await scope.Services.MarketDataQueries.QueryEvaluatedOptionChainAsync(new(){UnderlyingContractId=underlying.ContractId,UnderlyingSymbol=underlying.Symbol,ExpiryDate=expiry,ReleaseOnly=true,SubscriptionOwnerId=subscriptionOwner},token));chain=null;}}
    public async Task CloseAsync(){lock(confirmationGate){decision?.TrySetResult(new(false,TradeFillType.Broker));confirmation=null;}if(chain is not null)await scope.Services.MarketDataQueries.QueryEvaluatedOptionChainAsync(new(){UnderlyingContractId=underlying.ContractId,UnderlyingSymbol=underlying.Symbol,ExpiryDate=expiry,ReleaseOnly=true,SubscriptionOwnerId=subscriptionOwner},token);}
}
