using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.UI.Net.ViewModels.Portfolio;

/// <summary>Scoped financial read state. Each panel reports its own committed revision; history is not cash authority.</summary>
public sealed record FundFinancialViewState(FinancialReadScope? Scope=null, bool Loading=false, string Message="Select a Fund.",
    FinancialRead<FinancialBalanceSnapshot>? Balances=null,
    FinancialRead<FinancialPage<FinancialTransactionRow>>? Transactions=null,
    FinancialRead<FinancialPage<FinancialReservationView>>? Reservations=null,
    FinancialRead<FinancialJournal>? Journal=null);

/// <summary>Typed, bounded financial queries with generation fencing across Fund changes and cancelled requests.</summary>
public sealed class FundFinancialViewModel(IPortfolioFinancialApi api)
{
    long _generation;
    readonly object _stateGate=new();
    FundFinancialViewState _current=new();
    public FundFinancialViewState Current { get { lock(_stateGate) return _current; } }

    public void Clear()
    {
        lock(_stateGate) { _generation++; _current=new(); }
    }

    public async Task LoadAsync(FinancialReadScope scope,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if(scope.PortfolioId<=0 || scope.FundId is not >0) throw new ArgumentException("An exact Portfolio and Fund are required.");
        long generation;
        lock(_stateGate) { generation=++_generation; _current=new(scope,true,"Loading committed financial data..."); }
        try
        {
            var balances=api.GetAccountBalancesAsync(scope,new(),token);
            var transactions=api.GetFundTransactionsPageAsync(scope,new(100),token);
            var reservations=api.GetFundReservationsPageAsync(scope,new(100),token);
            await Task.WhenAll(balances,transactions,reservations).ConfigureAwait(false);
            var b=Read(await balances); var t=Read(await transactions); var r=Read(await reservations);
            string message=b.Status==FinancialReadStatus.NotFound ? "No financial book is configured for this Fund."
                : b.Value is { MigrationQualified:false } ? "Financial book is unqualified; new spending is disabled."
                : $"Financial authority: {b.Value!.OperatingState}. Balances revision {b.FinancialRevision}; transactions cut {t.Value?.AsOfRevision}; reservations cut {r.Value?.AsOfRevision}.";
            Publish(generation,new(scope,false,message,b,t,r));
        }
        catch(OperationCanceledException) when(token.IsCancellationRequested)
        { Publish(generation,new(scope,false,"Financial load cancelled.")); }
        catch(Exception error) { Publish(generation,new(scope,false,"Financial data unavailable: "+error.Message)); }
    }

    public async Task NextTransactionsAsync(CancellationToken token=default)
    {
        var (generation,view)=Snapshot();
        if(view.Loading || view.Scope is null || view.Transactions?.Value?.NextCursor is not { } cursor) return;
        try
        {
            var next=Read(await api.GetFundTransactionsPageAsync(view.Scope,new(100,cursor),token).ConfigureAwait(false));
            Publish(generation,view with { Transactions=next,Journal=null });
        }
        catch(Exception error) { Publish(generation,view with { Message="Transaction page unavailable: "+error.Message }); }
    }

    public async Task NextReservationsAsync(CancellationToken token=default)
    {
        var (generation,view)=Snapshot();
        if(view.Loading || view.Scope is null || view.Reservations?.Value?.NextCursor is not { } cursor) return;
        try
        {
            var next=Read(await api.GetFundReservationsPageAsync(view.Scope,new(100,cursor),token).ConfigureAwait(false));
            Publish(generation,view with { Reservations=next });
        }
        catch(Exception error) { Publish(generation,view with { Message="Reservation page unavailable: "+error.Message }); }
    }

    public async Task LoadJournalAsync(long journalId,CancellationToken token=default)
    {
        var (generation,view)=Snapshot();
        if(view.Loading || view.Scope is null || journalId<=0) return;
        try
        {
            var journal=Read(await api.GetJournalAsync(view.Scope,new(journalId),token).ConfigureAwait(false));
            Publish(generation,view with { Journal=journal });
        }
        catch(Exception error) { Publish(generation,view with { Message="Journal unavailable: "+error.Message,Journal=null }); }
    }

    void Publish(long generation,FundFinancialViewState value)
    {
        lock(_stateGate) if(generation==_generation) _current=value;
    }
    (long,FundFinancialViewState) Snapshot() { lock(_stateGate) return (_generation,_current); }
    static FinancialRead<T> Read<T>(ServiceResult<FinancialRead<T>> response) where T:class
    {
        if(!response.Success || response.Value is null) throw new InvalidOperationException(response.ErrorMessage??"No financial response received.");
        if(response.Value.Status is not (FinancialReadStatus.Found or FinancialReadStatus.NotFound))
            throw new InvalidOperationException("Financial authority is unavailable.");
        return response.Value;
    }
}
