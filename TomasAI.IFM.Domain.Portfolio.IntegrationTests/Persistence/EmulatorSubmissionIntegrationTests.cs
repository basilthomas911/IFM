using FluentAssertions;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using static TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence.GeneralLedgerPostingIntegrationTests;
using static TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence.CapacityReservationIntegrationTests;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence;

[Collection("PortfolioFinancialDatabase"),Trait("Category","PortfolioFinancial"),Trait("Gate","PF-FIN-05")]
public sealed class EmulatorSubmissionIntegrationTests(PortfolioEventStoreFixture fixture):IClassFixture<PortfolioEventStoreFixture>
{
    [Fact]
    public async Task Two_independent_submitters_and_restarted_adapter_return_one_durable_broker_order()
    {
        _=fixture; var request=await Request();
        var results=await Task.WhenAll(new EmulatorExecutionStore(Transactions()).SubmitAsync(request),new EmulatorExecutionStore(Transactions()).SubmitAsync(request));
        results[0].Id.Should().Be(results[1].Id);
        var replay=await new EmulatorExecutionStore(Transactions()).SubmitAsync(request);
        replay.Receipt.BrokerOrderReference.Should().Be(results[0].Receipt.BrokerOrderReference);
        var count=await Transactions().ExecuteAsync((db,ct)=>db.ScalarAsync("SELECT count(*) FROM portfolio_financial.emulator_order WHERE execution_id=$1;",[request.Body.Order.ExecutionId],ct));
        Convert.ToInt64(count).Should().Be(1);
        var query=await new FinancialQueryStore(Transactions()).ReadAsync(new FinancialReadScope
        { PortfolioId=request.PortfolioId,FundId=request.Body.Order.FundId,Access=request.Access },new GetPostingReceiptRequest(request.OperationId));
        query.Value!.EmulatorSubmission!.Id.Should().Be(replay.Id);
    }
    [Theory]
    [InlineData("units")] [InlineData("price")] [InlineData("side")]
    [InlineData("consume")] [InlineData("environment")]
    public async Task Changed_order_or_missing_consumption_never_creates_an_emulator_order(string change)
    {
        var request=await Request(); var order=request.Body.Order;
        order=change switch
        {
            "units"=>order with { StrategyUnits=order.StrategyUnits+1 },
            "price"=>order with { SignedDebitPerUnit=order.SignedDebitPerUnit+1 },
            "side"=>order with { Legs=[order.Legs[0] with { Side="Sell" }] },
            "environment"=>order with { Environment="Live" },
            _=>order
        };
        order=order with { ContentHash=order.Hash() };
        request=request with { Body=request.Body with { Order=order,
            ConsumptionCompletedEventId=change=="consume"?Guid.NewGuid():request.Body.ConsumptionCompletedEventId } };
        request=request with { InputSha256=FinancialCanonicalHash.Request(request) };
        await FluentActions.Awaiting(()=>new EmulatorExecutionStore(Transactions()).SubmitAsync(request)).Should().ThrowAsync<FinancialOperationException>();
        var count=await Transactions().ExecuteAsync((db,ct)=>db.ScalarAsync("SELECT count(*) FROM portfolio_financial.emulator_order WHERE execution_id=$1;",[request.Body.Order.ExecutionId],ct));
        Convert.ToInt64(count).Should().Be(0);
    }
    static async Task<SubmitEmulatorOrderCommand> Request()
    {
        var book=await CreateBook(b=>b with { Environment="Emulator",Funds=[b.Funds[0] with
        { Limits=[new(CapacityScopeKind.Portfolio,b.PortfolioId.ToString(),CapacityMeasure.LossCharge,CapacityUnit.Usd,1000)] }] });
        await Post(GeneralLedgerPostingIntegrationTests.Request(book,LedgerTransactionKind.DepositConfirmed,1000,0));
        var reserve=await ReserveRequest(book,700); await Reserve(reserve);
        var consume=ConsumeRequest(reserve,2);
        var order=new FinancialExecutionOrder
        {
            ExecutionId=consume.Body.ExecutionId,PortfolioId=book.PortfolioId,FundId=reserve.Body.FundId,BookId=book.BookId,
            OrderId=reserve.Body.OrderId,ReservationId=reserve.Body.ReservationId,SizedOrderHash=reserve.Body.SizedOrderHash,
            StrategyUnits=10,Environment="Emulator",SignedDebitPerUnit=6000,EntryFees=50,ValidUntilUtc=reserve.Body.ValidUntilUtc,
            Legs=[new(1,"ES-test","ES-test","F","Buy",10,50)]
        };
        order=order with { ContentHash=order.Hash() };
        await Accept(reserve,consume,order.ContentHash); var consumed=await Consume(consume);
        var operation=Guid.NewGuid(); var id=new LedgerPortfolioId(book.PortfolioId);
        var request=new SubmitEmulatorOrderCommand
        {
            CommandId=operation,OperationId=operation,EntityId=id,PortfolioId=book.PortfolioId,
            Subject=new(ActorType.Command,SubmitEmulatorOrderCommand.Actor,SubmitEmulatorOrderCommand.Verb,id.Format()),
            CorrelationId=Guid.NewGuid(),CausationId=consumed.Id,RequestedAtUtc=DateTime.UtcNow,ExpiresAtUtc=order.ValidUntilUtc,
            ExpectedFinancialRevision=consumed.Receipt.FinancialRevision,Access=new("integration",["PortfolioAdministrator"]),
            Body=new(order,consume.OperationId,consumed.Id,consume.InputSha256)
        };
        return request with { InputSha256=FinancialCanonicalHash.Request(request) };
    }
}
