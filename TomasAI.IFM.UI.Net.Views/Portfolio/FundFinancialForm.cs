using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.UI.Net.ViewModels.Portfolio;
using TomasAI.IFM.UI.Net.Views.App;

namespace TomasAI.IFM.UI.Net.Views.Portfolio;

/// <summary>Financial authority, bounded history and persisted posting journeys for the selected Portfolio Fund.</summary>
public sealed class FundFinancialForm : DarkTradingForm
{
    readonly FundFinancialViewModel _model;
    readonly IPortfolioFinancialApi _api;
    readonly IPendingFinancialOperationStore _pendingStore;
    string? _loadError;
    readonly DataGridView _pendingGrid=PortfolioUiStyle.Grid("Financial operation recovery");
    readonly Button _post=PortfolioUiStyle.Button("Transaction","Create or correct Fund transaction");
    readonly Button _ledger=PortfolioUiStyle.Button("Ledger","View ledger controls and reconciliation");
    readonly FinancialReadScope _scope;
    readonly CancellationTokenSource _lifetime=new();
    readonly DataGridView _balances=PortfolioUiStyle.Grid("Committed financial balances");
    readonly DataGridView _transactions=PortfolioUiStyle.Grid("Fund ledger transactions");
    readonly DataGridView _reservations=PortfolioUiStyle.Grid("Fund capacity reservations");
    readonly DataGridView _journal=PortfolioUiStyle.Grid("Selected journal entries");
    readonly Label _status=new() { Dock=DockStyle.Bottom,Height=55,Padding=new(6),AutoEllipsis=true };
    readonly Label _cash=new() { Dock=DockStyle.Top,Height=36,Padding=new(6),AutoEllipsis=true };
    readonly Button _refresh=PortfolioUiStyle.Button("Refresh","Refresh Fund financials");
    readonly Button _nextTransactions=PortfolioUiStyle.Button("Next transactions","Next transaction page");
    readonly Button _nextReservations=PortfolioUiStyle.Button("Next reservations","Next reservation page");
    readonly DarkTabControl _tabs=new() { Dock=DockStyle.Fill,AccessibleName="Fund financial views" };
    bool _loading;

    public FundFinancialForm(IPortfolioFinancialApi api,FinancialReadScope scope,string fundName,IPendingFinancialOperationStore? pendingStore=null,
        int? legacySourceFundId=null,TomasAI.IFM.UI.Net.Services.Fund.FundQueryService? legacyQueries=null)
    {
        _model=new(api); _scope=scope;_api=api;_pendingStore=pendingStore??PendingFinancialOperationStore.ForCurrentUser();
        Text=$"{fundName} — Financials"; Name="FundFinancialForm"; AccessibleName="Portfolio Fund financials";
        Size=new(1150,700); MinimumSize=new(850,500); PortfolioUiStyle.Apply(this);
        var close=PortfolioUiStyle.Button("Close","Close Fund financials"); close.DialogResult=DialogResult.Cancel;
        CancelButton=close;
        var toolbar=new FlowLayoutPanel { Dock=DockStyle.Top,Height=48,Padding=new(6),WrapContents=false };
        toolbar.Controls.AddRange([_refresh,_nextTransactions,_nextReservations,_post,_ledger,close]);
        foreach(var (title,grid) in new[] { ("Balances",_balances),("Transactions",_transactions),("Reservations",_reservations),("Journal",_journal),("Pending operations",_pendingGrid) })
        {
            var page=new TabPage(title) { BackColor=Color.Black,ForeColor=Color.White };
            grid.Dock=DockStyle.Fill; grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.DisplayedCells;
            grid.AllowUserToAddRows=false; grid.AllowUserToDeleteRows=false;
            page.Controls.Add(grid); _tabs.TabPages.Add(page);
        }
        var border=new Panel { Dock=DockStyle.Fill,Padding=new(3),BackColor=PortfolioUiStyle.Border };
        if(legacySourceFundId is >=0)
        {
            var history=new TabPage("Legacy history") { BackColor=Color.Black,ForeColor=Color.White };
            history.Controls.Add(new LegacyFinancialHistoryControl(legacySourceFundId.Value,legacyQueries));
            _tabs.TabPages.Add(history);
        }
        var content=new Panel { Dock=DockStyle.Fill,BackColor=Color.Black };
        content.Controls.Add(_tabs); content.Controls.Add(_cash); content.Controls.Add(_status); content.Controls.Add(toolbar);
        border.Controls.Add(content); Controls.Add(border);
        _post.Click+=async(_,_)=>await OpenPostingAsync();
        _ledger.Click+=async(_,_)=>
        {
            using var controls=new PortfolioLedgerControlForm(_api,_scope);
            controls.ShowDialog(this);
            if(!IsDisposed) await RunAsync(()=>_model.LoadAsync(_scope,_lifetime.Token));
        };
        _pendingGrid.CellDoubleClick+=async(_,e)=>
        {
            if(e.RowIndex<0 || _pendingGrid.Rows[e.RowIndex].DataBoundItem is not PendingRow row) return;
            try
            {
                var pending=await _pendingStore.LoadAsync(row.OperationId,_lifetime.Token);
                if(pending is null || IsDisposed) return;
                using var editor=new FundFinancialPostingForm(_api,_scope,_pendingStore,pending:pending);
                editor.ShowDialog(this);
                if(!IsDisposed) await RunAsync(()=>_model.LoadAsync(_scope,_lifetime.Token));
            }
            catch(Exception error) { if(!IsDisposed) _status.Text=error.Message; }
        };
        _refresh.Click+=async(_,_)=>await RunAsync(()=>_model.LoadAsync(_scope,_lifetime.Token));
        _nextTransactions.Click+=async(_,_)=>await RunAsync(()=>_model.NextTransactionsAsync(_lifetime.Token));
        _nextReservations.Click+=async(_,_)=>await RunAsync(()=>_model.NextReservationsAsync(_lifetime.Token));
        _transactions.CellDoubleClick+=async(_,e)=>
        {
            if(e.RowIndex<0 || _transactions.Rows[e.RowIndex].DataBoundItem is not TransactionRow { JournalId: >0 } row) return;
            await RunAsync(()=>_model.LoadJournalAsync(row.JournalId.Value,_lifetime.Token));
            if(!IsDisposed && _model.Current.Journal?.Value is not null) _tabs.SelectedIndex=3;
        };
        Shown+=async(_,_)=>await RunAsync(()=>_model.LoadAsync(_scope,_lifetime.Token));
        FormClosed+=(_,_)=>{_model.Clear();_lifetime.Cancel();_lifetime.Dispose();};
        Bind();
    }

