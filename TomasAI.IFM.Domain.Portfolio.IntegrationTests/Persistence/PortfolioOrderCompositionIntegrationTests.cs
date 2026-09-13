using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.PortfolioDb.OrderComposition;
using TomasAI.IFM.Application.Storage.PortfolioDb.Schema;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.OrderComposition.Model;
using TomasAI.IFM.Domain.Portfolio.Identity;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared.Model;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence;

[Trait("Category","PortfolioFinancial"),Trait("Gate","PPG-08")]
[Collection("PortfolioFinancialDatabase")]
public sealed class PortfolioOrderCompositionIntegrationTests(PortfolioEventStoreFixture fixture) : IClassFixture<PortfolioEventStoreFixture>
{
    [Fact]
    public async Task Eligible_funds_orders_completion_event_and_receipt_commit_once()
    {
        _=fixture;
        await InitializePortfolioSchema();
        var book=await GeneralLedgerPostingIntegrationTests.CreateBook(value=>value with
        {
            Funds=[value.Funds[0],value.Funds[0] with { FundId=value.Funds[0].FundId+1,CanSpend=false }]
        });
        var request=Request(book);
        var store=new PortfolioOrderCompositionStore(Transactions());

        var first=await EvaluateAsync(store,request);
        var duplicate=await store.EvaluateAsync(request,(_,_,_,_)=>throw new InvalidOperationException("Duplicate must not reevaluate."));

        duplicate.Id.Should().Be(first.Id);
        first.Receipt.Status.Should().Be(PortfolioOrderCompositionStatus.ExecuteTradeOrders);
        first.Receipt.TradeOrders.Should().ContainSingle().Which.Id.FundId.Should().Be(book.Funds[0].FundId);
        first.Receipt.TradeOrders.Should().OnlyContain(order => order.Id.OrderId > 0
            && order.Components.All(component => component.ReservedTradeId > 0));
        first.Receipt.FundDecisions.Should().HaveCount(2);
        var counts=await Transactions().ExecuteAsync(async(db,ct)=>(
            Decisions:await db.ScalarAsync("SELECT count(*) FROM portfolio.order_composition_decision WHERE operation_id=$1;",[request.OperationId],ct),
            Orders:await db.ScalarAsync("SELECT count(*) FROM portfolio.accepted_trade_order WHERE operation_id=$1;",[request.OperationId],ct),
            Events:await db.ScalarAsync("SELECT count(*) FROM event_log WHERE commandid=$1;",[request.CommandId],ct)));
        counts.Decisions.Should().Be(1L);counts.Orders.Should().Be(1L);counts.Events.Should().Be(1L);
    }

    [Fact]
    public async Task No_eligible_funds_commits_one_no_trade_decision_and_no_orders()
    {
        _=fixture;
        await InitializePortfolioSchema();
        var book=await GeneralLedgerPostingIntegrationTests.CreateBook(value=>value with
            { Funds=value.Funds.Select(fund=>fund with { CanSpend=false }).ToArray() });
        var request=Request(book);

        var completed=await EvaluateAsync(new PortfolioOrderCompositionStore(Transactions()),request);

        completed.Receipt.Status.Should().Be(PortfolioOrderCompositionStatus.NoTradeOrders);
        completed.Receipt.TradeOrders.Should().BeEmpty();
        var orders=await Transactions().ExecuteAsync((db,ct)=>db.ScalarAsync(
            "SELECT count(*) FROM portfolio.accepted_trade_order WHERE operation_id=$1;",[request.OperationId],ct));
        orders.Should().Be(0L);
    }

    [Fact]
    public async Task Conflicting_retry_is_rejected_without_changing_the_original_completion()
    {
        _=fixture;
        await InitializePortfolioSchema();
        var book=await GeneralLedgerPostingIntegrationTests.CreateBook();
        var request=Request(book);
        var store=new PortfolioOrderCompositionStore(Transactions());
        var original=await EvaluateAsync(store,request);
        var changed=request with { Body=request.Body with { Origin="Changed" } };
        changed=changed with { InputSha256=FinancialCanonicalHash.Request(changed) };

        var error=await FluentActions.Awaiting(()=>EvaluateAsync(store,changed))
            .Should().ThrowAsync<FinancialOperationException>();

        error.Which.Code.Should().Be(FinancialReasons.RequestMismatch);
        (await new PortfolioFinancialStore(Transactions()).ReadOperationAsync<PortfolioOrderCompositionCompletedEvent>(
            book.PortfolioId,request.OperationId,request.InputSha256))!.Id.Should().Be(original.Id);
    }

