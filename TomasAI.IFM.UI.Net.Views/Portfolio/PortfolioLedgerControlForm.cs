using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.UI.Net.ViewModels.Portfolio;
using TomasAI.IFM.UI.Net.Views.App;

namespace TomasAI.IFM.UI.Net.Views.Portfolio;

/// <summary>Configured ledger selectors, independent reconciliation and recoverable period/control commands.</summary>
public sealed class PortfolioLedgerControlForm:DarkTradingForm
{
    readonly IPortfolioFinancialApi _api;
    readonly FinancialReadScope _scope;
    readonly IPendingFinancialConfigurationStore _store;
    readonly FinancialConfigurationOperation _operations;
    readonly CancellationTokenSource _lifetime=new();
    readonly ComboBox _action=PortfolioUiStyle.Combo("Ledger control action");
    readonly ComboBox _period=PortfolioUiStyle.Combo("Configured ledger period");
    readonly ComboBox _account=PortfolioUiStyle.Combo("Configured ledger account");
    readonly ComboBox _rule=PortfolioUiStyle.Combo("Configured posting rule");
    readonly DateTimePicker _start=new Trade.IronCondor.DarkDateTimePicker { Format=DateTimePickerFormat.Short,Dock=DockStyle.Fill,AccessibleName="New period start" };
    readonly DateTimePicker _end=new Trade.IronCondor.DarkDateTimePicker { Format=DateTimePickerFormat.Short,Dock=DockStyle.Fill,AccessibleName="New period end" };
    readonly TextBox _reason=PortfolioUiStyle.TextBox("Ledger control audit reason");
    readonly Label _summary=new() { Dock=DockStyle.Fill,AutoEllipsis=true,AccessibleName="Ledger reconciliation summary" };
    readonly Label _status=new() { Dock=DockStyle.Fill,AutoEllipsis=true,AccessibleName="Ledger control status" };
    readonly Button _apply=PortfolioUiStyle.Button("Apply","Apply ledger control");
    readonly Button _refresh=PortfolioUiStyle.Button("Refresh","Refresh ledger configuration");
    readonly Button _setup=PortfolioUiStyle.Button("Set up book","Set up a development ledger book");
    readonly DataGridView _periods=PortfolioUiStyle.Grid("Ledger periods");
    readonly DataGridView _accounts=PortfolioUiStyle.Grid("Ledger accounts");
    readonly DataGridView _rules=PortfolioUiStyle.Grid("Ledger posting rules");
    readonly DataGridView _differences=PortfolioUiStyle.Grid("Ledger reconciliation differences");
    readonly DataGridView _pendingGrid=PortfolioUiStyle.Grid("Pending ledger controls");
    FinancialRead<FinancialLedgerConfiguration>? _configuration;
    PendingFinancialConfiguration? _pending;
    bool _busy;
    bool _bookMissing;

