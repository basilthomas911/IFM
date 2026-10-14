using System.Globalization;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models.Reference;
using TomasAI.IFM.UI.Net.Services.Reference;
using TomasAI.IFM.UI.Net.ViewModels.MarketData;
namespace TomasAI.IFM.UI.Rust.Bridge.Net;
/// <summary>Reuses the WinForms contract view models, their validators, paging fences and correlated terminal events.</summary>
sealed class ContractEditorSession : IDisposable
{
    readonly RustClientServiceScope scope;
    readonly FuturesContractEditorViewModel? futures;
    readonly FuturesOptionContractEditorViewModel? options;
    readonly Task ready;
    readonly CancellationTokenSource stopping = new();
    public ContractEditorSession(bool option)
    {
        scope=new(Environment.GetEnvironmentVariable("IFM_UI_NATS_URL")??"nats://localhost:4222");
        var reference=scope.Container.GetInstance<IReferenceDataService>();
        if(option)options=new(scope,reference);else futures=new(scope,reference);
        ready=scope.StartAsync(stopping.Token).AsTask();
    }
    static string Field(ContractEditorIntent input,string name)=>input.Fields.GetValueOrDefault(name)?.Trim()??"";
    static string Key(FuturesContractV3ReadModel row)=>row.Id.Format();
    static string[] Codes(IReadOnlyList<LookupTypeUiModel> values)=>values.Select(x=>x.ShortCode).ToArray();
    public async Task<ContractEditorResult> ExecuteAsync(ContractEditorIntent input)
    {
        await ready;
        if(futures is { } f)
        {
            if(input.Intent=="load")await Run(f.LoadOperation);
            else
            {
                var original=f.FuturesContracts.SingleOrDefault(x=>Key(x)==input.OriginalId);
                if(input.Intent=="remove") {f.PrepareRemove((original??throw new InvalidOperationException("Contract is no longer loaded")).Id);await Run(f.RemoveOperation);}
                else
                {
                    var row=input.Intent=="review"?ReferenceReviewMapping.Futures(input,original??throw new InvalidOperationException("Contract is no longer loaded")):ContractDraftMapping.Futures(input,original);
                    if(!row.IsValid)throw new ArgumentException("Invalid futures contract; on-the-run contracts require rollover enabled");
                    if(input.Intent=="add"){f.PrepareAdd(row);await Run(f.AddOperation);}
                    else {f.PrepareChange((original??throw new InvalidOperationException("Contract is no longer loaded")).Id,row);await Run(f.ChangeOperation);}
                }
            }
            return new(input.View,f.FuturesContracts.Select(x=>ReferenceReviewMapping.Row(new ContractEditorRow(Key(x),new(){
                ["contractId"]=x.ContractId,["description"]=x.Description??"",["symbol"]=x.Symbol,["localSymbol"]=x.LocalSymbol??"",
                ["securityType"]=x.SecurityType??"",["currency"]=x.Currency??"",["exchange"]=x.Exchange??"",["multiplier"]=x.Multiplier??"",
                ["provider"]=x.SchemaVersion>0?"true":"false",["lastTradeDate"]=x.LastTradeDate.ToString("yyyy-MM-dd"),["onTheRun"]=x.OnTheRun.ToString(),["rollover"]=x.Rollover.ToString()}),x)).ToArray(),
                new(){["symbol"]=Codes(f.Symbols),["securityType"]=Codes(f.SecurityTypes),["currency"]=Codes(f.Currencies),["exchange"]=Codes(f.Exchanges),["multiplier"]=Codes(f.Multipliers),["reviewState"]=Enum.GetNames<ReferenceReviewState>(),["exerciseStyle"]=Enum.GetNames<ReferenceExerciseStyle>(),["settlementStyle"]=Enum.GetNames<ReferenceSettlementStyle>(),["premiumStyle"]=Enum.GetNames<ReferencePremiumStyle>(),["premiumTickRule"]=Enum.GetNames<ReferencePremiumTickRule>(),["dayCount"]=Enum.GetNames<ReferenceDayCount>()});
        }
        var o=options!;
        if(input.Intent=="load")
        {
            await Run(o.LoadOperation);
            var symbol=Field(input,"symbol");
            if(symbol.Length>0 && symbol!=o.SelectedSymbol)
            {
                var index=o.Symbols.ToList().FindIndex(x=>x.ShortCode==symbol);
                if(index<0)throw new ArgumentException("Select a published symbol");
                o.SelectSymbol(index);await Run(o.LoadContractsOperation);
            }
        }
        else if(input.Intent=="next")await Run(o.LoadMoreOperation);
        else
        {
            var original=o.FuturesOptionContracts.SingleOrDefault(x=>x.ContractId==input.OriginalId);
            if(input.Intent=="remove"){o.PrepareRemove(original??throw new InvalidOperationException("Contract is no longer loaded"));await Run(o.RemoveOperation);}
            else
            {
                var row=input.Intent=="review"?ReferenceReviewMapping.Option(input,original??throw new InvalidOperationException("Contract is no longer loaded")):ContractDraftMapping.Option(input,original);
                if(!row.IsValid||!double.IsFinite(row.StrikePrice))throw new ArgumentException("Invalid option contract");
                if(input.Intent=="add"){o.PrepareAdd(row);await Run(o.AddOperation);}
                else{o.PrepareChange((original??throw new InvalidOperationException("Contract is no longer loaded")).ContractId,row);await Run(o.ChangeOperation);}
            }
        }
        return new(input.View,o.FuturesOptionContracts.Select(x=>ReferenceReviewMapping.Row(new ContractEditorRow(x.ContractId,new(){
            ["contractId"]=x.ContractId,["description"]=x.Description??"",["symbol"]=x.Symbol??"",["localSymbol"]=x.LocalSymbol??"",
            ["securityType"]=x.SecurityType??"",["currency"]=x.Currency??"",["exchange"]=x.Exchange??"",["multiplier"]=x.Multiplier??"",
            ["provider"]=x.SchemaVersion>0?"true":"false",["contractMonth"]=x.ContractMonth.ToString("yyyy-MM-dd"),["strikePrice"]=x.GetExactStrikePrice().ToString(CultureInfo.InvariantCulture),["optionType"]=x.OptionType??""}),x)).ToArray(),
            new(){["symbol"]=Codes(o.Symbols),["securityType"]=Codes(o.SecurityTypes),["currency"]=Codes(o.Currencies),["exchange"]=Codes(o.Exchanges),["multiplier"]=Codes(o.Multipliers),["optionType"]=Codes(o.OptionTypes),["reviewState"]=Enum.GetNames<ReferenceReviewState>(),["exerciseStyle"]=Enum.GetNames<ReferenceExerciseStyle>(),["settlementStyle"]=Enum.GetNames<ReferenceSettlementStyle>(),["premiumStyle"]=Enum.GetNames<ReferencePremiumStyle>(),["premiumTickRule"]=Enum.GetNames<ReferencePremiumTickRule>(),["dayCount"]=Enum.GetNames<ReferenceDayCount>()},o.HasMoreContracts);
    }
    internal async Task SaveProviderAsync(FuturesContractV3ReadModel draft, FuturesContractV3ReadModel? original)
    {
        await ready;
        var editor = futures ?? throw new InvalidOperationException("Futures editor required");
        await Run(editor.LoadOperation);
        if (!draft.IsValid || draft.SchemaVersion != 1 || draft.ReviewState != ReferenceReviewState.Draft) throw new ArgumentException("A valid provider draft is required");
        if (original is null) { editor.PrepareAdd(draft); await Run(editor.AddOperation); }
        else { editor.PrepareChange(original.Id, draft); await Run(editor.ChangeOperation); }
    }
    internal async Task SaveProviderAsync(FuturesOptionContractReadModel draft, FuturesOptionContractReadModel? original)
    {
        await ready;
        var editor = options ?? throw new InvalidOperationException("Options editor required");
        await Run(editor.LoadOperation);
        if (!draft.IsValid || draft.SchemaVersion != 1 || draft.ReviewState != ReferenceReviewState.Draft) throw new ArgumentException("A valid provider draft is required");
        if (original is null) { editor.PrepareAdd(draft); await Run(editor.AddOperation); }
        else { editor.PrepareChange(original.ContractId, draft); await Run(editor.ChangeOperation); }
    }
    async Task Run(IAsyncOperation operation)
    {
        if(!operation.CanExecute)throw new InvalidOperationException("The existing contract operation is unavailable");
        var pending=operation.ExecuteAsync(stopping.Token);
        try{await pending.WaitAsync(TimeSpan.FromSeconds(30),stopping.Token);}
        catch(TimeoutException){operation.Cancel();try{await pending;}catch(OperationCanceledException){}throw new EditorOutcomeUnknownException("Completion was not observed. Reload contracts before retrying.");}
    }
    public void Dispose()
    {
        stopping.Cancel();
        futures?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        options?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        scope.DisposeAsync().AsTask().GetAwaiter().GetResult();stopping.Dispose();
    }
}