    [Fact]
    public async Task Expired_request_rolls_back_decision_orders_event_and_receipt()
    {
        _=fixture;
        await InitializePortfolioSchema();
        var book=await GeneralLedgerPostingIntegrationTests.CreateBook();
        var request=Request(book) with { ExpiresAtUtc=DateTime.UtcNow.AddSeconds(-1) };
        request=request with { InputSha256=FinancialCanonicalHash.Request(request) };

        await FluentActions.Awaiting(()=>new PortfolioOrderCompositionStore(Transactions())
            .EvaluateAsync(request,Evaluate)).Should().ThrowAsync<TimeoutException>();

        var counts=await Transactions().ExecuteAsync(async(db,ct)=>(
            Decisions:await db.ScalarAsync("SELECT count(*) FROM portfolio.order_composition_decision WHERE operation_id=$1;",[request.OperationId],ct),
            Events:await db.ScalarAsync("SELECT count(*) FROM event_log WHERE commandid=$1;",[request.CommandId],ct),
            Receipts:await db.ScalarAsync("SELECT count(*) FROM portfolio_financial.financial_operation_receipt WHERE portfolio_id=$1 AND operation_id=$2;",[book.PortfolioId,request.OperationId],ct)));
        counts.Decisions.Should().Be(0L);counts.Events.Should().Be(0L);counts.Receipts.Should().Be(0L);
    }

    [Fact]
    public async Task Constraint_failure_after_business_writes_rolls_back_every_row_event_receipt_and_revision()
    {
        _=fixture;
        await InitializePortfolioSchema();
        var book=await GeneralLedgerPostingIntegrationTests.CreateBook();
        var request=Request(book);
        var duplicateLeg=request.Body.Components[0].Legs[0];
        request=request with
        {
            Body=request.Body with
            {
                Components=[request.Body.Components[0] with { Legs=[duplicateLeg,duplicateLeg] }]
            }
        };
        request=request with { InputSha256=FinancialCanonicalHash.Request(request) };

        var attemptedOrderId=0;
        await FluentActions.Awaiting(()=>new PortfolioOrderCompositionStore(Transactions())
            .EvaluateAsync(request,async(command,authority,revision,token)=>
            {
                var receipt=await Evaluate(command,authority,revision,token);
                attemptedOrderId=receipt.TradeOrders[0].Id.OrderId;
                return receipt;
            })).Should().ThrowAsync<PostgresException>();

        var counts=await Transactions().ExecuteAsync(async(db,ct)=>(
            Decisions:await db.ScalarAsync("SELECT count(*) FROM portfolio.order_composition_decision WHERE operation_id=$1;",[request.OperationId],ct),
            FundDecisions:await db.ScalarAsync("SELECT count(*) FROM portfolio.order_composition_fund_decision WHERE operation_id=$1;",[request.OperationId],ct),
            Orders:await db.ScalarAsync("SELECT count(*) FROM portfolio.accepted_trade_order WHERE operation_id=$1;",[request.OperationId],ct),
            Legs:await db.ScalarAsync("SELECT count(*) FROM portfolio.accepted_trade_order_leg WHERE order_id=$1;",[attemptedOrderId],ct),
            Events:await db.ScalarAsync("SELECT count(*) FROM event_log WHERE commandid=$1;",[request.CommandId],ct),
            Receipts:await db.ScalarAsync("SELECT count(*) FROM portfolio_financial.financial_operation_receipt WHERE portfolio_id=$1 AND operation_id=$2;",[book.PortfolioId,request.OperationId],ct),
            Revision:await db.ScalarAsync("SELECT financial_revision FROM portfolio_financial.financial_authority WHERE portfolio_id=$1;",[book.PortfolioId],ct)));
        counts.Decisions.Should().Be(0L);
        counts.FundDecisions.Should().Be(0L);
        counts.Orders.Should().Be(0L);
        counts.Legs.Should().Be(0L);
        counts.Events.Should().Be(0L);
        counts.Receipts.Should().Be(0L);
        counts.Revision.Should().Be(0L);
    }

