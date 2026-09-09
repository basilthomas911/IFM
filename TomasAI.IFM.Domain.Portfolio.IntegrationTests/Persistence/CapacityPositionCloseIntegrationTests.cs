using FluentAssertions;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using static TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence.CapacityReservationIntegrationTests;
using static TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence.GeneralLedgerPostingIntegrationTests;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence;

[Collection("PortfolioFinancialDatabase"),Trait("Category","PortfolioFinancial"),Trait("Gate","PF-FIN-04")]
public sealed class CapacityPositionCloseIntegrationTests(PortfolioEventStoreFixture fixture):IClassFixture<PortfolioEventStoreFixture>
{
    [Fact]
    public async Task Real_close_releases_only_reconciled_open_units_and_replay_cannot_release_twice()
    {
        _=fixture;
        var book=await FundedBook(); var reserve=await ReserveRequest(book,700); await Reserve(reserve);
        var consume=ConsumeRequest(reserve,2); await Accept(reserve,consume); await Consume(consume);
        await Change(ChangeRequest(consume,CapacityChangeKind.RecordFill,3,2,10,0,0));
        var close=ChangeRequest(consume,CapacityChangeKind.RecordPositionClose,4,3,10,0,0);
        close=close with { Body=close.Body with { ClosedUnits=4,RelatedPostingReference="integration-close/4" } };
        close=close with { InputSha256=FinancialCanonicalHash.Request(close) };
        await FluentActions.Awaiting(()=>Change(close)).Should().ThrowAsync<FinancialOperationException>();
        (await Usage(book)).Should().Be((0m,0m,700m));
        await Reconcile(close,reserve);
        var completed=await Change(close);
        completed.Receipt.ClosedUnits.Should().Be(4); completed.Receipt.FilledUnits.Should().Be(10);
        (await Usage(book)).Should().Be((0m,0m,420m));
        (await Change(close)).Id.Should().Be(completed.Id);
        var query=new FinancialQueryStore(Transactions()); var scope=new FinancialReadScope
        { PortfolioId=book.PortfolioId,FundId=reserve.Body.FundId,Access=new("integration",["PortfolioAdministrator"]) };
        (await query.ReadAsync(scope,new GetAccountBalancesRequest())).Value!.AvailableCash.Should().Be(580);
        var full=ChangeRequest(consume,CapacityChangeKind.RecordPositionClose,5,4,10,0,0);
        full=full with { Body=full.Body with { ClosedUnits=10,RelatedPostingReference="integration-close/10" } };
        full=full with { InputSha256=FinancialCanonicalHash.Request(full) };
        await Reconcile(full,reserve); var terminal=await Change(full);
        terminal.Receipt.Status.Should().Be(ReservationStatus.Released);
        (await Usage(book)).Should().Be((0m,0m,0m));
        (await query.ReadAsync(scope,new GetAccountBalancesRequest())).Value!.AvailableCash.Should().Be(1000);
        var current=(await query.ReadAsync(scope,new GetCapacityReservationRequest(reserve.Body.ReservationId))).Value!.Current;
        current.ClosedUnits.Should().Be(10); current.FilledUnits.Should().Be(10);
    }
    static Task Reconcile(ChangeCapacityReservationCommand command,ReservePortfolioTradeRiskCommand reserve)
        =>Transactions().ExecuteAsync((db,ct)=>db.AppendAsync($"IntegrationClose.{command.CommandId:N}",command.Body.Source.SourceEventId,
            new TestCapacityReconciliationEvent
            {
                Id=Guid.NewGuid(),CommandId=command.Body.Source.SourceEventId,CapacityReconciliation=new()
                {
                    ExecutionId=command.Body.ExecutionId,ExecutionRevision=command.Body.ExecutionRevision,PortfolioId=command.PortfolioId,
                    FundId=reserve.Body.FundId,OrderId=reserve.Body.OrderId,ReservationId=reserve.Body.ReservationId,
                    FilledUnits=command.Body.FilledUnits,CancelledUnits=command.Body.CancelledUnits,ClosedUnits=command.Body.ClosedUnits,
                    SourceContentHash=command.Body.Source.SourceContentHash,FinancialFactsComplete=true,
                    ReconciliationReference=command.Body.RelatedPostingReference,Environment=reserve.Body.ExecutionEnvironment
                }
            },0,ct));
}
