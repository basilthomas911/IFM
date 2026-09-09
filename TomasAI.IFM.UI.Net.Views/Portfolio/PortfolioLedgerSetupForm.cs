using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.UI.Net.ViewModels.Portfolio;
using TomasAI.IFM.UI.Net.Views.App;

namespace TomasAI.IFM.UI.Net.Views.Portfolio;

/// <summary>Reviews a server-prepared development book and persists its exact request before dispatch.</summary>
public sealed class PortfolioLedgerSetupForm:DarkTradingForm
{
    readonly IPortfolioFinancialApi _api;
    readonly FinancialReadScope _scope;
    readonly FinancialConfigurationOperation _operations;
    readonly CancellationTokenSource _lifetime=new();
    readonly ComboBox _account=PortfolioUiStyle.Combo("Configured Portfolio execution account");
    readonly DateTimePicker _start=new Trade.IronCondor.DarkDateTimePicker { Format=DateTimePickerFormat.Short,Dock=DockStyle.Fill };
    readonly DateTimePicker _end=new Trade.IronCondor.DarkDateTimePicker { Format=DateTimePickerFormat.Short,Dock=DockStyle.Fill };
    readonly TextBox _reason=PortfolioUiStyle.TextBox("Book setup reason");
    readonly TextBox _review=PortfolioUiStyle.TextBox("Generated book configuration",true);
    readonly Label _status=new() { Dock=DockStyle.Fill,AutoEllipsis=true };
    readonly Button _prepare=PortfolioUiStyle.Button("Prepare","Prepare development ledger book");
    readonly Button _save=PortfolioUiStyle.Button("Save","Save prepared ledger book");
    LedgerConfigurationRequest? _draft;
    PendingFinancialConfiguration? _pending;
    bool _busy;

