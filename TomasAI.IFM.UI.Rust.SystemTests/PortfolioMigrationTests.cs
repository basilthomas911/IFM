using System.Text.Json;
using NSubstitute;
using TomasAI.IFM.UI.Net.ViewModels.Portfolio;
using TomasAI.IFM.UI.Net.Views.Portfolio;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.Lookups;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Services;
using TomasAI.IFM.UI.Rust.Bridge.Net;

public sealed class PortfolioMigrationTests
{
    static readonly string Output=Path.Combine(FindRoot(),".artifacts/portfolio-migration");
    static string FindRoot(){var d=new DirectoryInfo(AppContext.BaseDirectory);while(d is not null&&!File.Exists(Path.Combine(d.FullName,"TomasAI.IFM.sln")))d=d.Parent;return d!.FullName;}
    static ContractEditorIntent Intent(string action,Dictionary<string,string>? fields=null,string? id=null)=>new(1,Guid.NewGuid(),"portfolio",action,1,id,fields??[]);
    static PortfolioDocument Document(ContractEditorResult result)=>JsonSerializer.Deserialize<PortfolioDocument>(MessagePack.MessagePackSerializer.ConvertToJson(result.Documents["portfolio"]),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!;
    [Theory]
    [InlineData("new-portfolio","Create Portfolio",640,260,6)]
    [InlineData("version-portfolio","Create Portfolio Version",640,260,6)]
    [InlineData("delete-portfolio","Delete Draft Portfolio",660,320,3)]
    [InlineData("portfolio-state","Change Portfolio State",520,150,2)]
    [InlineData("fund-state","Change Fund State",520,150,2)]
    [InlineData("new-fund","Create Fund",780,560,11)]
    [InlineData("version-fund","Change Fund",780,560,11)]
    [InlineData("risk-history","Risk history - Portfolio 101, Fund 701",1300,850,3)]
    [InlineData("allocation","Fund Allocation",760,520,6)]
    [InlineData("envelope","Fund Risk Envelope",760,720,12)]
    [InlineData("assignment","Trade Template Assignment",760,720,12)]
    [InlineData("risk-policy","Portfolio Risk Policy",1260,820,11)]
    [InlineData("financials","Fixture Fund — Financials",1150,700,3)]
    [InlineData("ledger","Portfolio ledger administration",1120,710,7)]
    [InlineData("posting","Fund Transaction",660,360,8)]
    [InlineData("setup","Development ledger setup",850,660,5)]
    [InlineData("authority","Portfolio financial authority",900,660,3)]
    public async Task Every_editor_has_original_dimensions_and_controls_and_exports_real_wire_document(string view,string title,int width,int height,int fields)
    {
        var services=Services(view=="delete-portfolio"?PortfolioOperatingState.Draft:PortfolioOperatingState.Active);using var session=new PortfolioSession(services);
        var initial=await session.ExecuteAsync(Intent("load"));
        Assert.Equal(6,Document(initial).Grids.Length);
        var result=await session.ExecuteAsync(Intent("portfolio-open",new(){["view"]=view}));
        var document=Document(result);var dialog=Assert.Single(document.Dialogs);
        Assert.Equal(title,dialog.Title);Assert.Equal(width,dialog.Width);Assert.Equal(height,dialog.Height);Assert.Equal(fields,dialog.Fields.Length);
        if(view is "new-portfolio" or "version-portfolio") {
            var currency=Assert.Single(dialog.Fields,f=>f.Key=="BaseCurrency");
            Assert.Equal("choice",currency.Kind);Assert.Equal("USD",currency.Value);
            Assert.Equal(new[]{"USD","CAD"},currency.Choices!.Select(c=>c.Value).ToArray());
            Assert.All(currency.Choices,c=>{Assert.Equal(c.Value,c.Label);Assert.True(c.Enabled);});
        }
        if(view=="posting")Assert.Equal(dialog.Status,document.Context["status"]);
        Directory.CreateDirectory(Output);
        File.WriteAllBytes(Path.Combine(Output,view+".msgpack"),result.Documents["portfolio"]);
        File.WriteAllText(Path.Combine(Output,view+".json"),JsonSerializer.Serialize(document,new JsonSerializerOptions{WriteIndented=true}));
        await OriginalInventory(view,dialog,services);
        await session.ExecuteAsync(Intent("portfolio-cancel"));
        Assert.Empty(services.PortfolioCommands.ReceivedCalls());
        Assert.Empty(services.PortfolioFundCommands.ReceivedCalls());
    }
    [Theory]
    [InlineData("AddAccountVersion","account-version","Edit ledger account",340,4)]
    [InlineData("AddPostingRuleVersion","rule-version","Edit posting rule",480,8)]
    public async Task Ledger_versions_preserve_configured_identity_and_original_controls(string action,string key,string title,int height,int fields)
    {
        var services=Services();using var session=new PortfolioSession(services);await session.ExecuteAsync(Intent("load"));
        await session.ExecuteAsync(Intent("portfolio-open",new(){["view"]="ledger"}));
        var result=await session.ExecuteAsync(Intent("portfolio-financial",new(){["action"]="Apply",["control"]=action,["account"]="401",["rule"]=RuleId.ToString()}));
        var dialog=Document(result).Dialogs.Last();Assert.Equal(key,dialog.Key);Assert.Equal(title,dialog.Title);Assert.Equal(670,dialog.Width);Assert.Equal(height,dialog.Height);Assert.Equal(fields,dialog.Fields.Length);
        Directory.CreateDirectory(Output);File.WriteAllBytes(Path.Combine(Output,key+".msgpack"),result.Documents["portfolio"]);
        Assert.DoesNotContain(services.PortfolioFinancial.ReceivedCalls(),c=>c.GetMethodInfo().Name=="ConfigureAsync");
    }
    [Fact]
    public async Task Funds_tabs_embed_selected_views_and_keep_nested_financial_dialogs()
    {
        var services=Services();using var session=new PortfolioSession(services);
        await session.ExecuteAsync(Intent("load"));
        var document=Document(await session.ExecuteAsync(Intent("portfolio-fund-view",new(){["view"]="financials"})));
        Assert.Empty(document.Dialogs);Assert.NotNull(document.FundView);
        Assert.Equal("financials",document.FundView.Key);
        Assert.Contains(document.FundView.Grids!,g=>g.Key=="balances");
        Assert.DoesNotContain("Refresh",document.FundView.Buttons);
        Assert.Contains(document.FundView.Fields,f=>f.Key=="cash"&&f.Caption=="Available Cash:"&&f.Value=="10,000.00 USD");
        Assert.Contains(document.FundView.Fields,f=>f.Key=="pendingWithdrawals"&&f.Caption=="Pending Withdrawals:"&&f.Value=="0.00 USD");

        var ledger=Document(await session.ExecuteAsync(Intent("portfolio-financial",new(){["action"]="Ledger"})));
        Assert.Equal("financials",ledger.FundView!.Key);
        Assert.Equal("ledger",Assert.Single(ledger.Dialogs).Key);
        var closed=Document(await session.ExecuteAsync(Intent("portfolio-cancel")));
        Assert.Empty(closed.Dialogs);Assert.Equal("financials",closed.FundView!.Key);
        Assert.Equal("financials",closed.Context["refreshParent"]);
        await session.ExecuteAsync(Intent("portfolio-financial",new(){["action"]="Refresh"}));

        var risk=Document(await session.ExecuteAsync(Intent("portfolio-fund-view",new(){["view"]="risk-history"})));
        Assert.Empty(risk.Dialogs);Assert.Equal("risk-history",risk.FundView!.Key);
        Assert.Contains(risk.FundView.Fields,f=>f.Key=="date");
        Assert.Contains(risk.FundView.Grids!,g=>g.Key=="risk-history");
        var refreshed=Document(await session.ExecuteAsync(Intent("portfolio-financial",
            new(){["action"]="Refresh",["date"]=risk.FundView.Fields.Single(f=>f.Key=="date").Value})));
        Assert.Equal("risk-history",refreshed.FundView!.Key);
        Assert.Empty(refreshed.Dialogs);

        var empty=Document(await session.ExecuteAsync(Intent("portfolio-fund-filter",new(){["fundFilter"]="Paused"})));
        Assert.Null(empty.FundView);Assert.Empty(empty.Dialogs);
        Assert.Empty(services.PortfolioCommands.ReceivedCalls());
        Assert.Empty(services.PortfolioFundCommands.ReceivedCalls());
    }

    [Fact]
    public async Task Embedded_fund_views_reload_for_a_new_selection_and_keep_editors_modal()
    {
        var services=Services();
        var first=new FundMandateReadModel{PortfolioId=101,FundId=701,FundMandateVersion=1,Name="First Fund",OperatingState=FundOperatingState.Active};
        var second=first with{FundId=702,Name="Second Fund"};
        services.PortfolioQueries.GetFundRevisionAsync(101,702,Arg.Any<CancellationToken>()).Returns(new ServiceOk<PortfolioAggregateRevision>(new(){PortfolioId=101,FundId=702,Revision=20}));
        services.PortfolioQueries.GetFundAllocationAsync(101,702,Arg.Any<CancellationToken>()).Returns(new ServiceOk<FundAllocationReadModel>(new(){Currency="USD",MaximumWeight=1}));
        services.PortfolioQueries.GetFundRiskEnvelopeAsync(101,702,Arg.Any<DateTime>(),Arg.Any<CancellationToken>()).Returns(new ServiceOk<FundRiskEnvelopeReadModel>(new(){Currency="USD",CapacityState=FundCapacityState.Available}));
        services.PortfolioQueries.GetAssignmentsAsync(101,702,1,Arg.Any<CancellationToken>()).Returns(new ServiceOk<FundTradeTemplateAssignmentReadModel[]>([]));
        services.PortfolioQueries.GetFundsAsync(101,Arg.Any<FundOperatingState?>(),Arg.Any<int>(),Arg.Any<string?>(),Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<PortfolioPage<FundMandateReadModel>>(new(){Items=[first,second]}));
        using var session=new PortfolioSession(services);await session.ExecuteAsync(Intent("load"));
        await session.ExecuteAsync(Intent("portfolio-fund-view",new(){["view"]="financials"}));
        var selected=Document(await session.ExecuteAsync(Intent("portfolio-fund",id:JsonSerializer.Serialize(new[]{"Second Fund","1","Active"}))));
        Assert.Equal("Second Fund — Financials",selected.FundView!.Title);
        Assert.Empty(selected.Dialogs);
        var editor=Document(await session.ExecuteAsync(Intent("portfolio-open",new(){["view"]="fund-state"})));
        Assert.Equal("fund-state",Assert.Single(editor.Dialogs).Key);
        Assert.Equal("financials",editor.FundView!.Key);
        var canceled=Document(await session.ExecuteAsync(Intent("portfolio-cancel")));
        Assert.Empty(canceled.Dialogs);Assert.Equal("Second Fund — Financials",canceled.FundView!.Title);
        Assert.True(PortfolioSession.Validate(Intent("portfolio-fund-view",new(){["view"]="risk-history"})));
    }

    static readonly Guid RuleId=Guid.Parse("b63fbbbe-fffe-4000-8000-000000000001");
    static Form SourceState<T>(string title,T current)where T:struct,Enum
    {
        var type=typeof(PortfolioAdministrationForm).Assembly.GetType("TomasAI.IFM.UI.Net.Views.Portfolio.StateReasonDialog`1")!.MakeGenericType(typeof(T));
        return (Form)Activator.CreateInstance(type,title,current)!;
    }
    static async Task OriginalInventory(string view,PortfolioDialog dialog,IUiServiceCatalog services)
    {
        var done=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread=new Thread(()=>{try{
            var p=new PortfolioReadModel{PortfolioId=101,PortfolioVersion=1,Name="Fixture Portfolio",BaseCurrency="USD",OperatingState=PortfolioOperatingState.Active,EffectiveFromUtc=DateTime.UtcNow};
            var f=new FundMandateReadModel{PortfolioId=101,FundId=701,FundMandateVersion=1,Name="Fixture Fund",DecisionHorizon="Daily",TradingYear=2026};
            var financial=new FinancialReadScope{PortfolioId=101,FundId=701,Access=new("fixture",["LedgerRead","LedgerConfigure"])};
            using Form? original=view switch {
                "new-portfolio"=>new PortfolioEditorForm(901),"version-portfolio"=>new PortfolioEditorForm(101,p),
                "portfolio-state"=>SourceState("Change Portfolio State",PortfolioOperatingState.Active),"fund-state"=>SourceState("Change Fund State",FundOperatingState.Draft),
                "delete-portfolio"=>new DeleteDraftPortfolioDialog(p with {OperatingState=PortfolioOperatingState.Draft}),
                "new-fund"=>new FundMandateEditorForm(101,902,catalog:[],selections:new(["ES"],[],[],[])),
                "version-fund"=>new FundMandateEditorForm(101,701,f,[],new(["ES"],[],[],[])),
                "allocation"=>new FundAllocationEditorForm(p,f,null),"envelope"=>new FundRiskEnvelopeEditorForm(p,f,null),
                "assignment"=>new FundAssignmentEditorForm(p,f,[]),
                "risk-policy"=>new PortfolioRiskPolicyForm(p,services.PortfolioQueries,services.PortfolioIdentities,services.PortfolioPolicyCommands,services.ReferenceQueries,true),
                "financials"=>new FundFinancialForm(services.PortfolioFinancial,financial,f.Name),
                "posting"=>new FundFinancialPostingForm(services.PortfolioFinancial,financial,PendingFinancialOperationStore.ForCurrentUser()),
                "ledger"=>new PortfolioLedgerControlForm(services.PortfolioFinancial,financial),
                "setup"=>new PortfolioLedgerSetupForm(services.PortfolioFinancial,financial,PendingFinancialConfigurationStore.ForCurrentUser()),
                "authority"=>new PortfolioFinancialAuthorityForm(services.PortfolioFinancial,financial,PendingFinancialConfigurationStore.ForCurrentUser()),
                "risk-history"=>new TomasAI.IFM.UI.Net.Views.Strategy.RiskHistoryForm(services.RiskQueries,101,701),
                _=>null
            };
            if(original is not null){
                if(view is not ("new-fund" or "version-fund" or "fund-state" or "new-portfolio" or "version-portfolio" or "portfolio-state" or "posting")){Assert.Equal(dialog.Width,original.Width);Assert.Equal(dialog.Height,original.Height);}
                static IEnumerable<Control> Children(Control c)=>c.Controls.Cast<Control>().SelectMany(child=>new[]{child}.Concat(Children(child)));
                var controls=Children(original).ToArray();var buttons=controls.OfType<Button>().Where(b=>b.AccessibleName!="Open selections").Select(b=>b.Text).ToArray();
                foreach(var button in dialog.Buttons)Assert.Contains(button,buttons);
                if(view=="financials")buttons=buttons.Where(button=>button!="Refresh").ToArray();
                if(view!="ledger")Assert.Equal(buttons.Order(StringComparer.Ordinal),dialog.Buttons.Order(StringComparer.Ordinal));
                original.ShowInTaskbar=false;original.StartPosition=FormStartPosition.Manual;original.Location=new(-3000,-3000);original.Show();System.Windows.Forms.Application.DoEvents();original.PerformLayout();
                File.WriteAllText(Path.Combine(Output,"winforms-"+view+"-controls.json"),JsonSerializer.Serialize(new {original.Text,original.Width,original.Height,Controls=controls.Select(c=>new {Type=c.GetType().Name,c.Text,c.Name,c.AccessibleName,Bounds=c.Bounds.ToString()})},new JsonSerializerOptions{WriteIndented=true}));
                using var bitmap=new Bitmap(original.Width,original.Height);original.DrawToBitmap(bitmap,new(Point.Empty,original.Size));bitmap.Save(Path.Combine(Output,"winforms-"+view+".png"));
            }
            done.SetResult();
        }catch(Exception error){done.SetException(error);}}){IsBackground=true};thread.SetApartmentState(ApartmentState.STA);thread.Start();await done.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }
    [Fact]
    public async Task Policy_discard_confirmation_keeps_editing_when_operator_selects_no()
    {
        using var session=new PortfolioSession(Services());await session.ExecuteAsync(Intent("load"));
        await session.ExecuteAsync(Intent("portfolio-open",new(){["view"]="risk-policy"}));
        await session.ExecuteAsync(Intent("portfolio-policy",new(){["action"]="New Version"}));
        var confirm=await session.ExecuteAsync(Intent("portfolio-cancel",new(){["dirty"]="true"}));
        Assert.Equal("discard-policy",Document(confirm).Dialogs.Last().Key);
        var retained=Document(await session.ExecuteAsync(Intent("portfolio-policy",new(){["action"]="No"})));
        Assert.Equal("risk-policy",Assert.Single(retained.Dialogs).Key);Assert.Equal("True",retained.Context["policyDirty"]);
        Assert.Contains(retained.Dialogs[0].Fields,f=>f.Key=="Name"&&f.Enabled);
    }

    [Fact]
    public async Task Metrics_requests_use_the_api_page_size_limit()
    {
        var services=Services();using var metrics=new FundMetricsViewModel(services.PortfolioFinancial);
        await metrics.LoadAsync(101,701,new DateOnly(2026,1,1),new DateOnly(2026,10,17));
        await services.PortfolioFinancial.Received(1).GetFundTransactionsPageAsync(
            Arg.Any<FinancialReadScope>(),Arg.Is<GetFundTransactionsPageRequest>(r=>r.PageSize==100),Arg.Any<CancellationToken>());
        Assert.NotNull(metrics.Report);
    }
    [Fact]
    public async Task Allocation_save_uses_current_revisions_and_policy_identity()
    {
        var s=Services();s.PortfolioCommands.DelegateAllocationAsync(Arg.Any<FundAllocationReadModel>(),10,Arg.Any<CancellationToken>()).Returns(new ServiceOk<Guid>(Guid.NewGuid()));
        using var session=new PortfolioSession(s);await session.ExecuteAsync(Intent("load"));
        await session.ExecuteAsync(Intent("portfolio-open",new(){["view"]="allocation"}));
        var result=await session.ExecuteAsync(Intent("portfolio-save",new(){["TargetWeight"]="0.75",["MinimumWeight"]="0",["MaximumWeight"]="1",["AllocatedCapital"]="5000",["Currency"]="usd",["SourcePolicyVersion"]="1"}));
        Assert.Empty(Document(result).Dialogs);
        await s.PortfolioCommands.Received(1).DelegateAllocationAsync(Arg.Is<FundAllocationReadModel>(v=>v.PortfolioId==101&&v.FundId==701&&v.SourcePolicyId==301&&v.SourcePolicyVersion==1&&v.TargetWeight==.75m&&v.Currency=="USD"&&v.AllocationVersion==1),10,Arg.Any<CancellationToken>());
    }
    [Fact]
    public async Task Posting_commits_exact_prepared_request_and_recovery_does_not_post_twice()
    {
        var s=Services();var api=s.PortfolioFinancial;var day=DateOnly.FromDateTime(DateTime.UtcNow);
        var rule=new LedgerPostingRule(RuleId,1,"hash",LedgerTransactionKind.DepositConfirmed,new(401,1),new(401,1),true);
        api.GetFinancialPostingConfigurationAsync(Arg.Any<FinancialReadScope>(),Arg.Any<GetFinancialPostingConfigurationRequest>(),Arg.Any<CancellationToken>()).Returns(Ok(new FinancialPostingConfiguration(301,701,new(),[rule],true,day)));
        LedgerPostingCompletedEvent? completion=null;PostFundTransactionCommand? sent=null;
        api.GetPostingReceiptAsync(Arg.Any<FinancialReadScope>(),Arg.Any<GetPostingReceiptRequest>(),Arg.Any<CancellationToken>()).Returns(_=>new ServiceOk<FinancialRead<FinancialOperationOutcome>>(new(completion is null?FinancialReadStatus.NotFound:FinancialReadStatus.Found,completion is null?null:new(){Posting=completion},5,DateTime.UtcNow)));
        api.PostAsync(Arg.Any<PostFundTransactionCommand>(),Arg.Any<CancellationToken>()).Returns(call=>{
            sent=call.Arg<PostFundTransactionCommand>();completion=new(){Id=Guid.NewGuid(),OperationId=sent.OperationId,InputHash=sent.InputSha256,Receipt=new(){OperationId=sent.OperationId,FundId=701,BookId=301}};
            return new ValueTask<ServiceResult<GuidResult>>(new ServiceOk<GuidResult>(new(sent.OperationId)));
        });
        var store=new PendingFinancialOperationStore(Path.Combine(Output,"posting-tests",Guid.NewGuid().ToString("N")));
        using var session=new PortfolioSession(s,store);await session.ExecuteAsync(Intent("load"));
        await session.ExecuteAsync(Intent("portfolio-open",new(){["view"]="posting"}));
        var values=new Dictionary<string,string>{["action"]="Post",["rule"]=RuleId.ToString(),["amount"]="125.50",["accountingDate"]=day.ToString("yyyy-MM-dd"),["valueDate"]=day.ToString("yyyy-MM-dd"),["description"]="Fixture deposit",["reference"]="Fixture confirmation",["confirmed"]="true"};
        var result=await session.ExecuteAsync(Intent("portfolio-financial",values));
        var dialog=Assert.Single(Document(result).Dialogs);Assert.StartsWith("Committed.",dialog.Status);Assert.All(dialog.Fields,f=>Assert.False(f.Enabled));
        Assert.NotNull(sent);Assert.Equal(5,sent.ExpectedFinancialRevision);Assert.Equal(125.50m,sent.Body.Amount);Assert.Equal(701,sent.Body.FundId);
        await session.ExecuteAsync(Intent("portfolio-financial",new(){["action"]="Check outcome"}));
        await api.Received(1).PostAsync(Arg.Any<PostFundTransactionCommand>(),Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Closing_ledger_returns_to_financials_then_administration()
    {
        using var session=new PortfolioSession(Services());await session.ExecuteAsync(Intent("load"));
        await session.ExecuteAsync(Intent("portfolio-open",new(){["view"]="financials"}));
        var open=await session.ExecuteAsync(Intent("portfolio-financial",new(){["action"]="Ledger"}));
        Directory.CreateDirectory(Output);File.WriteAllBytes(Path.Combine(Output,"ledger-nested.msgpack"),open.Documents["portfolio"]);
        Assert.Equal(new[]{"financials","ledger"},Document(open).Dialogs.Select(d=>d.Key));
        var closed=await session.ExecuteAsync(Intent("portfolio-cancel"));
        Assert.Equal("financials",Assert.Single(Document(closed).Dialogs).Key);
        Assert.Empty(Document(await session.ExecuteAsync(Intent("portfolio-cancel"))).Dialogs);
    }

    [Fact]
    public async Task Invalid_edit_stays_open_and_preserves_input_without_dispatching()
    {
        var services=Services();using var session=new PortfolioSession(services);await session.ExecuteAsync(Intent("load"));
        await session.ExecuteAsync(Intent("portfolio-open",new(){["view"]="version-portfolio"}));
        var result=await session.ExecuteAsync(Intent("portfolio-save",new(){["Name"]="",["BaseCurrency"]="USD",["OperatingState"]="Draft"}));
        var d=Assert.Single(Document(result).Dialogs);Assert.NotEmpty(d.Status);Assert.Equal("",d.Fields.Single(f=>f.Key=="Name").Value);
        Assert.Empty(services.PortfolioCommands.ReceivedCalls());
    }
    [Fact]
    public void Portfolio_gateway_only_accepts_known_actions_and_bounds_input()
    {
        Assert.True(PortfolioSession.Validate(Intent("portfolio-open",new(){["view"]="allocation"})));
        Assert.False(PortfolioSession.Validate(Intent("execute-sql")));
        Assert.False(PortfolioSession.Validate(Intent("load") with {RequestId=Guid.Empty}));
    }
    [Fact]
    public async Task Fund_filter_and_composite_identity_select_exact_versions_and_clear_empty_selection()
    {
        var services=Services();
        var old=new FundMandateReadModel{PortfolioId=101,FundId=701,FundMandateVersion=1,Name="Same Fund / Name",OperatingState=FundOperatingState.Disabled};
        var latest=old with{FundMandateVersion=2,OperatingState=FundOperatingState.Active};
        services.PortfolioQueries.GetFundsAsync(101,Arg.Any<FundOperatingState?>(),Arg.Any<int>(),Arg.Any<string?>(),Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<PortfolioPage<FundMandateReadModel>>(new(){Items=[latest,old]}));
        using var session=new PortfolioSession(services);
        var active=Document(await session.ExecuteAsync(Intent("load")));
        Assert.Equal("Active",active.Context["fundFilter"]);
        Assert.Equal("2",Assert.Single(active.Grids.Single(g=>g.Key=="funds").Rows).Fields["FundMandateVersion"]);
        var all=Document(await session.ExecuteAsync(Intent("portfolio-fund-filter",new(){["fundFilter"]="All"})));
        var rows=all.Grids.Single(g=>g.Key=="funds").Rows;Assert.Equal(2,rows.Length);Assert.NotEqual(rows[0].Id,rows[1].Id);
        foreach(var row in rows){
            var selected=Document(await session.ExecuteAsync(Intent("portfolio-fund",id:row.Id)));
            Assert.Equal(row.Id,selected.Grids.Single(g=>g.Key=="funds").Selected);
            Assert.Contains(selected.Grids.Single(g=>g.Key=="fund").Rows,r=>r.Fields["Property"]=="Version"&&r.Fields["Value"]==row.Fields["FundMandateVersion"]);
        }
        var disabled=Document(await session.ExecuteAsync(Intent("portfolio-fund-filter",new(){["fundFilter"]="Disabled"})));
        Assert.Single(disabled.Grids.Single(g=>g.Key=="funds").Rows);Assert.False(disabled.Enabled["Change Fund State..."]);
        var empty=Document(await session.ExecuteAsync(Intent("portfolio-fund-filter",new(){["fundFilter"]="Paused"})));
        Assert.Empty(empty.Grids.Single(g=>g.Key=="funds").Rows);Assert.Empty(empty.Grids.Single(g=>g.Key=="fund").Rows);
        Assert.False(empty.Enabled["Financials..."]);
        Assert.Empty(services.PortfolioFundCommands.ReceivedCalls());
    }
    [Theory]
    [InlineData("New Version")]
    [InlineData("Change Policy...")]
    public async Task Administration_loads_embedded_policy_and_all_portfolio_properties_without_a_modal(string editAction)
    {
        var services=Services();using var session=new PortfolioSession(services);
        var initial=await session.ExecuteAsync(Intent("load"));var document=Document(initial);
        Assert.Empty(document.Dialogs);Assert.NotNull(document.Policy);
        var grid=document.Grids.Single(g=>g.Key=="portfolios");
        Assert.Equal(typeof(PortfolioReadModel).GetProperties().Select(p=>p.Name).Order(),grid.Columns.Order());
        Directory.CreateDirectory(Output);File.WriteAllBytes(Path.Combine(Output,"administration.msgpack"),initial.Documents["portfolio"]);
        Assert.Equal("Change Policy...",document.Policy!.Buttons[1]);
        Assert.True(document.Policy.ButtonEnabled!["Change Policy..."]);
        var edit=Document(await session.ExecuteAsync(Intent("portfolio-policy",new(){["action"]=editAction})));
        Assert.Empty(edit.Dialogs);Assert.Contains(edit.Policy!.Fields,f=>f.Key=="Name"&&f.Enabled);
        Assert.False(edit.Policy.ButtonEnabled!["Change Policy..."]);
        var blocked=Document(await session.ExecuteAsync(Intent("portfolio-select",id:"101")));
        Assert.Contains("Save or cancel",blocked.Context["status"]);
        Assert.NotNull(blocked.Policy);
        var cancelled=Document(await session.ExecuteAsync(Intent("portfolio-policy",new(){["action"]="Cancel"})));
        Assert.Empty(cancelled.Dialogs);Assert.DoesNotContain(cancelled.Policy!.Fields,f=>f.Enabled);
        Assert.Empty(services.PortfolioPolicyCommands.ReceivedCalls());
    }
    [Fact]
    public async Task Embedded_policy_activation_refreshes_the_portfolio_grid_policy_reference()
    {
        var services=Services();var q=services.PortfolioQueries;
        var policy=new PortfolioFinancialPolicyReadModel{PortfolioId=101,PolicyId=305,PolicyVersion=3,AggregateRevision=7,Name="New policy",OperatingState=PortfolioFinancialPolicyState.Draft};
        q.GetPoliciesAsync(101,200,Arg.Any<CancellationToken>()).Returns(new ServiceOk<PortfolioPage<PortfolioFinancialPolicyReadModel>>(new(){Items=[policy]}));
        services.PortfolioPolicyCommands.ActivateAndAssignAsync(Arg.Any<TomasAI.IFM.Domain.Portfolio.Shared.Identities.PortfolioFinancialPolicyId>(),7,3,10,Arg.Any<CancellationToken>()).Returns(_=>{
            q.GetPortfoliosAsync(Arg.Any<PortfolioOperatingState?>(),Arg.Any<int>(),Arg.Any<string?>(),Arg.Any<CancellationToken>()).Returns(new ServiceOk<PortfolioPage<PortfolioReadModel>>(new(){Items=[new(){PortfolioId=101,PortfolioVersion=1,Name="Fixture Portfolio",OperatingState=PortfolioOperatingState.Active,ActivePolicyId=305,ActivePolicyVersion=3}]}));
            return new ServiceOk<Guid>(Guid.NewGuid());
        });
        using var session=new PortfolioSession(services);await session.ExecuteAsync(Intent("load"));
        var document=Document(await session.ExecuteAsync(Intent("portfolio-policy",new(){["action"]="Activate && Assign"})));
        var portfolio=Assert.Single(document.Grids.Single(g=>g.Key=="portfolios").Rows);
        Assert.Equal("305",portfolio.Fields["ActivePolicyId"]);Assert.Equal("3",portfolio.Fields["ActivePolicyVersion"]);
        Assert.NotNull(document.Policy);Assert.Empty(document.Dialogs);
    }
    internal static IUiServiceCatalog Services(PortfolioOperatingState state=PortfolioOperatingState.Active)
    {
        var s=Substitute.For<IUiServiceCatalog>();
        var portfolio=new PortfolioReadModel{PortfolioId=101,PortfolioVersion=1,Name="Fixture Portfolio",BaseCurrency="USD",OperatingState=state,ActivePolicyId=301,ActivePolicyVersion=1,EffectiveFromUtc=DateTime.UtcNow};
        var fund=new FundMandateReadModel{PortfolioId=101,FundId=701,FundMandateVersion=1,Name="Fixture Fund",FundCode="FUND-701",TradingYear=2026,OperatingState=FundOperatingState.Active,DecisionHorizon="Daily",EffectiveFromUtc=DateTime.UtcNow};
        var q=s.PortfolioQueries;
        q.GetPortfoliosAsync(Arg.Any<PortfolioOperatingState?>(),Arg.Any<int>(),Arg.Any<string?>(),Arg.Any<CancellationToken>()).Returns(new ServiceOk<PortfolioPage<PortfolioReadModel>>(new(){Items=[portfolio]}));
        q.GetFundsAsync(101,Arg.Any<FundOperatingState?>(),Arg.Any<int>(),Arg.Any<string?>(),Arg.Any<CancellationToken>()).Returns(new ServiceOk<PortfolioPage<FundMandateReadModel>>(new(){Items=[fund]}));
        q.GetPortfolioRevisionAsync(101,Arg.Any<CancellationToken>()).Returns(new ServiceOk<PortfolioAggregateRevision>(new(){PortfolioId=101,Revision=10}));
        q.GetFundRevisionAsync(101,701,Arg.Any<CancellationToken>()).Returns(new ServiceOk<PortfolioAggregateRevision>(new(){PortfolioId=101,FundId=701,Revision=20}));
        q.GetFundAllocationAsync(101,701,Arg.Any<CancellationToken>()).Returns(new ServiceOk<FundAllocationReadModel>(new(){Currency="USD",MaximumWeight=1}));
        q.GetFundRiskEnvelopeAsync(101,701,Arg.Any<DateTime>(),Arg.Any<CancellationToken>()).Returns(new ServiceOk<FundRiskEnvelopeReadModel>(new(){Currency="USD",CapacityState=FundCapacityState.Available}));
        q.GetAssignmentsAsync(101,701,1,Arg.Any<CancellationToken>()).Returns(new ServiceOk<FundTradeTemplateAssignmentReadModel[]>([]));
        q.GetPoliciesAsync(101,200,Arg.Any<CancellationToken>()).Returns(new ServiceOk<PortfolioPage<PortfolioFinancialPolicyReadModel>>(new(){Items=[new(){PortfolioId=101,PolicyId=301,PolicyVersion=1,Name="Fixture policy",BaseCurrency="USD",OperatingState=PortfolioFinancialPolicyState.Draft}]}));
        s.PortfolioIdentities.AllocatePortfolioIdAsync(Arg.Any<CancellationToken>()).Returns(new ServiceOk<PortfolioBusinessIdAllocation>(new(){Kind=PortfolioBusinessIdentityKind.Portfolio,Value=901}));
        s.PortfolioIdentities.AllocateFundIdAsync(Arg.Any<CancellationToken>()).Returns(new ServiceOk<PortfolioBusinessIdAllocation>(new(){Kind=PortfolioBusinessIdentityKind.Fund,Value=902}));
        s.ReferenceQueries.GetStrategyDeploymentChoicesAsync(Arg.Any<CancellationToken>()).Returns(new ServiceOk<StrategyDeploymentChoice[]>([new(new(StrategyCatalogKind.Deployment,Guid.Parse("ba722a00-0000-4000-8000-000000000001"),1),"FixtureDeployment","Fixture deployment",CatalogLifecycleStatus.Published,TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType.Daily,[new(101,"ES","XCME","USD")],["FuturesOption"],[],[])]));
        s.ReferenceQueries.GetTradeStrategySymbolsAsync(Arg.Any<TradeStrategyFamilyType>(),Arg.Any<CancellationToken>()).Returns(new ServiceOk<TradeStrategySymbolReadModel[]>([new(){Id=101,Symbol="ES",Exchange="XCME",Currency="USD",Description="ES"}]));
        s.ReferenceQueries.GetLookupDefinitionsAsync(Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(call=>new ServiceOk<LookupDefinitionReadModel[]>([new(1,call.Arg<string>(),"FuturesOption","Fixture selection","",0,true,DateTime.UtcNow,DateTime.UtcNow)]));
        s.RiskQueries.GetHistoryAsync(101,701,Arg.Any<DateOnly>(),25,Arg.Any<string?>(),Arg.Any<CancellationToken>()).Returns(new ServiceOk<RiskHistoryPage>(new([],null)));
        var api=s.PortfolioFinancial;
        api.GetAccountBalancesAsync(Arg.Any<FinancialReadScope>(),Arg.Any<GetAccountBalancesRequest>(),Arg.Any<CancellationToken>()).Returns(Ok(new FinancialBalanceSnapshot(301,"Active",[new(401,701,"USD",10000,0,10000)],0,10000,true)));
        api.GetFundTransactionsPageAsync(Arg.Any<FinancialReadScope>(),Arg.Any<GetFundTransactionsPageRequest>(),Arg.Any<CancellationToken>()).Returns(Ok(new FinancialPage<FinancialTransactionRow>([],null,5)));
        api.GetFundReservationsPageAsync(Arg.Any<FinancialReadScope>(),Arg.Any<GetFundReservationsPageRequest>(),Arg.Any<CancellationToken>()).Returns(Ok(new FinancialPage<FinancialReservationView>([],null,5)));
        api.GetFinancialLedgerConfigurationAsync(Arg.Any<FinancialReadScope>(),Arg.Any<GetFinancialLedgerConfigurationRequest>(),Arg.Any<CancellationToken>()).Returns(Ok(new FinancialLedgerConfiguration(301,"USD","Development","Active","fixture",[],[new(new(401,1,"Cash",PostingSide.Debit,true,"hash"),"Active")],[new(new(RuleId,1,"hash",LedgerTransactionKind.DepositConfirmed,new(401,1),new(401,1),true),"Active",new(2026,1,1),null)],null)));
        api.GetFinancialPostingConfigurationAsync(Arg.Any<FinancialReadScope>(),Arg.Any<GetFinancialPostingConfigurationRequest>(),Arg.Any<CancellationToken>()).Returns(Ok(new FinancialPostingConfiguration(301,701,new(),[],true,DateOnly.FromDateTime(DateTime.UtcNow))));
        api.PrepareFinancialBookAsync(Arg.Any<FinancialReadScope>(),Arg.Any<PrepareFinancialBookRequest>(),Arg.Any<CancellationToken>()).Returns(Ok(new FinancialBookSetup(["Fixture account"],["Fixture Fund"],null)));
        return s;
    }
    static ServiceResult<FinancialRead<T>> Ok<T>(T value) where T:class=>new ServiceOk<FinancialRead<T>>(new(FinancialReadStatus.Found,value,5,DateTime.UtcNow));
}
