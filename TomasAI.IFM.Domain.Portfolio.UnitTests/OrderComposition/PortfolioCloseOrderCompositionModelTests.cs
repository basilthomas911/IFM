using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Identity;
using TomasAI.IFM.Domain.Portfolio.OrderComposition.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;

namespace TomasAI.IFM.Domain.Portfolio.UnitTests.OrderComposition;

public sealed class PortfolioCloseOrderCompositionModelTests
{
    static readonly DateTime Now = new(2026, 9, 13, 14, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Closing_order_allocates_only_a_new_order_and_retains_the_existing_trade()
    {
        var (request, openingOrder) = Fixture();
        var identities = new IdentityAllocator();

        var receipt = await PortfolioCloseOrderCompositionModel.EvaluateAsync(
            request, Book(), 8, openingOrder, identities);

        receipt.Status.Should().Be(PortfolioCloseOrderCompositionStatus.ExecuteTradeOrder);
        receipt.TradeOrder!.PositionType.Should().Be(TradeOrderPositionType.Closing);
        receipt.TradeOrder.TargetPositionId.Should().Be(request.Body.Position.Id);
        receipt.TradeOrder.Id.OrderId.Should().Be(901);
        receipt.TradeOrder.Components.Should().ContainSingle()
            .Which.ReservedTradeId.Should().Be(request.Body.Position.Id.Trade.TradeId);
        identities.OrderAllocations.Should().Be(1);
        identities.TradeAllocations.Should().Be(0);
    }

    [Fact]
    public async Task Closing_order_must_exactly_reverse_every_remaining_leg()
    {
        var (request, openingOrder) = Fixture();
        request = request with
        {
            Body = request.Body with
            {
                Component = request.Body.Component with
                {
                    Legs = [request.Body.Component.Legs[0] with { SignedQuantity = 1 }]
                }
            }
        };

        var action = () => PortfolioCloseOrderCompositionModel.EvaluateAsync(
            request, Book(), 8, openingOrder, new IdentityAllocator()).AsTask();

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*exactly reverse*");
    }

    [Fact]
    public async Task Opening_intent_is_rejected_by_the_close_boundary()
    {
        var (request, openingOrder) = Fixture();
        request = request with
        {
            Body = request.Body with { PositionType = TradeOrderPositionType.Opening }
        };

        var action = () => PortfolioCloseOrderCompositionModel.EvaluateAsync(
            request, Book(), 8, openingOrder, new IdentityAllocator()).AsTask();

        await action.Should().ThrowAsync<ArgumentException>();
    }

    static (EvaluatePortfolioCloseOrderCompositionCommand Request, TradeOrderDefinition OpeningOrder) Fixture()
    {
        var trade = new TradeEntityId(11, 12, 13, 14);
        var positionId = new StrategyPositionId(trade, Guid.Parse("10000000-0000-0000-0000-000000000001"));
        var legId = Guid.Parse("20000000-0000-0000-0000-000000000001");
        var openLeg = new TradeLegDefinition
        {
            TradeLegId = legId, ContractId = "ESZ6", ContractKey = "ESZ6",
            AssetFamily = TradeAssetFamily.Futures, SignedQuantity = 2
        };
        var component = new TradeOrderComponentDefinition
        {
            ComponentId = Guid.Parse("30000000-0000-0000-0000-000000000001"),
            StrategyKind = TradeStrategyKind.FuturesOutright,
            ReservedTradeId = trade.TradeId,
            Legs = [openLeg]
        };
        var openingOrder = new TradeOrderDefinition
        {
            Id = new(trade.PortfolioId, trade.FundId, trade.OrderId), Revision = 1,
            Status = TradeOrderStatus.Completed, PositionType = TradeOrderPositionType.Opening,
            ValueDate = DateOnly.FromDateTime(Now), ValidUntilUtc = Now.AddDays(1),
            Origin = "Strategy", DefinitionHash = new('a', 64), Components = [component]
        };
        var workflowId = new ExitPositionWorkflowId(
            positionId, DateOnly.FromDateTime(Now), Guid.Parse("40000000-0000-0000-0000-000000000001"));
        var operationId = Guid.Parse("50000000-0000-0000-0000-000000000001");
        var request = new EvaluatePortfolioCloseOrderCompositionCommand
        {
            CommandId = operationId, OperationId = operationId, PortfolioId = trade.PortfolioId,
            EntityId = new(trade.PortfolioId, operationId), RequestedAtUtc = Now,
            ExpiresAtUtc = Now.AddMinutes(1), InputSha256 = new('b', 64),
            Body = new PortfolioCloseOrderCandidate
            {
                CompositionId = workflowId.ExitDecisionId, WorkflowId = workflowId,
                Position = new StrategyPositionSnapshot
                {
                    Id = positionId, StrategyKind = TradeStrategyKind.FuturesOutright,
                    PositionSequence = 4, IsOpen = true, AsOfUtc = Now,
                    Legs =
                    [
                        new StrategyPositionLeg
                        {
                            TradeLegId = legId, ContractId = openLeg.ContractId,
                            ContractKey = openLeg.ContractKey, AssetFamily = openLeg.AssetFamily,
                            SignedQuantity = openLeg.SignedQuantity, OpeningPrice = 100,
                            CurrentPrice = 101, LastPriceAtUtc = Now
                        }
                    ]
                },
                StrategyKind = TradeStrategyKind.FuturesOutright,
                ValueDate = DateOnly.FromDateTime(Now), ValidUntilUtc = Now.AddMinutes(1),
                Origin = "FuturesExitPositionWorkflow", PositionType = TradeOrderPositionType.Closing,
                EvidenceHash = new('c', 64),
                Component = component with
                {
                    Legs = [openLeg with { SignedQuantity = -openLeg.SignedQuantity }]
                }
            }
        };
        return (request, openingOrder);
    }

    static FinancialBookConfiguration Book() => new()
    {
        PortfolioId = 11, BookId = 1, AccountingEntityId = Guid.NewGuid(), Currency = "USD",
        Funds = [new FinancialFundAuthority { FundId = 12 }]
    };

    sealed class IdentityAllocator : IPortfolioBusinessIdAllocator
    {
        public int OrderAllocations { get; private set; }
        public int TradeAllocations { get; private set; }
        public ValueTask<int> AllocateOrderIdAsync(CancellationToken cancellationToken = default)
        {
            OrderAllocations++;
            return ValueTask.FromResult(900 + OrderAllocations);
        }
        public ValueTask<int> AllocateTradeIdAsync(CancellationToken cancellationToken = default)
        {
            TradeAllocations++;
            return ValueTask.FromResult(800 + TradeAllocations);
        }
        public ValueTask<Domain.Portfolio.Shared.Identities.PortfolioId> AllocatePortfolioIdAsync(
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<int> AllocateFundIdAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
