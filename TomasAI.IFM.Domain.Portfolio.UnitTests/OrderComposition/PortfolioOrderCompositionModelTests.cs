using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Identity;
using TomasAI.IFM.Domain.Portfolio.OrderComposition.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

namespace TomasAI.IFM.Domain.Portfolio.UnitTests.OrderComposition;

public sealed class PortfolioOrderCompositionModelTests
{
    [Fact]
    public async Task Eligible_funds_each_receive_one_order_and_disabled_funds_are_preserved_as_decisions()
    {
        var request = Request();
        var book = Book(true, false, true);
        var result = await PortfolioOrderCompositionModel.EvaluateAsync(
            request, book, 7, Financial(book), new TestIdentityAllocator());
        result.Status.Should().Be(PortfolioOrderCompositionStatus.ExecuteTradeOrders);
        result.TradeOrders.Select(x => x.Id.FundId).Should().Equal(10, 12);
        result.FundDecisions.Should().HaveCount(3).And.ContainSingle(x => !x.Accepted && x.FundId == 11);
        result.TradeOrders.Should().OnlyContain(x => x.Id.OrderId > 0
            && x.Components.All(c => c.ReservedTradeId > 0));
        result.CapacityEffects.Should().HaveCount(2).And.OnlyContain(effect => effect.OrderId > 0
            && effect.Exposures.Length > 0 && effect.RequiredCash == 100);
    }

    [Fact]
    public async Task No_eligible_fund_returns_atomic_no_trade_result_with_empty_orders()
    {
        var identities = new TestIdentityAllocator();
        var book = Book(false, false);
        var result = await PortfolioOrderCompositionModel.EvaluateAsync(
            Request(), book, 9, Financial(book), identities);
        result.Status.Should().Be(PortfolioOrderCompositionStatus.NoTradeOrders);
        result.TradeOrders.Should().BeEmpty();
        result.FundDecisions.Should().OnlyContain(x => !x.Accepted);
        identities.Allocations.Should().Be(0);
    }

    [Fact]
    public async Task Accepted_funds_receive_sequence_allocated_order_and_trade_identities()
    {
        var identities = new TestIdentityAllocator(orderStart: 100, tradeStart: 200);
        var book = Book(true);
        var result = await PortfolioOrderCompositionModel.EvaluateAsync(
            Request(), book, 2, Financial(book), identities);
        result.TradeOrders.Single().Id.OrderId.Should().Be(101);
        result.TradeOrders.Single().Components.Single().ReservedTradeId.Should().Be(201);
        identities.Allocations.Should().Be(2);
    }

    [Fact]
    public async Task Invalid_or_unqualified_input_is_rejected_without_a_partial_result()
    {
        var invalid = Request() with { Body = Request().Body with { Components = [] } };
        await FluentActions.Awaiting(() => PortfolioOrderCompositionModel.EvaluateAsync(
            invalid, Book(true), 1, Financial(Book(true)), new TestIdentityAllocator()).AsTask()).Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => PortfolioOrderCompositionModel.EvaluateAsync(
            Request(), Book(true) with { MigrationQualified=false }, 1,
            Financial(Book(true)), new TestIdentityAllocator()).AsTask()).Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Unassigned_deployment_rejects_the_fund_without_allocating_ids()
    {
        var identities = new TestIdentityAllocator();
        var request = Request();
        var other = new CatalogKey(StrategyCatalogKind.Deployment, Guid.NewGuid(), 1);
        request = request with { Body = request.Body with { DeploymentKey = other } };

        var result = await PortfolioOrderCompositionModel.EvaluateAsync(
            request, Book(true), 2, Financial(Book(true)), identities);

        result.Status.Should().Be(PortfolioOrderCompositionStatus.NoTradeOrders);
        result.FundDecisions.Single().ReasonCode.Should().Be("StrategyDeploymentNotAssigned");
        identities.Allocations.Should().Be(0);
    }

    [Fact]
    public async Task Per_deployment_risk_limit_rejects_before_allocating_ids()
    {
        var identities = new TestIdentityAllocator();
        var request = Request();
        request = request with { Body = request.Body with { RequiredCapital = 101, MaximumLoss = 101 } };
        var book = Book(true);
        book = book with
        {
            Funds = book.Funds.Select(fund => fund with
            {
                Deployments = fund.Deployments.Select(deployment => deployment with
                    { MaximumRiskPerTrade = 100 }).ToArray()
            }).ToArray()
        };

        var result = await PortfolioOrderCompositionModel.EvaluateAsync(request, book, 2, Financial(book), identities);

        result.Status.Should().Be(PortfolioOrderCompositionStatus.NoTradeOrders);
        result.FundDecisions.Single().ReasonCode.Should().Be("MaximumRiskPerTradeExceeded");
        identities.Allocations.Should().Be(0);
    }

    [Fact]
    public async Task Insufficient_fund_cash_rejects_without_ids_or_capacity_effects()
    {
        var identities = new TestIdentityAllocator();
        var book = Book(true);

        var result = await PortfolioOrderCompositionModel.EvaluateAsync(
            Request(),book,2,[new(book.Funds[0].FundId,99,[])],identities);

        result.Status.Should().Be(PortfolioOrderCompositionStatus.NoTradeOrders);
        result.FundDecisions.Single().ReasonCode.Should().Be("InsufficientCash");
        result.CapacityEffects.Should().BeEmpty();
        identities.Allocations.Should().Be(0);
    }

