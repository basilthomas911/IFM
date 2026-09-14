using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using System.Security.Cryptography;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.PortfolioDb.OrderComposition;
using TomasAI.IFM.Application.Storage.PortfolioDb.Schema;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.OrderComposition.Model;
using TomasAI.IFM.Domain.Portfolio.Identity;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence;

[Trait("Category","PortfolioFinancial"),Trait("Gate","PPG-08")]
[Collection("PortfolioFinancialDatabase")]
public sealed class PortfolioOrderCompositionIntegrationTests(PortfolioEventStoreFixture fixture) : IClassFixture<PortfolioEventStoreFixture>
{
    static readonly CatalogKey DeploymentKey = new(StrategyCatalogKind.Deployment,
        Guid.Parse("22222222-2222-2222-2222-222222222222"), 1);

    [Fact]
    public async Task Eligible_funds_orders_completion_event_and_receipt_commit_once()
    {
        _=fixture;
        await InitializePortfolioSchema();
        var book=await CreateOrderBook(value=>value with
        {
            Funds=[value.Funds[0],value.Funds[0] with { FundId=value.Funds[0].FundId+1,CanSpend=false }]
        });
        var request=Request(book);
        var store=new PortfolioOrderCompositionStore(Transactions());

        var first=await EvaluateAsync(store,request);
        var duplicate=await store.EvaluateAsync(request,(_,_,_,_,_)=>throw new InvalidOperationException("Duplicate must not reevaluate."));

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
        first.Receipt.CapacityEffects.Should().ContainSingle();
        var capacity=await Transactions().ExecuteAsync(async(db,ct)=>(
            Effects:await db.ScalarAsync("SELECT count(*) FROM portfolio.accepted_trade_order_capacity WHERE order_id=$1;",[first.Receipt.TradeOrders[0].Id.OrderId],ct),
            Working:await db.ScalarAsync("SELECT coalesce(sum(working),0) FROM portfolio_financial.capacity_usage WHERE portfolio_id=$1 AND scope_kind=$2 AND scope_key=$3;",
                [book.PortfolioId,(int)CapacityScopeKind.Fund,FinancialScopeKeys.Fund(book.Funds[0].FundId)],ct)));
        Convert.ToInt64(capacity.Effects).Should().BeGreaterThan(0L);
        Convert.ToDecimal(capacity.Working).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task No_eligible_funds_commits_one_no_trade_decision_and_no_orders()
    {
        _=fixture;
        await InitializePortfolioSchema();
        var book=await CreateOrderBook(value=>value with
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
        var book=await CreateOrderBook();
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
        var book=await CreateOrderBook();
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
        var book=await CreateOrderBook();
        var baselineRevision=Convert.ToInt64(await Transactions().ExecuteAsync((db,ct)=>db.ScalarAsync(
            "SELECT financial_revision FROM portfolio_financial.financial_authority WHERE portfolio_id=$1;",[book.PortfolioId],ct)));
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
            .EvaluateAsync(request,async(command,authority,revision,financial,token)=>
            {
                var receipt=await Evaluate(command,authority,revision,financial,token);
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
        Convert.ToInt64(counts.Revision).Should().Be(baselineRevision);
    }

    [Fact]
    public async Task Capacity_write_failure_rolls_back_usage_orders_event_receipt_and_revision()
    {
        _=fixture;
        await InitializePortfolioSchema();
        var book=await CreateOrderBook();
        var request=Request(book);
        var before=await Transactions().ExecuteAsync(async(db,ct)=>(
            Revision:Convert.ToInt64(await db.ScalarAsync("SELECT financial_revision FROM portfolio_financial.financial_authority WHERE portfolio_id=$1;",[book.PortfolioId],ct)),
            Working:Convert.ToDecimal(await db.ScalarAsync("SELECT coalesce(sum(working),0) FROM portfolio_financial.capacity_usage WHERE portfolio_id=$1;",[book.PortfolioId],ct))));

        await FluentActions.Awaiting(()=>new PortfolioOrderCompositionStore(Transactions())
            .EvaluateAsync(request,async(command,authority,revision,financial,token)=>
            {
                var receipt=await Evaluate(command,authority,revision,financial,token);
                var effect=receipt.CapacityEffects.Single();
                return receipt with { CapacityEffects=[effect with { Exposures=[effect.Exposures[0],effect.Exposures[0]] }] };
            })).Should().ThrowAsync<PostgresException>();

        var after=await Transactions().ExecuteAsync(async(db,ct)=>(
            Decisions:Convert.ToInt64(await db.ScalarAsync("SELECT count(*) FROM portfolio.order_composition_decision WHERE operation_id=$1;",[request.OperationId],ct)),
            Orders:Convert.ToInt64(await db.ScalarAsync("SELECT count(*) FROM portfolio.accepted_trade_order WHERE operation_id=$1;",[request.OperationId],ct)),
            Effects:Convert.ToInt64(await db.ScalarAsync("SELECT count(*) FROM portfolio.accepted_trade_order_capacity WHERE portfolio_id=$1 AND financial_revision>$2;",[book.PortfolioId,before.Revision],ct)),
            Events:Convert.ToInt64(await db.ScalarAsync("SELECT count(*) FROM event_log WHERE commandid=$1;",[request.CommandId],ct)),
            Receipts:Convert.ToInt64(await db.ScalarAsync("SELECT count(*) FROM portfolio_financial.financial_operation_receipt WHERE portfolio_id=$1 AND operation_id=$2;",[book.PortfolioId,request.OperationId],ct)),
            Revision:Convert.ToInt64(await db.ScalarAsync("SELECT financial_revision FROM portfolio_financial.financial_authority WHERE portfolio_id=$1;",[book.PortfolioId],ct)),
            Working:Convert.ToDecimal(await db.ScalarAsync("SELECT coalesce(sum(working),0) FROM portfolio_financial.capacity_usage WHERE portfolio_id=$1;",[book.PortfolioId],ct))));
        after.Decisions.Should().Be(0);after.Orders.Should().Be(0);after.Effects.Should().Be(0);
        after.Events.Should().Be(0);after.Receipts.Should().Be(0);
        after.Revision.Should().Be(before.Revision);after.Working.Should().Be(before.Working);
    }

    [Fact]
    public async Task Accepted_close_allocates_one_new_order_retains_trade_identity_and_commits_once()
    {
        _ = fixture;
        await InitializePortfolioSchema();
        var book = await CreateOrderBook();
        var opening = await EvaluateAsync(new PortfolioOrderCompositionStore(Transactions()), Request(book));
        var openingOrder = opening.Receipt.TradeOrders.Single();
        var openingComponent = openingOrder.Components.Single();
        var positionId = new StrategyPositionId(new TradeEntityId(
            openingOrder.Id.PortfolioId, openingOrder.Id.FundId, openingOrder.Id.OrderId,
            openingComponent.ReservedTradeId), Guid.NewGuid());
        var now = DateTime.UtcNow;
        var workflowId = new TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow.ExitPositionWorkflowId(
            positionId, DateOnly.FromDateTime(now), Guid.NewGuid());
        var operationId = Guid.NewGuid();
        var close = new EvaluatePortfolioCloseOrderCompositionCommand
        {
            CommandId = operationId,
            OperationId = operationId,
            PortfolioId = book.PortfolioId,
            EntityId = new(book.PortfolioId, operationId),
            Subject = new(ActorType.Function, EvaluatePortfolioCloseOrderCompositionCommand.Actor,
                EvaluatePortfolioCloseOrderCompositionCommand.Verb,
                new FinancialExecutionId(book.PortfolioId, operationId).Format()),
            CorrelationId = Guid.NewGuid(),
            CausationId = Guid.NewGuid(),
            RequestedAtUtc = now,
            ExpiresAtUtc = now.AddMinutes(2),
            Access = new("integration", ["OrderCompositionClose"], [book.PortfolioId]),
            Body = new()
            {
                CompositionId = Guid.NewGuid(),
                WorkflowId = workflowId,
                StrategyKind = openingComponent.StrategyKind,
                ValueDate = workflowId.ValueDate,
                ValidUntilUtc = now.AddMinutes(1),
                Origin = "FuturesExitPositionWorkflow",
                EvidenceHash = new('c', 64),
                PositionType = TradeOrderPositionType.Closing,
                Position = new()
                {
                    Id = positionId,
                    StrategyKind = openingComponent.StrategyKind,
                    Phase = StrategyPositionPhase.MarkToMarket,
                    PositionSequence = 5,
                    RouteGeneration = 1,
                    AsOfUtc = now,
                    IsOpen = true,
                    Legs = openingComponent.Legs.Select(leg => new StrategyPositionLeg
                    {
                        TradeLegId = leg.TradeLegId,
                        ContractId = leg.ContractId,
                        ContractKey = leg.ContractKey,
                        AssetFamily = leg.AssetFamily,
                        SignedQuantity = leg.SignedQuantity,
                        OpeningPrice = 100,
                        CurrentPrice = 101,
                        LastSourceSequence = 5,
                        LastPriceAtUtc = now,
                        Expiry = leg.Expiry,
                        Strike = leg.Strike,
                        PutCall = leg.PutCall
                    }).ToArray()
                },
                Component = openingComponent with
                {
                    Legs = openingComponent.Legs.Select(leg => leg with
                    {
                        SignedQuantity = -leg.SignedQuantity
                    }).ToArray()
                }
            }
        };
        close = close with { InputSha256 = FinancialCanonicalHash.Request(close) };
        var store = new PortfolioCloseOrderCompositionStore(Transactions());

        var first = await store.EvaluateAsync(close, (request, authority, revision, order, token) =>
            PortfolioCloseOrderCompositionModel.EvaluateAsync(request, authority, revision, order,
                new TestIdentityAllocator(request.OperationId), token));
        var duplicate = await store.EvaluateAsync(close, (_, _, _, _, _) =>
            throw new InvalidOperationException("Duplicate close must not reevaluate."));

        duplicate.Id.Should().Be(first.Id);
        first.Receipt.TradeOrder!.PositionType.Should().Be(TradeOrderPositionType.Closing);
        first.Receipt.TradeOrder.TargetPositionId.Should().Be(positionId);
        first.Receipt.TradeOrder.Id.OrderId.Should().NotBe(openingOrder.Id.OrderId);
        first.Receipt.TradeOrder.Components.Single().ReservedTradeId
            .Should().Be(openingComponent.ReservedTradeId);
        var counts = await Transactions().ExecuteAsync(async (db, token) => (
            CloseRows: await db.ScalarAsync(
                "SELECT count(*) FROM portfolio.accepted_position_close WHERE operation_id=$1;",
                [operationId], token),
            Events: await db.ScalarAsync("SELECT count(*) FROM event_log WHERE commandid=$1;",
                [close.CommandId], token)));
        counts.CloseRows.Should().Be(1L);
        counts.Events.Should().Be(1L);
    }

    static PostgresEventTransaction Transactions()=>GeneralLedgerPostingIntegrationTests.Transactions();

    static Task<PortfolioOrderCompositionCompletedEvent> EvaluateAsync(
        PortfolioOrderCompositionStore store,EvaluatePortfolioOrderCompositionCommand request) =>
        store.EvaluateAsync(request,Evaluate);

    static ValueTask<PortfolioOrderCompositionReceipt> Evaluate(
        EvaluatePortfolioOrderCompositionCommand request,FinancialBookConfiguration book,long revision,
        IReadOnlyList<PortfolioFundFinancialSnapshot> financial,CancellationToken cancellationToken) => PortfolioOrderCompositionModel.EvaluateAsync(
            request,book,revision,financial,new TestIdentityAllocator(request.OperationId),cancellationToken);

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
                PositionType=TradeOrderPositionType.Opening,
                StrategyKind=TradeStrategyKind.FuturesOutright,ValueDate=DateOnly.FromDateTime(now),
                ValidUntilUtc=now.AddMinutes(1),Origin="StrategyWorkflow",EvidenceHash=new('b',64),
                RequiredCapital=1000,MaximumLoss=1000,StressLoss=1000,Notional=10000,
                ProductSymbol="ES",ProductExchange="XCME",ProductCurrency="USD",
                DeploymentKey=DeploymentKey,Components=[new TradeOrderComponentDefinition
                {
                    ComponentId=Guid.NewGuid(),StrategyKind=TradeStrategyKind.FuturesOutright,
                    Legs=[new TradeLegDefinition { TradeLegId=Guid.NewGuid(),ContractId="ESZ6",
                        AssetFamily=TradeAssetFamily.Futures,SignedQuantity=1,ContractKey="ESZ6" }]
                }]
            }
        };
        return request with { InputSha256=FinancialCanonicalHash.Request(request) };
    }

    internal static async Task<FinancialBookConfiguration> CreateOrderBook(
        Func<FinancialBookConfiguration,FinancialBookConfiguration>? configure = null)
    {
        var book = await GeneralLedgerPostingIntegrationTests.CreateBook(value =>
        {
            value = value with
            {
                Funds = value.Funds.Select(fund => fund with
                {
                    Limits = Limits(CapacityScopeKind.Portfolio,FinancialScopeKeys.Portfolio(value.PortfolioId))
                        .Concat(Limits(CapacityScopeKind.Fund,FinancialScopeKeys.Fund(fund.FundId))).ToArray(),
                    Deployments = [new FinancialDeploymentAuthority(
                        fund.Reference with { DeploymentKey = DeploymentKey },
                        Limits(CapacityScopeKind.Deployment,FinancialScopeKeys.Deployment(DeploymentKey)), 100_000)]
                }).ToArray()
            };
            return configure?.Invoke(value) ?? value;
        });
        await GeneralLedgerPostingIntegrationTests.Post(GeneralLedgerPostingIntegrationTests.Request(
            book,LedgerTransactionKind.DepositConfirmed,1_000_000,0));
        return book;
    }

    static CapacityLimit[] Limits(CapacityScopeKind scope,string key) =>
    [
        new(scope,key,CapacityMeasure.SettlementCash,CapacityUnit.Usd,1_000_000),
        new(scope,key,CapacityMeasure.LossCharge,CapacityUnit.Usd,1_000_000),
        new(scope,key,CapacityMeasure.Margin,CapacityUnit.Usd,1_000_000),
        new(scope,key,CapacityMeasure.GrossNotional,CapacityUnit.Usd,10_000_000),
        new(scope,key,CapacityMeasure.PositionSlots,CapacityUnit.Positions,100),
        new(scope,key,CapacityMeasure.GrossContracts,CapacityUnit.Contracts,100)
    ];

    internal sealed class TestIdentityAllocator : IPortfolioBusinessIdAllocator
    {
        int nextOrder;
        int nextTrade;

        public TestIdentityAllocator(Guid operationId)
        {
            var hash = SHA256.HashData(operationId.ToByteArray());
            var seed = BitConverter.ToInt32(hash, 0) & 0x1fffffff;
            nextOrder = 500_000_000 + seed;
            nextTrade = 1_100_000_000 + seed;
        }

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
