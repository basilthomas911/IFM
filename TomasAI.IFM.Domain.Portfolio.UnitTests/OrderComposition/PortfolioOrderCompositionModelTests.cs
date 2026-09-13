using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Identity;
using TomasAI.IFM.Domain.Portfolio.OrderComposition.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared.Model;

namespace TomasAI.IFM.Domain.Portfolio.UnitTests.OrderComposition;

public sealed class PortfolioOrderCompositionModelTests
{
    [Fact]
    public async Task Eligible_funds_each_receive_one_order_and_disabled_funds_are_preserved_as_decisions()
    {
        var request = Request();
        var result = await PortfolioOrderCompositionModel.EvaluateAsync(
            request, Book(true, false, true), 7, new TestIdentityAllocator());
        result.Status.Should().Be(PortfolioOrderCompositionStatus.ExecuteTradeOrders);
        result.TradeOrders.Select(x => x.Id.FundId).Should().Equal(10, 12);
        result.FundDecisions.Should().HaveCount(3).And.ContainSingle(x => !x.Accepted && x.FundId == 11);
        result.TradeOrders.Should().OnlyContain(x => x.Id.OrderId > 0
            && x.Components.All(c => c.ReservedTradeId > 0));
    }

    [Fact]
    public async Task No_eligible_fund_returns_atomic_no_trade_result_with_empty_orders()
    {
        var identities = new TestIdentityAllocator();
        var result = await PortfolioOrderCompositionModel.EvaluateAsync(
            Request(), Book(false, false), 9, identities);
        result.Status.Should().Be(PortfolioOrderCompositionStatus.NoTradeOrders);
        result.TradeOrders.Should().BeEmpty();
        result.FundDecisions.Should().OnlyContain(x => !x.Accepted);
        identities.Allocations.Should().Be(0);
    }

    [Fact]
    public async Task Accepted_funds_receive_sequence_allocated_order_and_trade_identities()
    {
        var identities = new TestIdentityAllocator(orderStart: 100, tradeStart: 200);
        var result = await PortfolioOrderCompositionModel.EvaluateAsync(
            Request(), Book(true), 2, identities);
        result.TradeOrders.Single().Id.OrderId.Should().Be(101);
        result.TradeOrders.Single().Components.Single().ReservedTradeId.Should().Be(201);
        identities.Allocations.Should().Be(2);
    }

    [Fact]
    public async Task Invalid_or_unqualified_input_is_rejected_without_a_partial_result()
    {
        var invalid = Request() with { Body = Request().Body with { Components = [] } };
        await FluentActions.Awaiting(() => PortfolioOrderCompositionModel.EvaluateAsync(
            invalid, Book(true), 1, new TestIdentityAllocator()).AsTask()).Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => PortfolioOrderCompositionModel.EvaluateAsync(
            Request(), Book(true) with { MigrationQualified=false }, 1,
            new TestIdentityAllocator()).AsTask()).Should().ThrowAsync<InvalidOperationException>();
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
                ValueDate=DateOnly.FromDateTime(now), ValidUntilUtc=now.AddMinutes(1), Origin="ITI",
                EvidenceHash=new('b',64), Components=[new() { ComponentId=Guid.NewGuid(),StrategyKind=TradeStrategyKind.FuturesOutright,
                    Legs=[new() { TradeLegId=Guid.NewGuid(),MarketInstrumentId=42,AssetFamily=TradeAssetFamily.Futures,SignedQuantity=1,ContractKey="ES" }] }]
            }
        };
    }

    static FinancialBookConfiguration Book(params bool[] enabled) => new()
    {
        PortfolioId=1,BookId=1,AccountingEntityId=Guid.NewGuid(),ExecutionAccountReference="DU1",Environment="Dev",
        MigrationQualified=true,Funds=enabled.Select((value,index)=>new FinancialFundAuthority {FundId=10+index,CanSpend=value}).ToArray()
    };

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