    [Fact]
    public async Task Portfolio_capacity_is_shared_across_funds_accepted_in_the_same_atomic_decision()
    {
        var identities = new TestIdentityAllocator();
        var book = Book(true,true) with
        {
            Funds = Book(true,true).Funds.Select(fund => fund with
            {
                Limits = fund.Limits.Select(limit => limit.ScopeKind == CapacityScopeKind.Portfolio
                    && limit.Measure == CapacityMeasure.SettlementCash ? limit with { Maximum=150 } : limit).ToArray()
            }).ToArray()
        };

        var result = await PortfolioOrderCompositionModel.EvaluateAsync(
            Request(),book,2,Financial(book),identities);

        result.TradeOrders.Should().ContainSingle().Which.Id.FundId.Should().Be(10);
        result.FundDecisions.Single(decision => decision.FundId == 11).ReasonCode.Should().Be("CapacityExceeded");
        identities.Allocations.Should().Be(2);
    }

    static EvaluatePortfolioOrderCompositionCommand Request()
    {
        var now = new DateTime(2026,9,12,14,0,0,DateTimeKind.Utc);
        return new()
        {
            CommandId=Guid.NewGuid(), OperationId=Guid.NewGuid(), PortfolioId=1,
            EntityId=new(1,Guid.NewGuid()), RequestedAtUtc=now, ExpiresAtUtc=now.AddMinutes(1),
            InputSha256=new('a',64), Body=new()
            {
                CompositionId=Guid.Parse("11111111-1111-1111-1111-111111111111"), WorkflowId=Guid.NewGuid(),
                DecisionHorizon="Daily", StrategyKind=TradeStrategyKind.FuturesOutright,
                PositionType=TradeOrderPositionType.Opening,
                ValueDate=DateOnly.FromDateTime(now), ValidUntilUtc=now.AddMinutes(1), Origin="ITI",
                EvidenceHash=new('b',64), DeploymentKey=new(StrategyCatalogKind.Deployment,
                    Guid.Parse("22222222-2222-2222-2222-222222222222"),1),
                ProductSymbol="ES",ProductExchange="XCME",ProductCurrency="USD",
                RequiredCapital=100,MaximumLoss=100,StressLoss=100,Notional=1000,
                Components=[new() { ComponentId=Guid.NewGuid(),StrategyKind=TradeStrategyKind.FuturesOutright,
                    Legs=[new() { TradeLegId=Guid.NewGuid(),ContractId="ESZ6",AssetFamily=TradeAssetFamily.Futures,SignedQuantity=1,ContractKey="ESZ6" }] }]
            }
        };
    }

    static FinancialBookConfiguration Book(params bool[] enabled) => new()
    {
        PortfolioId=1,BookId=1,AccountingEntityId=Guid.NewGuid(),ExecutionAccountReference="DU1",Environment="Dev",
        MigrationQualified=true,Funds=enabled.Select((value,index)=>new FinancialFundAuthority
        {
            FundId=10+index,CanSpend=value,
            Limits=Limits(CapacityScopeKind.Portfolio,FinancialScopeKeys.Portfolio(1))
                .Concat(Limits(CapacityScopeKind.Fund,FinancialScopeKeys.Fund(10+index))).ToArray(),
            Deployments=[new FinancialDeploymentAuthority(
                new FinancialAuthorityReference
                {
                    DeploymentKey=new(StrategyCatalogKind.Deployment,
                        Guid.Parse("22222222-2222-2222-2222-222222222222"),1),
                    ValidUntilUtc=new DateTime(2027,1,1,0,0,0,DateTimeKind.Utc)
                }, Limits(CapacityScopeKind.Deployment,FinancialScopeKeys.Deployment(new(StrategyCatalogKind.Deployment,
                    Guid.Parse("22222222-2222-2222-2222-222222222222"),1))), 0)]
        }).ToArray()
    };

    static PortfolioFundFinancialSnapshot[] Financial(FinancialBookConfiguration book) =>
        book.Funds.Select(fund => new PortfolioFundFinancialSnapshot(fund.FundId, 1_000_000m, [])).ToArray();

    static CapacityLimit[] Limits(CapacityScopeKind scope,string key) =>
    [
        new(scope,key,CapacityMeasure.SettlementCash,CapacityUnit.Usd,1_000_000m),
        new(scope,key,CapacityMeasure.LossCharge,CapacityUnit.Usd,1_000_000m),
        new(scope,key,CapacityMeasure.Margin,CapacityUnit.Usd,1_000_000m),
        new(scope,key,CapacityMeasure.GrossNotional,CapacityUnit.Usd,10_000_000m),
        new(scope,key,CapacityMeasure.PositionSlots,CapacityUnit.Positions,100),
        new(scope,key,CapacityMeasure.GrossContracts,CapacityUnit.Contracts,100)
    ];

    sealed class TestIdentityAllocator(int orderStart = 0, int tradeStart = 0) : IPortfolioBusinessIdAllocator
    {
        int order = orderStart;
        int trade = tradeStart;
        public int Allocations { get; private set; }
        public ValueTask<int> AllocateOrderIdAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Allocations++;
            return ValueTask.FromResult(++order);
        }
        public ValueTask<int> AllocateTradeIdAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Allocations++;
            return ValueTask.FromResult(++trade);
        }
        public ValueTask<Domain.Portfolio.Shared.Identities.PortfolioId> AllocatePortfolioIdAsync(
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<int> AllocateFundIdAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
