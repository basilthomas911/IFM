using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Portfolio;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Portfolio;

public sealed class PortfolioExecutionContractMapperTests
{
    [Fact]
    public void Portfolio_instruction_round_trips_through_the_trade_boundary()
    {
        var instruction = new PortfolioExecutionOrderInstruction
        {
            Id = new(11, 17, 23),
            Revision = 4,
            Status = PortfolioExecutionOrderStatus.Approved,
            ValueDate = new(2026, 9, 21),
            ValidUntilUtc = new(2026, 9, 21, 18, 0, 0, DateTimeKind.Utc),
            Origin = "Portfolio",
            DefinitionHash = new('a', 64),
            PositionType = PortfolioExecutionPositionType.Closing,
            TargetPosition = new(11, 17, 19, 29, Guid.Parse("11111111-1111-1111-1111-111111111111")),
            BrokerAccountAlias = "DU1",
            BrokerEnvironment = PortfolioBrokerEnvironment.Paper,
            PortfolioApprovalId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            MicroExecutionProfileId = "adaptive",
            MicroExecutionProfileVersion = 3,
            MicroExecutionProfileHash = new('b', 64),
            AccountPromotionApprovalReference = "approval",
            RequiredCapital = 1200m,
            MaximumLoss = 450m,
            BrokerOrderType = PortfolioBrokerOrderType.Limit,
            BrokerAlgorithm = PortfolioBrokerAlgorithm.Adaptive,
            Components =
            [
                new PortfolioExecutionComponent
                {
                    ComponentId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                    StrategyKind = PortfolioExecutionStrategyKind.FuturesOutright,
                    ReservedTradeId = 31,
                    SignedNetDebitLimit = 2.5m,
                    Legs =
                    [
                        new PortfolioExecutionLeg
                        {
                            TradeLegId = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                            AssetFamily = PortfolioExecutionAssetFamily.Futures,
                            SignedQuantity = -2,
                            LimitPrice = 6400.25m,
                            ContractKey = "ESZ6",
                            ContractId = "ESZ6",
                            CashMultiplier = 50m
                        }
                    ]
                }
            ]
        };

        var tradeOrder = instruction.ToTradeOrder();
        var roundTrip = tradeOrder.ToPortfolioInstruction();

        tradeOrder.Id.Should().Be(new TradeOrderId(11, 17, 23));
        tradeOrder.TargetPositionId!.Value.Trade.TradeId.Should().Be(29);
        roundTrip.Should().BeEquivalentTo(instruction);
    }
}