    public PortfolioLedgerSetupForm(IPortfolioFinancialApi api,FinancialReadScope scope,IPendingFinancialConfigurationStore store)
    {
        _api=api;_scope=scope with { FundId=null };_operations=new(api,store);
        Text="Development ledger setup";Name="PortfolioLedgerSetupForm";Size=new(850,660);MinimumSize=new(780,610);PortfolioUiStyle.Apply(this);
        var root=new TableLayoutPanel { Dock=DockStyle.Fill,Padding=new(14),ColumnCount=2,RowCount=7 };
        root.ColumnStyles.Add(new(SizeType.Absolute,145));root.ColumnStyles.Add(new(SizeType.Percent,100));
        for(int row=0;row<3;row++) root.RowStyles.Add(new(SizeType.Absolute,38));
        root.RowStyles.Add(new(SizeType.Absolute,68));root.RowStyles.Add(new(SizeType.Percent,100));root.RowStyles.Add(new(SizeType.Absolute,48));root.RowStyles.Add(new(SizeType.Absolute,65));
        void Field(string label,Control control,int row)
        {
            root.Controls.Add(new Label { Text=label,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft },0,row);
            control.Dock=DockStyle.Fill;root.Controls.Add(control,1,row);
        }
        Field("Execution account",_account,0);Field("Period start",_start,1);Field("Period end",_end,2);
        _reason.Multiline=true;_reason.MaxLength=1024;Field("Reason",_reason,3);
        _start.Value=new(DateTime.Today.Year,1,1);_end.Value=new(DateTime.Today.Year,12,31);
        _review.Multiline=true;_review.ScrollBars=ScrollBars.Vertical;root.Controls.Add(_review,0,4);root.SetColumnSpan(_review,2);
        var buttons=new FlowLayoutPanel { Dock=DockStyle.Fill,WrapContents=false,FlowDirection=FlowDirection.RightToLeft };
        var close=PortfolioUiStyle.Button("Close","Close ledger setup");close.DialogResult=DialogResult.Cancel;CancelButton=close;
        foreach(var button in new[] { close,_save,_prepare }) button.MinimumSize=new(125,32);
        buttons.Controls.AddRange([close,_save,_prepare]);root.Controls.Add(buttons,0,5);root.SetColumnSpan(buttons,2);
        root.Controls.Add(_status,0,6);root.SetColumnSpan(_status,2);Controls.Add(root);
        _prepare.Click+=async(_,_)=>await RunAsync(PrepareAsync);_save.Click+=async(_,_)=>await RunAsync(SaveAsync);
        Shown+=async(_,_)=>await RunAsync(async()=>
        {
            var response=await _api.PrepareFinancialBookAsync(_scope,new(),_lifetime.Token);
            if(!response.Success || response.Value?.Value is not { } setup) throw new InvalidOperationException(response.ErrorMessage??"Book setup is unavailable.");
            if(IsDisposed) return;_account.DataSource=setup.ExecutionAccounts;
            _review.Text="Funds: "+string.Join(", ",setup.FundNames)+Environment.NewLine+"Prepare to review generated accounts and posting rules. No capital is added and spending stays disabled.";
        });
        _account.SelectedIndexChanged+=(_,_)=>ClearDraft();_start.ValueChanged+=(_,_)=>ClearDraft();_end.ValueChanged+=(_,_)=>ClearDraft();
        FormClosed+=(_,_)=>_lifetime.Cancel();UpdateEnabled();
    }
    void ClearDraft() { if(_pending is null) _draft=null;UpdateEnabled(); }
    async Task PrepareAsync()
    {
        if(_account.SelectedItem is not string account) throw new InvalidOperationException("Select a configured execution account.");
        var response=await _api.PrepareFinancialBookAsync(_scope,new(account,DateOnly.FromDateTime(_start.Value),DateOnly.FromDateTime(_end.Value)),_lifetime.Token);
        if(!response.Success || response.Value?.Value?.Draft is not { } draft) throw new InvalidOperationException(response.ErrorMessage??"Book preparation failed.");
        if(IsDisposed) return;_draft=draft;
        _review.Text=$"Generated book {draft.BookId} | {draft.Book!.Currency} | Development\r\n"+
            string.Join("\r\n",draft.Accounts.Select(x=>$"Account {x.AccountId}: {x.Category}, v{x.Version}"))+"\r\n\r\nRules: "+
            string.Join(", ",draft.Rules.Select(x=>x.Kind))+"\r\n\r\nThis creates configuration only. Opening capital and qualification are separate operations.";
        _status.Text="Review the prepared configuration and enter an audit reason before saving.";
    }
    async Task SaveAsync()
    {
        if(_pending is null)
        {
            if(_draft is null) return;
            _pending=new(FinancialControlPreparation.CreateBook(_scope,_draft,_reason.Text,DateTime.UtcNow),PendingFinancialPhase.Prepared,"Prepared.");
        }
        _pending=_pending.Phase==PendingFinancialPhase.Prepared?await _operations.SubmitAsync(_pending.Request,_lifetime.Token)
            :await _operations.RecoverAsync(_pending.Request.OperationId,_lifetime.Token);
        if(!IsDisposed) _status.Text=_pending.Message;
    }
    async Task RunAsync(Func<Task> work)
    {
        if(_busy) return;_busy=true;UpdateEnabled();
        try { await work(); } catch(Exception error) { if(!IsDisposed) _status.Text=error.Message; }
        finally { _busy=false;if(!IsDisposed) UpdateEnabled(); }
    }
    void UpdateEnabled()
    {
        var editable=!_busy && _pending is null;
        _account.Enabled=_start.Enabled=_end.Enabled=_reason.Enabled=editable;
        _prepare.Enabled=editable && _account.Items.Count>0;
        _save.Text=_pending?.Phase switch { PendingFinancialPhase.Committed=>"Saved",PendingFinancialPhase.ExpiredWithoutPosting=>"Expired",null=>"Save",_=>"Check outcome" };
        _save.Enabled=!_busy && (_draft is not null && _pending is null || _pending?.Phase is PendingFinancialPhase.Prepared or PendingFinancialPhase.OutcomeUnknown);
    }
}