    async Task RunAsync(Func<Task> load)
    {
        if(_loading) return;
        _loading=true;_loadError=null;
        _refresh.Enabled=false; _nextTransactions.Enabled=false; _nextReservations.Enabled=false;_post.Enabled=false;_ledger.Enabled=false;
        _status.Text="Loading committed financial data...";
        try { await load();
            var pending=await _pendingStore.ListAsync(_scope.PortfolioId,_scope.FundId!.Value,_lifetime.Token);
            if(!IsDisposed) _pendingGrid.DataSource=pending.Where(x=>x.Phase is PendingFinancialPhase.Prepared or PendingFinancialPhase.OutcomeUnknown)
                .Concat(pending.Where(x=>x.Phase is PendingFinancialPhase.Committed or PendingFinancialPhase.ExpiredWithoutPosting).Take(100)).Select(x=>new PendingRow(x.Request.OperationId,x.Request.Body.TransactionKind,x.Request.Body.Amount,x.Phase,x.Message)).ToArray();
        }
        catch(Exception error) { _loadError=error.Message; }
        finally { _loading=false; if(!IsDisposed && !Disposing) Bind(); }
    }

    void Bind()
    {
        var view=_model.Current;
        _status.Text=_loadError??view.Message;
        _cash.Text=view.Balances?.Value is { } balances
            ? $"Available cash: {balances.AvailableCash:N2} USD    Pending withdrawals: {balances.PendingWithdrawals:N2} USD    {balances.OperatingState}"
            : "Available cash: unavailable";
        _balances.DataSource=view.Balances?.Value?.Accounts ?? [];
        _transactions.DataSource=view.Transactions?.Value?.Items.Select(x=>new TransactionRow(x.TransactionId,x.Transaction.AccountingDate,
            x.Transaction.TransactionKind,x.Transaction.Amount,x.Transaction.Currency,x.Transaction.Description,x.JournalId,x.OperationId)).ToArray() ?? [];
        _reservations.DataSource=view.Reservations?.Value?.Items.Select(x=>new ReservationRow(x.OriginalReceipt.ReservationId,
            x.OriginalReceipt.OrderId,x.Current.Status,x.Current.StrategyUnits,x.Current.FilledUnits,x.Current.ClosedUnits,x.Current.CancelledUnits,x.Current.RemainingUnits,x.Current.Version)).ToArray() ?? [];
        _journal.DataSource=view.Journal?.Value?.Entries ?? [];
        foreach(var grid in new[] { _balances,_transactions,_journal })
            foreach(var name in new[] { "Debits","Credits","Balance","Amount","Debit","Credit" })
                if(grid.Columns.Contains(name))
                {
                    grid.Columns[name]!.DefaultCellStyle.Format="N2";
                    grid.Columns[name]!.DefaultCellStyle.Alignment=DataGridViewContentAlignment.MiddleRight;
                }
        _post.Enabled=!view.Loading && view.Balances?.Value is not null && (_scope.Access.Roles.Contains("PortfolioAdministrator") || _scope.Access.Roles.Contains("LedgerPost"));
        _ledger.Enabled=!view.Loading;
        _refresh.Enabled=!view.Loading;
        _nextTransactions.Enabled=!view.Loading && view.Transactions?.Value?.NextCursor is not null;
        _nextReservations.Enabled=!view.Loading && view.Reservations?.Value?.NextCursor is not null;
    }
    async Task OpenPostingAsync()
    {
        FinancialTransactionRow? related=null;
        if(_transactions.CurrentRow?.DataBoundItem is TransactionRow row)
            related=_model.Current.Transactions?.Value?.Items.SingleOrDefault(x=>x.OperationId==row.OperationId);
        using var editor=new FundFinancialPostingForm(_api,_scope,_pendingStore,related);
        editor.ShowDialog(this);if(!IsDisposed) await RunAsync(()=>_model.LoadAsync(_scope,_lifetime.Token));
    }
    sealed record PendingRow(Guid OperationId,LedgerTransactionKind Type,decimal Amount,PendingFinancialPhase Phase,string Message);
    sealed record TransactionRow(long TransactionId,DateOnly AccountingDate,LedgerTransactionKind Type,decimal Amount,string Currency,string Description,long? JournalId,Guid OperationId);
    sealed record ReservationRow(Guid ReservationId,int OrderId,ReservationStatus Status,int Units,int Filled,int Closed,int Cancelled,int Remaining,long Version);
}