    public PortfolioLedgerControlForm(IPortfolioFinancialApi api,FinancialReadScope scope,IPendingFinancialConfigurationStore? store=null)
    {
        _api=api;_scope=scope with { FundId=null };_store=store??PendingFinancialConfigurationStore.ForCurrentUser();_operations=new(api,_store);
        Text="Portfolio ledger administration";Name="PortfolioLedgerControlForm";Size=new(1120,710);MinimumSize=new(940,650);PortfolioUiStyle.Apply(this);
        var root=new TableLayoutPanel { Dock=DockStyle.Fill,Padding=new(12),ColumnCount=2,RowCount=3 };
        root.ColumnStyles.Add(new(SizeType.Percent,100));root.ColumnStyles.Add(new(SizeType.Absolute,330));
        root.RowStyles.Add(new(SizeType.Absolute,74));root.RowStyles.Add(new(SizeType.Percent,100));root.RowStyles.Add(new(SizeType.Absolute,70));
        root.Controls.Add(_summary,0,0);_setup.Dock=DockStyle.Top;root.Controls.Add(_setup,1,0);root.Controls.Add(_status,0,2);root.SetColumnSpan(_status,2);
        var tabs=new DarkTabControl { Dock=DockStyle.Fill,AccessibleName="Ledger administration views" };
        foreach(var (title,grid) in new[] { ("Periods",_periods),("Accounts",_accounts),("Rules",_rules),("Reconciliation",_differences),("Pending",_pendingGrid) })
        {
            grid.AllowUserToAddRows=false;grid.AllowUserToDeleteRows=false;
            var page=new TabPage(title) { BackColor=Color.Black,ForeColor=Color.White };page.Controls.Add(grid);tabs.TabPages.Add(page);
        }
        root.Controls.Add(tabs,0,1);
        var fields=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=9,Padding=new(8,0,0,0) };
        fields.ColumnStyles.Add(new(SizeType.Absolute,95));fields.ColumnStyles.Add(new(SizeType.Percent,100));
        for(int i=0;i<6;i++) fields.RowStyles.Add(new(SizeType.Absolute,37));
        fields.RowStyles.Add(new(SizeType.Absolute,72));fields.RowStyles.Add(new(SizeType.Absolute,123));fields.RowStyles.Add(new(SizeType.Percent,100));
        void Add(string label,Control control,int row)
        {
            fields.Controls.Add(new Label { Text=label,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft },0,row);
            control.Dock=DockStyle.Fill;fields.Controls.Add(control,1,row);
        }
        Add("Action",_action,0);Add("Period",_period,1);Add("Start",_start,2);Add("End",_end,3);Add("Account",_account,4);Add("Rule",_rule,5);
        _reason.Multiline=true;_reason.MaxLength=1024;Add("Reason",_reason,6);
        var buttons=new TableLayoutPanel { Dock=DockStyle.Fill,RowCount=3,ColumnCount=1,Padding=new(0,4,0,0) };
        buttons.ColumnStyles.Add(new(SizeType.Percent,100));
        var close=PortfolioUiStyle.Button("Close","Close ledger administration");close.DialogResult=DialogResult.Cancel;CancelButton=close;
        foreach(var button in new[] { _apply,_refresh,close })
        { button.AutoSize=false;button.Dock=DockStyle.Fill;button.Margin=new(0,2,0,2);buttons.Controls.Add(button);buttons.RowStyles.Add(new(SizeType.Percent,33.333F)); }
        fields.Controls.Add(buttons,1,7);root.Controls.Add(fields,1,1);Controls.Add(root);
        _action.DataSource=new[] { new ActionChoice(LedgerConfigurationAction.Reconcile,"Reconcile"),new(LedgerConfigurationAction.OpenPeriod,"Open period"),
            new(LedgerConfigurationAction.ClosePeriod,"Close period"),new(LedgerConfigurationAction.ReopenPeriod,"Reopen period"),
            new(LedgerConfigurationAction.RetireAccount,"Retire account"),new(LedgerConfigurationAction.RetirePostingRule,"Retire rule"),
            new(LedgerConfigurationAction.QualifyDevelopmentBook,"Qualify book"),new(LedgerConfigurationAction.RefreshAuthority,"Refresh authority") };
        _action.DisplayMember=nameof(ActionChoice.Label);_action.SelectedIndexChanged+=(_,_)=>UpdateEnabled();
        _apply.Click+=async(_,_)=>await RunAsync(ApplyAsync);_refresh.Click+=async(_,_)=>await RunAsync(LoadAsync);
        _setup.Click+=async(_,_)=>
        {
            using var setup=new PortfolioLedgerSetupForm(_api,_scope,_store);setup.ShowDialog(this);
            if(!IsDisposed) await RunAsync(LoadAsync);
        };
        _pendingGrid.CellDoubleClick+=async(_,e)=>
        {
            if(e.RowIndex<0 || _pendingGrid.Rows[e.RowIndex].DataBoundItem is not PendingRow row) return;
            await RunAsync(async()=>
            {
                _pending=await _store.LoadAsync(row.OperationId,_lifetime.Token);
                if(_pending is null) return;
                _pending=await _operations.RecoverAsync(_pending.Request.OperationId,_lifetime.Token);
                await LoadAsync();_status.Text=_pending.Message;
            });
        };
        Shown+=async(_,_)=>await RunAsync(LoadAsync);
        FormClosed+=(_,_)=>_lifetime.Cancel();
        _status.Text="Loading ledger configuration...";UpdateEnabled();
    }
    async Task RunAsync(Func<Task> work)
    {
        if(_busy) return;_busy=true;UpdateEnabled();
        try { await work(); }
        catch(Exception error) { if(!IsDisposed) _status.Text=error.Message; }
        finally { _busy=false;if(!IsDisposed) UpdateEnabled(); }
    }
    async Task LoadAsync()
    {
        var response=await _api.GetFinancialLedgerConfigurationAsync(_scope,new(),_lifetime.Token);
        if(!response.Success || response.Value is null)
            throw new InvalidOperationException(response.ErrorMessage??"Ledger configuration could not be read.");
        var pending=await _store.ListAsync(_scope.PortfolioId,_lifetime.Token);
        if(IsDisposed) return;
        _pendingGrid.DataSource=pending.Where(x=>x.Phase is PendingFinancialPhase.Prepared or PendingFinancialPhase.OutcomeUnknown)
            .Concat(pending.Where(x=>x.Phase is PendingFinancialPhase.Committed or PendingFinancialPhase.ExpiredWithoutPosting).Take(100))
            .Select(x=>new PendingRow(x.Request.OperationId,x.Request.Body.Action,x.Phase,x.Message)).ToArray();
        _bookMissing=response.Value.Status==FinancialReadStatus.NotFound;
        if(_bookMissing)
        {
            _configuration=null;_summary.Text="No ledger book is configured for this Portfolio.";
            _status.Text="Set up a development book, or recover an existing request from Pending before creating another.";return;
        }
        if(response.Value.Value is null) throw new InvalidOperationException("Ledger configuration is missing.");
        _configuration=response.Value;var c=_configuration.Value!;
        var selectedPeriod=(_period.SelectedItem as PeriodChoice)?.Value.PeriodId;
        _period.DataSource=c.Periods.Select(x=>new PeriodChoice(x,$"{x.StartDate:d} - {x.EndDate:d} ({x.State})")).ToArray();_period.DisplayMember=nameof(PeriodChoice.Label);
        if(selectedPeriod is not null) _period.SelectedItem=(_period.DataSource as PeriodChoice[])!.FirstOrDefault(x=>x.Value.PeriodId==selectedPeriod);
        _account.DataSource=c.Accounts.Select(x=>new AccountChoice(x,$"{x.Definition.AccountId}: {x.Definition.Category} ({x.State})")).ToArray();_account.DisplayMember=nameof(AccountChoice.Label);
        _rule.DataSource=c.Rules.Select(x=>new RuleChoice(x,$"{x.Definition.Kind} v{x.Definition.Version} ({x.State})")).ToArray();_rule.DisplayMember=nameof(RuleChoice.Label);
        _periods.DataSource=c.Periods;
        _accounts.DataSource=c.Accounts.Select(x=>new { x.Definition.AccountId,x.Definition.Version,x.Definition.Category,x.Definition.NormalSide,x.State }).ToArray();
        _rules.DataSource=c.Rules.Select(x=>new { x.Definition.Kind,x.Definition.RuleId,x.Definition.Version,DebitAccount=x.Definition.Debit.AccountId,CreditAccount=x.Definition.Credit.AccountId,x.State,x.EffectiveFrom,x.EffectiveTo }).ToArray();
        _differences.DataSource=c.LatestReconciliation?.Differences??[];
        _summary.Text=$"Book {c.BookId} | {c.Currency} | {c.OperatingState} | Revision {_configuration.FinancialRevision}\n"+
            (c.LatestReconciliation is { } r?$"Ledger reconciliation: debits {r.Debits:N2}, credits {r.Credits:N2}, differences {r.Differences.Length}; source cut {r.SourceCut}":"No ledger reconciliation has been recorded.");
        _status.Text="Select a configured control and enter an audit reason. Reconciliation compares journals with recorded balances.";
    }
    async Task ApplyAsync()
    {
        if(_pending?.Phase is not (PendingFinancialPhase.Prepared or PendingFinancialPhase.OutcomeUnknown))
        {
            if(_configuration is null || _action.SelectedItem is not ActionChoice action) return;
            if(action.Value==LedgerConfigurationAction.RefreshAuthority)
            {
                using var review=new PortfolioFinancialAuthorityForm(_api,_scope,_store);review.ShowDialog(this);
                if(!IsDisposed) await LoadAsync();return;
            }
            var command=FinancialControlPreparation.Create(_scope,_configuration,action.Value,_reason.Text,DateTime.UtcNow,
                (_period.SelectedItem as PeriodChoice)?.Value.PeriodId,DateOnly.FromDateTime(_start.Value),DateOnly.FromDateTime(_end.Value),
                (_account.SelectedItem as AccountChoice)?.Value.Definition.AccountId,(_rule.SelectedItem as RuleChoice)?.Value.Definition.RuleId);
            _pending=new(command,PendingFinancialPhase.Prepared,"Prepared.");
        }
        _pending=_pending.Phase==PendingFinancialPhase.Prepared
            ?await _operations.SubmitAsync(_pending.Request,_lifetime.Token)
            :await _operations.RecoverAsync(_pending.Request.OperationId,_lifetime.Token);
        if(IsDisposed) return;await LoadAsync();_status.Text=_pending.Message;
    }
    void UpdateEnabled()
    {
        var unresolved=_pending?.Phase is PendingFinancialPhase.Prepared or PendingFinancialPhase.OutcomeUnknown;
        var editing=!_busy && !unresolved && _configuration is not null;
        var action=(_action.SelectedItem as ActionChoice)?.Value;
        _action.Enabled=editing;_reason.Enabled=editing;
        _period.Enabled=editing && action is LedgerConfigurationAction.ClosePeriod or LedgerConfigurationAction.ReopenPeriod;
        _start.Enabled=_end.Enabled=editing && action==LedgerConfigurationAction.OpenPeriod;
        _account.Enabled=editing && action==LedgerConfigurationAction.RetireAccount;_rule.Enabled=editing && action==LedgerConfigurationAction.RetirePostingRule;
        _apply.Text=unresolved?"Check outcome":"Apply";
        _apply.Enabled=!_busy && _configuration is not null && (_scope.Access.Roles.Contains("PortfolioAdministrator") || _scope.Access.Roles.Contains("LedgerConfigure"));
        _refresh.Enabled=!_busy;
        _setup.Visible=_bookMissing;
        _setup.Enabled=!_busy && _bookMissing && _pendingGrid.Rows.Cast<DataGridViewRow>().All(x=>x.DataBoundItem is not PendingRow { Phase:PendingFinancialPhase.Prepared or PendingFinancialPhase.OutcomeUnknown }) &&
            (_scope.Access.Roles.Contains("PortfolioAdministrator") || _scope.Access.Roles.Contains("LedgerConfigure"));
    }
    sealed record ActionChoice(LedgerConfigurationAction Value,string Label);
    sealed record PeriodChoice(FinancialLedgerPeriod Value,string Label);
    sealed record AccountChoice(FinancialConfiguredAccount Value,string Label);
    sealed record RuleChoice(FinancialConfiguredRule Value,string Label);
    sealed record PendingRow(Guid OperationId,LedgerConfigurationAction Action,PendingFinancialPhase Phase,string Message);
}
