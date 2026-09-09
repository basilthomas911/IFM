using FluentAssertions;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.Command.Model;
using TomasAI.IFM.Domain.Portfolio.Persistence;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using static TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence.GeneralLedgerPostingIntegrationTests;
namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence;
[Collection("PortfolioFinancialDatabase"),Trait("Category","PortfolioFinancial")]
public sealed class FundRiskTerminalIntegrationTests(PortfolioEventStoreFixture fixture):IClassFixture<PortfolioEventStoreFixture>
{
    [Theory]
    [InlineData(false,false)] [InlineData(true,false)] [InlineData(false,true)]
    public async Task Terminal_fund_update_requires_exact_committed_workflow_and_fences_delayed_reservation(bool tamper,bool held)
    {
        var book=await CreateBook(b=>b with {Environment="Emulator",AuthorityEpoch=1,Funds=[b.Funds[0] with {Reference=b.Funds[0].Reference with {AuthorityEpoch=1},Limits=[new(CapacityScopeKind.Portfolio,b.PortfolioId.ToString(),CapacityMeasure.LossCharge,CapacityUnit.Usd,1000)]}]});var fund=book.Funds[0].FundId;
        await Post(Request(book,LedgerTransactionKind.DepositConfirmed,1000,0));
        var pending=await CapacityReservationIntegrationTests.ReserveRequest(book,100);
        if(held)await CapacityReservationIntegrationTests.Reserve(pending);
        var source=new WorkflowStrategyStateUpdatedEvent {Id=Guid.NewGuid(),CommandId=Guid.NewGuid(),WorkflowId=new(Guid.NewGuid()),WorkflowRevision=9,ReceivedOn=DateTime.UtcNow,
            State=new(){CurrentStage=StrategyWorkflowStage.RiskManagement,Status=WorkflowStrategyMachineStatus.TimedOut,TerminalAtUtc=DateTime.UtcNow,
                OrderComposition=new(){Result=new(){PayloadSha256=new('A',64),CompositionResult=new(){Candidate=new(){PortfolioId=book.PortfolioId,FundId=fund,OrderId=pending.Body.OrderId}}}}}};
        await Transactions().ExecuteAsync(async(db,ct)=>{await db.AppendAsync($"RiskTerminalTest.{source.CommandId}",source.CommandId,source,0,ct);return true;});
        var evidence=source.TerminalRisk!;evidence.Should().NotBeNull();
        if(tamper)evidence=evidence with {Reason="different"};
        var changed=new FundCompositionStateChanged(Guid.NewGuid(),Guid.NewGuid(),2,DateTime.UtcNow,"test",new(){PortfolioId=book.PortfolioId,FundId=fund,OrderId=pending.Body.OrderId,Status="Expired",TerminalRisk=evidence});
        var store=new PortfolioEventStore(fixture.EventSourceDb,new PortfolioAuthorityFence(Transactions()));
        if(tamper || held)
            await FluentActions.Awaiting(()=>store.AppendFundAsync(new(book.PortfolioId,fund),changed,1)).Should().ThrowAsync<FinancialOperationException>();
        else
        {
            await store.AppendFundAsync(new(book.PortfolioId,fund),changed,1);
            var receipt=await new FinancialQueryStore(Transactions()).ReadAsync(new(){PortfolioId=book.PortfolioId,FundId=fund,Access=new("test",["LedgerRead"],[book.PortfolioId])},new GetPostingReceiptRequest(pending.OperationId));
            receipt.FinancialRevision.Should().BeGreaterThan(pending.ExpectedFinancialRevision);
            await FluentActions.Awaiting(()=>CapacityReservationIntegrationTests.Reserve(pending)).Should().ThrowAsync<FinancialOperationException>();
        }
    }
    [Fact]
    public async Task Unfenced_store_cannot_append_terminal_risk()
    {
        var evidence=new RiskTerminalEvidence(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),1,2,3,"hash","Expired",Guid.Empty,"","Expired",DateTime.UtcNow);
        var changed=new FundCompositionStateChanged(Guid.NewGuid(),Guid.NewGuid(),2,DateTime.UtcNow,"test",new(){TerminalRisk=evidence});
        await FluentActions.Awaiting(()=>new PortfolioEventStore(fixture.EventSourceDb).AppendFundAsync(new(1,2),changed,1)).Should().ThrowAsync<InvalidOperationException>();
    }
}
