using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.ViewModels.Portfolio;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.Portfolio;

[Trait("Category","PortfolioFinancial")]
public sealed class FundFinancialViewModelTests
{
    [Fact]
    public async Task Slow_old_fund_cannot_replace_new_fund_financial_data()
    {
        var api=Api(); var first=Scope(1); var second=Scope(2);
        var waiting=new TaskCompletionSource<ServiceResult<FinancialRead<FinancialBalanceSnapshot>>>(TaskCreationOptions.RunContinuationsAsynchronously);
        api.GetAccountBalancesAsync(first,Arg.Any<GetAccountBalancesRequest>(),Arg.Any<CancellationToken>()).Returns(waiting.Task);
        var model=new FundFinancialViewModel(api);
        var old=model.LoadAsync(first);
        await model.LoadAsync(second);
        waiting.SetResult(Ok(new FinancialBalanceSnapshot(1,"Active",[],0,999,true),1));
        await old;
        model.Current.Scope!.FundId.Should().Be(2); model.Current.Balances!.Value!.AvailableCash.Should().Be(100);
    }

    [Fact]
    public async Task Clear_fences_late_response_and_never_restores_selection()
    {
        var api=Api(); var scope=Scope(1);
        var waiting=new TaskCompletionSource<ServiceResult<FinancialRead<FinancialBalanceSnapshot>>>(TaskCreationOptions.RunContinuationsAsynchronously);
        api.GetAccountBalancesAsync(scope,Arg.Any<GetAccountBalancesRequest>(),Arg.Any<CancellationToken>()).Returns(waiting.Task);
        var model=new FundFinancialViewModel(api); var load=model.LoadAsync(scope); model.Clear();
        waiting.SetResult(Ok(new FinancialBalanceSnapshot(1,"Active",[],0,999,true),1)); await load;
        model.Current.Scope.Should().BeNull(); model.Current.Balances.Should().BeNull();
    }

    [Fact]
    public async Task Next_page_uses_original_scoped_cursor_and_missing_book_is_not_zero_cash()
    {
        var api=Api(); var scope=Scope(1); var cursor=new FinancialPageCursor(1,1,7,2,0,"Transactions");
        api.GetFundTransactionsPageAsync(scope,Arg.Is<GetFundTransactionsPageRequest>(x=>x.Cursor==null),Arg.Any<CancellationToken>())
            .Returns(Ok(new FinancialPage<FinancialTransactionRow>([],cursor,7),7));
        var model=new FundFinancialViewModel(api); await model.LoadAsync(scope); await model.NextTransactionsAsync();
        await api.Received(1).GetFundTransactionsPageAsync(scope,Arg.Is<GetFundTransactionsPageRequest>(x=>x.PageSize==100 && x.Cursor==cursor),Arg.Any<CancellationToken>());
        api.GetAccountBalancesAsync(scope,Arg.Any<GetAccountBalancesRequest>(),Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<FinancialRead<FinancialBalanceSnapshot>>(new(FinancialReadStatus.NotFound,null,0,DateTime.UtcNow)));
        await model.LoadAsync(scope);
        model.Current.Balances!.Value.Should().BeNull(); model.Current.Message.Should().Contain("No financial book");
    }

    [Fact]
    public async Task Query_outage_clears_stale_balances_instead_of_presenting_them_as_current()
    {
        var api=Api(); var scope=Scope(1); var model=new FundFinancialViewModel(api); await model.LoadAsync(scope);
        api.GetAccountBalancesAsync(scope,Arg.Any<GetAccountBalancesRequest>(),Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ServiceResult<FinancialRead<FinancialBalanceSnapshot>>>(new TimeoutException("offline")));
        await model.LoadAsync(scope);
        model.Current.Balances.Should().BeNull(); model.Current.Message.Should().Contain("unavailable");
    }

    static FinancialReadScope Scope(int id)=>new() { PortfolioId=id,FundId=id,Access=new("test",["LedgerRead"],[id]) };
    static ServiceResult<FinancialRead<T>> Ok<T>(T value,long revision) where T:class=>new ServiceOk<FinancialRead<T>>(new(FinancialReadStatus.Found,value,revision,DateTime.UtcNow));
    static IPortfolioFinancialApi Api()
    {
        var api=Substitute.For<IPortfolioFinancialApi>();
        api.GetAccountBalancesAsync(Arg.Any<FinancialReadScope>(),Arg.Any<GetAccountBalancesRequest>(),Arg.Any<CancellationToken>())
            .Returns(Ok(new FinancialBalanceSnapshot(1,"Active",[],0,100,true),1));
        api.GetFundTransactionsPageAsync(Arg.Any<FinancialReadScope>(),Arg.Any<GetFundTransactionsPageRequest>(),Arg.Any<CancellationToken>())
            .Returns(Ok(new FinancialPage<FinancialTransactionRow>([],null,1),1));
        api.GetFundReservationsPageAsync(Arg.Any<FinancialReadScope>(),Arg.Any<GetFundReservationsPageRequest>(),Arg.Any<CancellationToken>())
            .Returns(Ok(new FinancialPage<FinancialReservationView>([],null,1),1));
        return api;
    }
}
