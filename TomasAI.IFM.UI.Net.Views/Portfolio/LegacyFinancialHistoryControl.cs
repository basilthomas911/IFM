using TomasAI.IFM.UI.Net.Services.Fund;

namespace TomasAI.IFM.UI.Net.Views.Portfolio;

/// <summary>Original legacy values, scoped by the explicit historical Fund mapping. No mutation service is accepted.</summary>
public sealed class LegacyFinancialHistoryControl : UserControl
{
    readonly DataGridView _history=PortfolioUiStyle.Grid("Read-only legacy financial history");
    readonly DateTimePicker _from=new Trade.IronCondor.DarkDateTimePicker { Format=DateTimePickerFormat.Short,Width=125,AccessibleName="Legacy history from" };
    readonly DateTimePicker _to=new Trade.IronCondor.DarkDateTimePicker { Format=DateTimePickerFormat.Short,Width=125,AccessibleName="Legacy history to" };
    readonly Button _load=PortfolioUiStyle.Button("Load history","Load original legacy transactions");
    readonly Label _status=new() { Dock=DockStyle.Bottom,Height=48,Padding=new(6),AutoEllipsis=true };
    readonly CancellationTokenSource _lifetime=new();
    readonly int _sourceFundId;
    readonly FundQueryService? _queries;
    bool _loading;

    public LegacyFinancialHistoryControl(int sourceFundId,FundQueryService? queries)
    {
        _sourceFundId=sourceFundId;_queries=queries;
        Dock=DockStyle.Fill;BackColor=Color.Black;ForeColor=Color.White;
        _from.Value=DateTime.Today.AddMonths(-1);_to.Value=DateTime.Today;
        var toolbar=new FlowLayoutPanel { Dock=DockStyle.Top,Height=48,Padding=new(6),WrapContents=false };
        toolbar.Controls.AddRange([_from,_to,_load]);
        var note=new Label { Dock=DockStyle.Top,Height=55,Padding=new(6),
            Text=$"Legacy Fund {sourceFundId} — read-only original records. Currency is unrecorded. Amounts and legacy balances do not fund the new ledger; development capital is entered separately." };
        _history.Dock=DockStyle.Fill;_history.ReadOnly=true;_history.AllowUserToAddRows=false;_history.AllowUserToDeleteRows=false;
        _history.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.DisplayedCells;
        Controls.Add(_history);Controls.Add(_status);Controls.Add(toolbar);Controls.Add(note);
        _load.Enabled=queries is not null;
        _status.Text=queries is null?"Legacy history service is unavailable.":"Choose a date range and load history (up to one year).";
        _load.Click+=async(_,_)=>await LoadHistoryAsync();
    }

    async Task LoadHistoryAsync()
    {
        if(_loading || _queries is null) return;
        _loading=true;_load.Enabled=false;_history.DataSource=null;
        _status.Text="Loading original legacy records...";
        try
        {
            var rows=await _queries.GetLegacyTransactionsAsync(_sourceFundId,DateOnly.FromDateTime(_from.Value),DateOnly.FromDateTime(_to.Value),_lifetime.Token);
            if(IsDisposed || Disposing) return;
            _history.DataSource=rows.OrderByDescending(x=>x.ValueDate).ThenByDescending(x=>x.TransactionDate)
                .Select(x=>new { x.ValueDate,Type=x.TransactionType.ToString(),OriginalAmount=x.Amount,LegacyBalance=x.Balance,Currency="Unrecorded",x.Description,
                    Disposition=Enum.IsDefined(x.TransactionType)&&x.TransactionType!=TomasAI.IFM.Domain.Fund.Shared.FundTransactionType.Unknown&&x.TransactionId>0&&x.TransactionDate!=default?"Read-only history":"Unqualified legacy record",x.TransactionId,x.FundId,x.OrderId,x.TradeId,x.TransactionDate }).ToArray();
            // Preserve source decimal precision; no currency formatting or ledger-derived totals.
            foreach(var name in new[] { "OriginalAmount","LegacyBalance" })
                if(_history.Columns.Contains(name)) _history.Columns[name]!.DefaultCellStyle.Format="0.############################";
            _status.Text=$"{rows.Length} original records. Historical values are excluded from available cash and capacity.";
        }
        catch(OperationCanceledException) when(_lifetime.IsCancellationRequested) { }
        catch(Exception error) { if(!IsDisposed && !Disposing) _status.Text="Legacy history unavailable: "+error.Message; }
        finally { _loading=false;if(!IsDisposed && !Disposing) _load.Enabled=true; }
    }

    protected override void Dispose(bool disposing)
    {
        if(disposing) _lifetime.Cancel();
        base.Dispose(disposing);
    }
}
