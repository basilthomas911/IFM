using System.Globalization;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.UI.Net.ViewModels.Portfolio;

namespace TomasAI.IFM.UI.Rust.Bridge.Net;

sealed partial class PortfolioSession
{
    FundFinancialViewModel? financial;
    FinancialTransactionRow? related;
    LedgerPostingReceipt? relatedReceipt;
    FinancialRead<FinancialPostingConfiguration>? posting;
    FinancialRead<FinancialLedgerConfiguration>? ledger;
    FinancialRead<FinancialAuthorityDraft>? authority;
    LedgerConfigurationRequest? bookDraft;
    int? accountVersionId;
    Guid? ruleVersionId;
    PendingFinancialOperation? pendingPosting;
    PendingFinancialConfiguration? pendingConfig;
    readonly IPendingFinancialOperationStore postingStore=PendingFinancialOperationStore.ForCurrentUser();
    readonly IPendingFinancialConfigurationStore configStore=PendingFinancialConfigurationStore.ForCurrentUser();
    FinancialReadScope FinancialScope => new() { PortfolioId=Portfolio.PortfolioId,FundId=Fund.FundId,Access=new(Environment.UserName,["LedgerRead","LedgerPost","LedgerReverse","LedgerConfigure","LedgerPeriodReopen","LedgerImport"],[Portfolio.PortfolioId]) };
    FinancialReadScope LedgerScope => FinancialScope with {FundId=null};
    static DateOnly Date(ContractEditorIntent input,string key)=>DateOnly.Parse(F(input,key),CultureInfo.InvariantCulture);
    void PutDialog(PortfolioDialog dialog) { var index=dialogs.FindIndex(d=>d.Key==dialog.Key);if(index<0)dialogs.Add(dialog);else dialogs[index]=dialog; }
    async Task OpenFinancials()
    {
        financial??=new(services.PortfolioFinancial);
        await financial.LoadAsync(FinancialScope,stopping.Token);
        related=financial.Current.Transactions?.Value?.Items.FirstOrDefault();relatedReceipt=null;
        if(related?.JournalId is >0)await financial.LoadJournalAsync(related.JournalId.Value,stopping.Token);
        await UpdateFinancials();
    }
    async Task UpdateFinancials()
    {
        var v=financial!.Current;
        var pending=await postingStore.ListAsync(Portfolio.PortfolioId,Fund.FundId,stopping.Token);
        var grids=new[] {Grid("balances",v.Balances?.Value?.Accounts??[]),
            Grid("transactions",v.Transactions?.Value?.Items.Select(x=>new {x.TransactionId,x.Transaction.AccountingDate,Type=x.Transaction.TransactionKind,x.Transaction.Amount,x.Transaction.Currency,x.Transaction.Description,x.JournalId,x.OperationId}).ToArray()??[],identity:o=>BridgeDocument.Fields(o)["OperationId"]),
            Grid("reservations",v.Reservations?.Value?.Items.Select(x=>new {x.OriginalReceipt.ReservationId,x.OriginalReceipt.OrderId,x.Current.Status,Units=x.Current.StrategyUnits,Filled=x.Current.FilledUnits,Closed=x.Current.ClosedUnits,Cancelled=x.Current.CancelledUnits,Remaining=x.Current.RemainingUnits,x.Current.Version}).ToArray()??[]),
            Grid("journal",v.Journal?.Value?.Entries??[]),Grid("pending-postings",pending.Select(x=>new {OperationId=x.Request.OperationId,Type=x.Request.Body.TransactionKind,x.Request.Body.Amount,x.Phase,x.Message}),identity:o=>BridgeDocument.Fields(o)["OperationId"]) };
        var financialDialog=new PortfolioDialog("financials",$"{Fund.Name} — Financials",1150,700,170,
            [Field("cash","Available Cash:",v.Balances?.Value is {} b?$"{b.AvailableCash:N2} USD":"unavailable","label",false),
             Field("pendingWithdrawals","Pending Withdrawals:",v.Balances?.Value is {} balances?$"{balances.PendingWithdrawals:N2} USD":"unavailable","label",false),
             Field("financialState","",v.Balances?.Value?.OperatingState??"Unavailable","label",false)],
            ["Next transactions","Next reservations","Transaction","Ledger","Close"],v.Message,grids,
            new(){["Next transactions"]=!v.Loading&&v.Transactions?.Value?.NextCursor is not null,["Next reservations"]=!v.Loading&&v.Reservations?.Value?.NextCursor is not null,["Transaction"]=!v.Loading&&v.Balances?.Value is not null,["Ledger"]=!v.Loading});
        PutDialog(financialDialog);
    }
    async Task OpenPosting(DateOnly? date=null,Dictionary<string,string>? keep=null)
    {
        var day=date??DateOnly.FromDateTime(Eastern(DateTime.UtcNow));
        var response=await services.PortfolioFinancial.GetFinancialPostingConfigurationAsync(FinancialScope,new(day),stopping.Token);
        if(!response.Success||response.Value?.Value is null)throw new InvalidOperationException(response.ErrorMessage??"Posting configuration unavailable.");
        posting=response.Value;
        if(related is not null) { var receipt=await services.PortfolioFinancial.GetPostingReceiptAsync(FinancialScope,new(related.OperationId),stopping.Token);relatedReceipt=receipt.Value?.Value?.Posting?.Receipt; }
        var rules=posting.Value.Rules.Where(r=>r.Kind is LedgerTransactionKind.DepositConfirmed or LedgerTransactionKind.WithdrawalRequested
            || r.Kind==LedgerTransactionKind.OpeningBalance&&posting.Value.AllowDevelopmentOpeningCapital
            || related?.JournalId is >0&&r.Kind==LedgerTransactionKind.Reversal
            || related?.Transaction.TransactionKind==LedgerTransactionKind.WithdrawalRequested&&r.Kind is LedgerTransactionKind.WithdrawalSettled or LedgerTransactionKind.WithdrawalCancelled).ToArray();
        var choices=rules.Select(r=>new PortfolioChoice(r.RuleId.ToString(),$"{PostingLabel(r.Kind)} (version {r.Version})")).ToArray();
        var fields=new[] {Field("rule","Transaction",choices.FirstOrDefault()?.Value??"","choice",choices:choices),Field("amount","Amount (USD)",related is null?.01m:Math.Abs(related.Transaction.Amount),"number",min:.01m,max:1000000000),Field("accountingDate","Accounting date",day.ToString("yyyy-MM-dd"),"date"),Field("valueDate","Value date",day.ToString("yyyy-MM-dd"),"date"),Field("description","Description","","multiline"),Field("reference","Movement reference",""),Field("confirmed","I confirm this cash movement",false,"check"),Field("original","Original transaction",related is null?"None selected":$"Transaction {related.TransactionId}; journal {related.JournalId}",enabled:false)};
        if(keep is not null)fields=fields.Select(f=>keep.TryGetValue(f.Key,out var value)?f with {Value=value}:f).ToArray();
        PutDialog(new("posting","Fund Transaction",660,360,280,fields,["Post","Close"],posting.Value.PeriodOpen?"Choose a configured transaction. Generated identities are assigned when you post.":"This accounting period is closed."));
        UpdatePostingRule();
        dialogs[^1]=dialogs[^1] with {ButtonEnabled=new(){["Post"]=posting.Value.PeriodOpen&&rules.Length>0,["Close"]=true}};
    }
    static string PostingLabel(LedgerTransactionKind kind)=>kind switch {
        LedgerTransactionKind.DepositConfirmed=>"Confirmed deposit",LedgerTransactionKind.OpeningBalance=>"Development opening capital",LedgerTransactionKind.WithdrawalRequested=>"Request withdrawal",LedgerTransactionKind.WithdrawalSettled=>"Confirm selected withdrawal",LedgerTransactionKind.WithdrawalCancelled=>"Cancel selected withdrawal",LedgerTransactionKind.Reversal=>"Reverse selected journal",_=>kind.ToString()
    };
    void UpdatePostingRule()
    {
        var d=dialogs.LastOrDefault(d=>d.Key=="posting");if(d is null)return;
        var ruleId=d.Fields.Single(f=>f.Key=="rule").Value;
        var opening=posting?.Value?.Rules.SingleOrDefault(r=>r.RuleId.ToString()==ruleId)?.Kind==LedgerTransactionKind.OpeningBalance;
        PutDialog(d with {Fields=d.Fields.Select(f=>f.Key=="confirmed"?f with {Caption=opening?"Development capital; no external movement":"I confirm this cash movement",Value=opening?"false":f.Value,Enabled=!opening}:f.Key=="reference"?f with {Value=opening?"":f.Value,Enabled=!opening}:f).ToArray()});
    }
    async Task OpenLedger()
    {
        var response=await services.PortfolioFinancial.GetFinancialLedgerConfigurationAsync(LedgerScope,new(),stopping.Token);
        if(!response.Success||response.Value is null)throw new InvalidOperationException(response.ErrorMessage);
        ledger=response.Value;var c=ledger.Value;
        var pending=await configStore.ListAsync(Portfolio.PortfolioId,stopping.Token);
        var fields=new[] {Field("control","Action",LedgerConfigurationAction.Reconcile,"choice",choices:[new("Reconcile","Reconcile"),new("OpenPeriod","Open period"),new("ClosePeriod","Close period"),new("ReopenPeriod","Reopen period"),new("RetireAccount","Retire account"),new("RetirePostingRule","Retire rule"),new("QualifyDevelopmentBook","Qualify book"),new("RefreshAuthority","Refresh authority"),new("AddAccountVersion","Edit account"),new("AddPostingRuleVersion","Edit rule")]),
            Field("period","Period",c?.Periods.FirstOrDefault()?.PeriodId.ToString(),"choice",choices:c?.Periods.Select(p=>new PortfolioChoice(p.PeriodId.ToString(),$"{p.StartDate:d} - {p.EndDate:d} ({p.State})")).ToArray()??[]),
            Field("start","Start",DateOnly.FromDateTime(Eastern(DateTime.UtcNow)).ToString("yyyy-MM-dd"),"date"),Field("end","End",DateOnly.FromDateTime(Eastern(DateTime.UtcNow)).ToString("yyyy-MM-dd"),"date"),
            Field("account","Account",c?.Accounts.FirstOrDefault()?.Definition.AccountId,"choice",choices:c?.Accounts.Select(a=>new PortfolioChoice(a.Definition.AccountId.ToString(CultureInfo.InvariantCulture),$"{a.Definition.AccountId}: {a.Definition.Category} ({a.State})")).ToArray()??[]),
            Field("rule","Rule",c?.Rules.FirstOrDefault()?.Definition.RuleId.ToString(),"choice",choices:c?.Rules.Select(r=>new PortfolioChoice(r.Definition.RuleId.ToString(),$"{r.Definition.Kind} v{r.Definition.Version} ({r.State})")).ToArray()??[]),Field("reason","Reason","","multiline")};
        var grids=new[]{Grid("periods",c?.Periods??[]),Grid("accounts",c?.Accounts.Select(a=>new{a.Definition.AccountId,a.Definition.Version,a.Definition.Category,a.Definition.NormalSide,a.State}).ToArray()??[]),Grid("rules",c?.Rules.Select(r=>new{r.Definition.Kind,r.Definition.RuleId,r.Definition.Version,DebitAccount=r.Definition.Debit.AccountId,CreditAccount=r.Definition.Credit.AccountId,r.State,r.EffectiveFrom,r.EffectiveTo}).ToArray()??[]),Grid("reconciliation",c?.LatestReconciliation?.Differences??[]),Grid("pending-controls",pending.Select(p=>new{OperationId=p.Request.OperationId,Action=p.Request.Body.Action,p.Phase,p.Message}),identity:o=>BridgeDocument.Fields(o)["OperationId"])};
        var buttons=c is null?new[]{"Set up book","Refresh","Close"}:new[]{"Apply","Refresh","Close"};
        PutDialog(new("ledger","Portfolio ledger administration",1120,710,95,fields,buttons,c is null?"No ledger book is configured for this Portfolio.":$"Book {c.BookId} | {c.Currency} | {c.OperatingState} | Revision {ledger.FinancialRevision}\n"+(c.LatestReconciliation is {} r?$"Ledger reconciliation: debits {r.Debits:N2}, credits {r.Credits:N2}, differences {r.Differences.Length}; source cut {r.SourceCut}":"No ledger reconciliation has been recorded."),grids));
        UpdateLedgerFields();
        if(c is null) {
            var current=dialogs.Single(d=>d.Key=="ledger");
            PutDialog(current with {ButtonEnabled=new(){["Set up book"]=!pending.Any(p=>p.Phase is PendingFinancialPhase.Prepared or PendingFinancialPhase.OutcomeUnknown),["Refresh"]=true,["Close"]=true}});
        }
        if(pendingConfig?.Phase is PendingFinancialPhase.Prepared or PendingFinancialPhase.OutcomeUnknown) {
            var d=dialogs.Single(d=>d.Key=="ledger");PutDialog(d with {Fields=d.Fields.Select(f=>f with {Enabled=false}).ToArray(),Buttons=["Check outcome","Refresh","Close"],Status=pendingConfig.Message});
        }
    }
    void UpdateLedgerFields()
    {
        var d=dialogs.Single(d=>d.Key=="ledger");var control=d.Fields.Single(f=>f.Key=="control").Value;
        PutDialog(d with {Fields=d.Fields.Select(f=>f with {Enabled=ledger?.Value is not null && f.Key switch {"period"=>control is "ClosePeriod" or "ReopenPeriod","start" or "end"=>control=="OpenPeriod","account"=>control is "RetireAccount" or "AddAccountVersion","rule"=>control is "RetirePostingRule" or "AddPostingRuleVersion",_=>true}}).ToArray()});
    }
    async Task OpenFinancialEditor(string view)
    {
        switch(view) {
            case "ledger": pendingConfig=null;await OpenLedger();break;
            case "posting": pendingPosting=null;await OpenPosting();break;
            case "setup": {
                bookDraft=null;pendingConfig=null;
                var result=await services.PortfolioFinancial.PrepareFinancialBookAsync(LedgerScope,new(),stopping.Token);
                if(!result.Success||result.Value?.Value is not {} setup)throw new InvalidOperationException(result.ErrorMessage);
                var today=Eastern(DateTime.UtcNow);
                PutDialog(new("setup","Development ledger setup",850,660,145,[Field("account","Execution account",setup.ExecutionAccounts.FirstOrDefault(),"choice",choices:setup.ExecutionAccounts.Select(a=>new PortfolioChoice(a,a)).ToArray()),Field("start","Period start",new DateOnly(today.Year,1,1).ToString("yyyy-MM-dd"),"date"),Field("end","Period end",new DateOnly(today.Year,12,31).ToString("yyyy-MM-dd"),"date"),Field("reason","Reason","","multiline"),Field("review","",$"Funds: {string.Join(", ",setup.FundNames)}\nPrepare to review generated accounts and posting rules. No capital is added and spending stays disabled.","review",false)],["Prepare","Save","Close"])); break;
            }
            case "authority": pendingConfig=null;authority=null;PutDialog(new("authority","Portfolio financial authority",900,660,145,[Field("permit","Permit new spending when all authority is ready",false,"check"),Field("reason","Reason","","multiline"),Field("review","","","review",false)],["Prepare","Save","Close"],"Prepare to review current policy, mandate, envelope and deployment limits."));break;
        }
        if(dialogs.LastOrDefault() is {} current && current.Key is "setup" or "authority") {
            PutDialog(current with {ButtonEnabled=new(){["Save"]=false,["Prepare"]=current.Key=="authority"||current.Fields.First(f=>f.Key=="account").Choices?.Length>0,["Close"]=true}});
        }
    }
    async Task ChooseFinancial(ContractEditorIntent input)
    {
        if(dialogs.Count==0)return; var d=dialogs[^1];
        d=d with {Fields=d.Fields.Select(f=>input.Fields.TryGetValue(f.Key,out var v)?f with {Value=v}:f).ToArray()};PutDialog(d);
        if(d.Key=="risk-history") {
            riskDate=Date(input,"date");riskCursor=null;
            PutDialog(d with {Grids=[new("risk-history",["EvaluatedAtUtc","Horizon","Variant","Outcome","Units","Reason","OrderId","WorkflowId","InvocationId"],[])],Fields=d.Fields.Select(f=>f.Key is "decision" or "evidence"?f with {Value=""}:f).ToArray(),ButtonEnabled=new(){["Refresh"]=true,["Next page"]=false}});
        }else if(d.Key=="posting") {
            if(pendingPosting is not null)return;
            if(posting?.Value?.AccountingDate!=Date(input,"accountingDate"))await OpenPosting(Date(input,"accountingDate"),input.Fields);
            UpdatePostingRule();
        }else if(d.Key=="ledger")UpdateLedgerFields();
        else if(d.Key=="setup") {bookDraft=null;PutDialog(d with {ButtonEnabled=new(){["Save"]=false,["Prepare"]=true,["Close"]=true}});}
        else if(d.Key=="authority") {authority=null;PutDialog(d with {Fields=d.Fields.Select(f=>f.Key=="review"?f with {Value=""}:f).ToArray(),ButtonEnabled=new(){["Save"]=false,["Prepare"]=true,["Close"]=true}});}
    }
    async Task FinancialAction(ContractEditorIntent input)
    {
        refreshParent="";
        var action=F(input,"action");var key=dialogs.LastOrDefault()?.Key;
        if(key=="risk-history") {riskDate=Date(input,"date");await OpenRiskHistory(action=="Next page");return;}
        if(action=="Close") {dialogs.RemoveAt(dialogs.Count-1);if(dialogs.LastOrDefault()?.Key=="financials")await UpdateFinancials();return;}
        if(key=="financials") {
            switch(action) {
                case "Refresh":await OpenFinancials();return;
                case "Next transactions":await financial!.NextTransactionsAsync(stopping.Token);await UpdateFinancials();return;
                case "Next reservations":await financial!.NextReservationsAsync(stopping.Token);await UpdateFinancials();return;
                case "Transaction":pendingPosting=null;await OpenPosting();return;
                case "Ledger":pendingConfig=null;await OpenLedger();return;
            }
        }
        if(key=="ledger") {
            if(action=="Refresh"){await OpenLedger();return;}
            if(action=="Set up book"){await OpenFinancialEditor("setup");return;}
            if(action is "Apply" or "Check outcome") {
                if(pendingConfig?.Phase is not(PendingFinancialPhase.Prepared or PendingFinancialPhase.OutcomeUnknown)) {
                    var control=Enum.Parse<LedgerConfigurationAction>(F(input,"control"));
                    if(control==LedgerConfigurationAction.RefreshAuthority){await OpenFinancialEditor("authority");return;}
                    if(control is LedgerConfigurationAction.AddAccountVersion or LedgerConfigurationAction.AddPostingRuleVersion){OpenLedgerVersion(input,control);return;}
                    var request=FinancialControlPreparation.Create(LedgerScope,ledger!,control,F(input,"reason"),DateTime.UtcNow,Guid.TryParse(F(input,"period"),out var period)?period:null,Date(input,"start"),Date(input,"end"),int.TryParse(F(input,"account"),out var account)?account:null,Guid.TryParse(F(input,"rule"),out var rule)?rule:null);
                    pendingConfig=new(request,PendingFinancialPhase.Prepared,"Prepared.");
                }
                await SubmitConfiguration();await OpenLedger();return;
            }
        }
        if(action=="Prepare") {
            if(key=="setup") {
                var response=await services.PortfolioFinancial.PrepareFinancialBookAsync(LedgerScope,new(F(input,"account"),Date(input,"start"),Date(input,"end")),stopping.Token);
                if(!response.Success||response.Value?.Value?.Draft is not {} draft)throw new InvalidOperationException(response.ErrorMessage);
                bookDraft=draft;SetReview($"Generated book {draft.BookId} | {draft.Book!.Currency} | Development\n"+string.Join("\n",draft.Accounts.Select(a=>$"Account {a.AccountId}: {a.Category}, v{a.Version}"))+"\n\nRules: "+string.Join(", ",draft.Rules.Select(r=>r.Kind))+"\n\nThis creates configuration only. Opening capital and qualification are separate operations.");
            }else if(key=="authority") {
                var response=await services.PortfolioFinancial.PrepareFinancialAuthorityAsync(LedgerScope,new(B(input,"permit")),stopping.Token);
                if(!response.Success||response.Value?.Value is null)throw new InvalidOperationException(response.ErrorMessage);
                authority=response.Value;
                var lines=new List<string>{ $"Financial revision {authority.FinancialRevision}",string.Join(Environment.NewLine,authority.Value.Notes),"" };
                foreach(var fund in authority.Value.Draft.Book!.Funds) {
                    lines.Add($"Fund {fund.FundId}: {(fund.CanSpend?"New spending permitted":"New spending disabled")}");
                    foreach(var deployment in fund.Deployments) {
                        lines.Add($"Deployment {deployment.Reference.DeploymentKey}: per-trade loss {deployment.MaximumRiskPerTrade:N2} USD; valid until {deployment.Reference.ValidUntilUtc:O}");
                        lines.AddRange(deployment.Limits.Select(limit=>$"  {limit.Measure}: {limit.Maximum:N2} {limit.Unit}"));
                    }
                    lines.AddRange(fund.Limits.Select(limit=>$"{limit.ScopeKind} {limit.ScopeKey} / {limit.Measure}: {(limit.Enabled?limit.Maximum.ToString("N2"):"Disabled")} {limit.Unit}"));
                }
                SetReview(string.Join(Environment.NewLine,lines));
            }
            return;
        }
        if(action is "Post" or "Check outcome" or "Save" or "Review and save")await SaveFinancial(input);
    }
    void SetReview(string text) {dialogs[^1]=dialogs[^1] with {ButtonEnabled=new(){["Save"]=true,["Prepare"]=true,["Close"]=true},Fields=dialogs[^1].Fields.Select(f=>f.Key=="review"?f with {Value=text}:f).ToArray(),Status="Review the prepared configuration and enter an audit reason before saving."};}
    async Task SubmitConfiguration()
    {
        var op=new FinancialConfigurationOperation(services.PortfolioFinancial,configStore);
        pendingConfig=pendingConfig!.Phase==PendingFinancialPhase.Prepared?await op.SubmitAsync(pendingConfig.Request,stopping.Token):await op.RecoverAsync(pendingConfig.Request.OperationId,stopping.Token);
        SetPending(pendingConfig.Message,pendingConfig.Phase);
    }
    void SetPending(string status,PendingFinancialPhase phase)
    {
        var title=dialogs[^1].Key=="posting"?"Check outcome":phase switch {PendingFinancialPhase.Committed=>"Saved",PendingFinancialPhase.ExpiredWithoutPosting=>"Expired",_=>"Check outcome"};
        dialogs[^1]=dialogs[^1] with {Status=status,ButtonEnabled=new(){[title]=phase is PendingFinancialPhase.Prepared or PendingFinancialPhase.OutcomeUnknown,["Close"]=true},Fields=dialogs[^1].Fields.Select(f=>f with {Enabled=false}).ToArray(),Buttons=dialogs[^1].Buttons.Select(b=>b is "Post" or "Save" or "Review and save" or "Check outcome" or "Saved" or "Expired"?title:b).ToArray()};
    }
    async Task SaveFinancial(ContractEditorIntent input)
    {
        var key=dialogs[^1].Key;
        if(key=="posting") {
            var op=new FinancialPostingOperation(services.PortfolioFinancial,postingStore);
            if(pendingPosting is null) {
                var rule=posting!.Value!.Rules.Single(r=>r.RuleId.ToString()==F(input,"rule"));
                var request=FinancialPostingPreparation.Create(FinancialScope,posting,rule,new(rule.Kind,N(input,"amount"),Date(input,"accountingDate"),Date(input,"valueDate"),F(input,"description"),F(input,"reference"),B(input,"confirmed")),related,relatedReceipt,DateTime.UtcNow);
                pendingPosting=new(request,PendingFinancialPhase.Prepared,"Prepared.");pendingPosting=await op.SubmitAsync(request,stopping.Token);
            }else pendingPosting=await op.RecoverAsync(pendingPosting.Request.OperationId,stopping.Token);
            SetPending(pendingPosting.Message,pendingPosting.Phase);return;
        }
        if(pendingConfig is null) {
            ConfigureLedgerCommand request=key switch {
                "setup"=>FinancialControlPreparation.CreateBook(LedgerScope,bookDraft??throw new InvalidOperationException("Prepare the book first."),F(input,"reason"),DateTime.UtcNow),
                "authority"=>FinancialControlPreparation.RefreshAuthority(LedgerScope,authority??throw new InvalidOperationException("Prepare the authority first."),F(input,"reason"),DateTime.UtcNow),
                "account-version"=>FinancialControlPreparation.EditAccount(LedgerScope,ledger!,accountVersionId??throw new InvalidOperationException("No account version is selected."),Enum.Parse<PostingSide>(F(input,"normal")),B(input,"dimension"),F(input,"reason"),DateTime.UtcNow),
                "rule-version"=>FinancialControlPreparation.EditRule(LedgerScope,ledger!,ruleVersionId??throw new InvalidOperationException("No rule version is selected."),Binding(F(input,"debit"))!,Binding(F(input,"credit"))!,B(input,"confirmed"),Binding(F(input,"asset")),Binding(F(input,"pnl")),Date(input,"effective"),F(input,"reason"),DateTime.UtcNow),
                _=>throw new InvalidOperationException("No financial draft is open.")
            };
            pendingConfig=new(request,PendingFinancialPhase.Prepared,"Prepared configuration.");
        }
        await SubmitConfiguration();
        if(key is "account-version" or "rule-version") {
            dialogs.RemoveAt(dialogs.Count-1);await OpenLedger();
            if(pendingConfig is not null)dialogs[^1]=dialogs[^1] with {Status=pendingConfig.Message};
        }
    }
    LedgerAccountBinding? Binding(string text) {
        if(string.IsNullOrEmpty(text))return null;var pair=text.Split('@');
        return new(int.Parse(pair[0],CultureInfo.InvariantCulture),long.Parse(pair[1],CultureInfo.InvariantCulture));
    }
    void OpenLedgerVersion(ContractEditorIntent input,LedgerConfigurationAction action)
    {
        var c=ledger!.Value!;pendingConfig=null;
        var choices=c.Accounts.Where(a=>a.State=="Active").Select(a=>new PortfolioChoice($"{a.Definition.AccountId}@{a.Definition.Version}",$"{a.Definition.Category} / {a.Definition.AccountId} v{a.Definition.Version}")).ToArray();
        if(action==LedgerConfigurationAction.AddAccountVersion) {
            var a=c.Accounts.Single(a=>a.Definition.AccountId.ToString(CultureInfo.InvariantCulture)==F(input,"account"));
            accountVersionId=a.Definition.AccountId;
            Editor("account-version","Edit ledger account",670,340,150,[Field("id","Version",$"{a.Definition.Category}: {a.Definition.Version} ? {a.Definition.Version+1}","label",false),Field("normal","Normal side",a.Definition.NormalSide,"choice",choices:[new("Debit","Debit"),new("Credit","Credit")]),Field("dimension","Require Fund dimension",a.Definition.FundDimensionRequired,"check"),Field("reason","Audit reason","","multiline")],"Review and save");
        }else {
            var r=c.Rules.Single(r=>r.Definition.RuleId.ToString()==F(input,"rule"));string Key(LedgerAccountBinding? b)=>b is null?"":$"{b.AccountId}@{b.Version}";
            ruleVersionId=r.Definition.RuleId;
            Editor("rule-version","Edit posting rule",670,480,150,[Field("id","Version",$"{r.Definition.Kind}: {r.Definition.Version} ? {r.Definition.Version+1}","label",false),Field("debit","Debit",Key(r.Definition.Debit),"choice",choices:choices),Field("credit","Credit",Key(r.Definition.Credit),"choice",choices:choices),Field("asset","Valuation asset",Key(r.Definition.ValuationAsset),"choice",choices:[new("","None"),..choices]),Field("pnl","Unrealized P&L",Key(r.Definition.UnrealizedPnl),"choice",choices:[new("","None"),..choices]),Field("confirmed","Require confirmed movement",r.Definition.RequiresConfirmedMovement,"check"),Field("effective","Effective from",DateOnly.FromDateTime(Eastern(DateTime.UtcNow)).ToString("yyyy-MM-dd"),"date"),Field("reason","Audit reason","","multiline")],"Review and save");
        }
    }
    async Task FinancialGrid(ContractEditorIntent input)
    {
        switch(F(input,"grid")) {
            case "transactions":
                related=financial!.Current.Transactions!.Value!.Items.Single(t=>t.OperationId.ToString()==input.OriginalId);
                if(related.JournalId is >0)await financial.LoadJournalAsync(related.JournalId.Value,stopping.Token);
                await UpdateFinancials();break;
            case "pending-postings": {
                pendingPosting=await postingStore.LoadAsync(Guid.Parse(input.OriginalId!),stopping.Token)??throw new InvalidOperationException("Pending operation was not found.");
                var b=pendingPosting.Request.Body;
                PutDialog(new("posting","Fund Transaction",660,360,280,[Field("rule","Transaction",$"{b.TransactionKind} (version {b.PostingRule.Version})",enabled:false),Field("amount","Amount (USD)",b.Amount,"number",false),Field("accountingDate","Accounting date",b.AccountingDate.ToString("yyyy-MM-dd"),"date",false),Field("valueDate","Value date",b.ValueDate.ToString("yyyy-MM-dd"),"date",false),Field("description","Description",b.Description,"multiline",false),Field("reference","Movement reference",b.MovementEvidence.SourceReference,enabled:false),Field("confirmed","I confirm this cash movement",b.MovementEvidence.Status==MovementStatus.Confirmed,"check",false),Field("original","Original transaction",b.RelatedJournalId?.ToString()??b.RelatedObligationId?.ToString()??"None selected",enabled:false)],["Check outcome","Close"],pendingPosting.Message));
                dialogs[^1]=dialogs[^1] with {Fields=dialogs[^1].Fields.Select(f=>f with {Value=f.Key switch {"amount"=>b.Amount.ToString(CultureInfo.InvariantCulture),"accountingDate"=>b.AccountingDate.ToString("yyyy-MM-dd"),"valueDate"=>b.ValueDate.ToString("yyyy-MM-dd"),"description"=>b.Description,"reference"=>b.MovementEvidence.SourceReference,"confirmed"=>(b.MovementEvidence.Status==MovementStatus.Confirmed).ToString(),_=>f.Value}}).ToArray()};SetPending(pendingPosting.Message,pendingPosting.Phase);break;
            }
            case "pending-controls":
                pendingConfig=await configStore.LoadAsync(Guid.Parse(input.OriginalId!),stopping.Token)??throw new InvalidOperationException("Pending control was not found.");
                await SubmitConfiguration();await OpenLedger();break;
        }
    }
}
