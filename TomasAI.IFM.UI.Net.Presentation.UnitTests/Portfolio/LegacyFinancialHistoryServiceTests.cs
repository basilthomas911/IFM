using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.ServiceApi;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Services.Fund;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.Portfolio;

[Trait("Category","PortfolioFinancial")]
public sealed class LegacyFinancialHistoryServiceTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Legacy_read_rejects_foreign_funds_or_dates_without_presenting_them_as_selected_history(bool foreignFund)
    {
        var api=Substitute.For<IFundQueryApi>();var day=new DateOnly(2026,9,9);
        var row=new FundTransactionReadModel(1,DateTime.UtcNow,FundTransactionType.CashDeposit,foreignFund?2:1,0,0,default,
            foreignFund?day:day.AddDays(1),default,"Original",0.123456789m,99999);
        api.GetFundTransactionsAsync(1,day,day).Returns(new ServiceOk<FundTransactionReadModel[]>([row]));
        await FluentActions.Awaiting(()=>new FundQueryService(api).GetLegacyTransactionsAsync(1,day,day,default)).Should().ThrowAsync<InvalidOperationException>();
    }
    [Fact]
    public async Task Cancelled_navigation_stops_observation_of_an_outstanding_legacy_read()
    {
        var api=Substitute.For<IFundQueryApi>();var day=new DateOnly(2026,9,9);
        var completion=new TaskCompletionSource<ServiceResult<FundTransactionReadModel[]>>(TaskCreationOptions.RunContinuationsAsynchronously);
        api.GetFundTransactionsAsync(1,day,day).Returns(completion.Task);
        using var cancellation=new CancellationTokenSource();
        var pending=new FundQueryService(api).GetLegacyTransactionsAsync(1,day,day,cancellation.Token);cancellation.Cancel();
        await FluentActions.Awaiting(()=>pending).Should().ThrowAsync<OperationCanceledException>();
        completion.TrySetResult(new ServiceOk<FundTransactionReadModel[]>([]));
        pending.IsCanceled.Should().BeTrue();
    }
}
