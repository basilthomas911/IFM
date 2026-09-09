using FluentAssertions;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.Command.Model;
using TomasAI.IFM.Domain.Portfolio.Persistence;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using static TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence.GeneralLedgerPostingIntegrationTests;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence;

[Collection("PortfolioFinancialDatabase"),Trait("Category","PortfolioFinancial"),Trait("Gate","PF-FIN-04")]
public sealed class PortfolioAuthorityFenceTests(PortfolioEventStoreFixture fixture):IClassFixture<PortfolioEventStoreFixture>
{
    [Fact]
    public async Task Real_portfolio_event_writer_invalidates_financial_authority_in_the_same_transaction()
    {
        var book=await CapacityReservationIntegrationTests.FundedBook();
        var events=new PortfolioEventStore(fixture.EventSourceDb,new PortfolioAuthorityFence(Transactions()));
        var changed=new PortfolioOperatingStateChanged(Guid.NewGuid(),Guid.NewGuid(),2,DateTime.UtcNow,"integration",PortfolioOperatingState.Disabled,"test revocation");
        await events.AppendPortfolioAsync(new(book.PortfolioId),changed,1);
        var snapshot=await new FinancialQueryStore(Transactions()).ReadAsync(new FinancialReadScope { PortfolioId=book.PortfolioId,Access=new("integration",["PortfolioAdministrator"]) },new GetAccountBalancesRequest());
        snapshot.FinancialRevision.Should().Be(2); snapshot.Value!.OperatingState.Should().Be("NeedsRefresh");
        var request=await CapacityReservationIntegrationTests.ReserveRequest(book,700);
        request=request with { ExpectedFinancialRevision=2 };
        var error=await FluentActions.Awaiting(()=>CapacityReservationIntegrationTests.Reserve(request)).Should().ThrowAsync<FinancialOperationException>();
        error.Which.Code.Should().Be(FinancialReasons.AuthorityDenied);
    }

    [Fact]
    public async Task Order_status_event_advances_fund_source_fence_without_revoking_unchanged_mandate()
    {
        var book=await CapacityReservationIntegrationTests.FundedBook();
        var events=new PortfolioEventStore(fixture.EventSourceDb,new PortfolioAuthorityFence(Transactions()));
        var changed=new FundCompositionStateChanged(Guid.NewGuid(),Guid.NewGuid(),2,DateTime.UtcNow,"integration",
            new() { PortfolioId=book.PortfolioId,FundId=book.Funds[0].FundId,OrderId=123,Status="Composed" });
        await events.AppendFundAsync(new PortfolioFundId(book.PortfolioId,book.Funds[0].FundId),changed,1);
        var refreshed=await new PortfolioFinancialDbContext(Transactions()).ReadBookAsync(book.PortfolioId);
        refreshed!.Funds[0].FundStreamVersion.Should().Be(2); refreshed.Funds[0].Reference.Should().Be(book.Funds[0].Reference);
        var request=await CapacityReservationIntegrationTests.ReserveRequest(refreshed,700);
        (await CapacityReservationIntegrationTests.Reserve(request)).Receipt.FinancialRevision.Should().Be(2);
    }

    [Fact]
    public async Task Failed_configuration_compare_and_swap_cannot_invalidate_financial_state()
    {
        var book=await CapacityReservationIntegrationTests.FundedBook();
        var events=new PortfolioEventStore(fixture.EventSourceDb,new PortfolioAuthorityFence(Transactions()));
        var changed=new PortfolioOperatingStateChanged(Guid.NewGuid(),Guid.NewGuid(),10,DateTime.UtcNow,"integration",PortfolioOperatingState.Disabled,"stale write");
        await FluentActions.Awaiting(()=>events.AppendPortfolioAsync(new(book.PortfolioId),changed,9)).Should().ThrowAsync<TomasAI.IFM.Shared.Exceptions.ConcurrencyException>();
        var snapshot=await new FinancialQueryStore(Transactions()).ReadAsync(new FinancialReadScope { PortfolioId=book.PortfolioId,Access=new("integration",["PortfolioAdministrator"]) },new GetAccountBalancesRequest());
        snapshot.FinancialRevision.Should().Be(1); snapshot.Value!.OperatingState.Should().Be("Active");
    }
}