    static PostgresEventTransaction Transactions()=>GeneralLedgerPostingIntegrationTests.Transactions();

    static Task<PortfolioOrderCompositionCompletedEvent> EvaluateAsync(
        PortfolioOrderCompositionStore store,EvaluatePortfolioOrderCompositionCommand request) =>
        store.EvaluateAsync(request,Evaluate);

    static ValueTask<PortfolioOrderCompositionReceipt> Evaluate(
        EvaluatePortfolioOrderCompositionCommand request,FinancialBookConfiguration book,long revision,
        CancellationToken cancellationToken) => PortfolioOrderCompositionModel.EvaluateAsync(
            request,book,revision,new TestIdentityAllocator(),cancellationToken);

    internal static async Task InitializePortfolioSchema()
    {
        var settings=new DbConnectionSettings().Add(PortfolioDbContext.PortfolioDbConnection,
            Environment.GetEnvironmentVariable("IFM_POSTGRES_EVENTSOURCE_TEST_CONNECTION")
                ?? "Host=localhost;Port=5432;Database=event-source-test-db","System.Data.Postgres");
        await new PortfolioSchemaDb(settings,NullLogger<DbProvider>.Instance).CreateAllAsync();
    }

    internal static EvaluatePortfolioOrderCompositionCommand Request(FinancialBookConfiguration book)
    {
        var now=DateTime.UtcNow;var operation=Guid.NewGuid();
        var request=new EvaluatePortfolioOrderCompositionCommand
        {
            CommandId=Guid.NewGuid(),OperationId=operation,PortfolioId=book.PortfolioId,
            EntityId=new(book.PortfolioId,operation),
            Subject=new(ActorType.Function,EvaluatePortfolioOrderCompositionCommand.Actor,
                EvaluatePortfolioOrderCompositionCommand.Verb,new FinancialExecutionId(book.PortfolioId,operation).Format()),
            CorrelationId=Guid.NewGuid(),CausationId=Guid.NewGuid(),RequestedAtUtc=now,ExpiresAtUtc=now.AddMinutes(2),
            ExpectedFinancialRevision=0,Access=new("integration",["PortfolioAdministrator"]),
            Body=new()
            {
                CompositionId=Guid.NewGuid(),WorkflowId=Guid.NewGuid(),DecisionHorizon="Daily",
                StrategyKind=TradeStrategyKind.FuturesOutright,ValueDate=DateOnly.FromDateTime(now),
                ValidUntilUtc=now.AddMinutes(1),Origin="StrategyWorkflow",EvidenceHash=new('b',64),
                RequiredCapital=1000,Components=[new TradeOrderComponentDefinition
                {
                    ComponentId=Guid.NewGuid(),StrategyKind=TradeStrategyKind.FuturesOutright,
                    Legs=[new TradeLegDefinition { TradeLegId=Guid.NewGuid(),MarketInstrumentId=42,
                        AssetFamily=TradeAssetFamily.Futures,SignedQuantity=1,ContractKey="ES" }]
                }]
            }
        };
        return request with { InputSha256=FinancialCanonicalHash.Request(request) };
    }

    internal sealed class TestIdentityAllocator : IPortfolioBusinessIdAllocator
    {
        static int nextOrder=1_000_000;
        static int nextTrade=2_000_000;
        public ValueTask<int> AllocateOrderIdAsync(CancellationToken cancellationToken=default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Interlocked.Increment(ref nextOrder));
        }
        public ValueTask<int> AllocateTradeIdAsync(CancellationToken cancellationToken=default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Interlocked.Increment(ref nextTrade));
        }
        public ValueTask<Domain.Portfolio.Shared.Identities.PortfolioId> AllocatePortfolioIdAsync(
            CancellationToken cancellationToken=default)=>throw new NotSupportedException();
        public ValueTask<int> AllocateFundIdAsync(CancellationToken cancellationToken=default)=>throw new NotSupportedException();
    }
}
