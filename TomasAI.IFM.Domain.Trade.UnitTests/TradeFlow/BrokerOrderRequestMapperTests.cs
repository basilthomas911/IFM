using FluentAssertions;
using TomasAI.IFM.Domain.Trade.Order.Broker.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using AppBrokerAlgorithm = TomasAI.IFM.Application.TradeBroker.Contracts.BrokerAlgorithm;
using AppBrokerEnvironment = TomasAI.IFM.Application.TradeBroker.Contracts.BrokerEnvironment;
using AppBrokerOrderType = TomasAI.IFM.Application.TradeBroker.Contracts.BrokerOrderType;

namespace TomasAI.IFM.Domain.Trade.UnitTests.TradeFlow;

public sealed class BrokerOrderRequestMapperTests
{
    [Fact]
    public void Approved_market_adaptive_selection_and_environment_reach_broker_request()
    {
        var order = ApprovedOrder() with
        {
            BrokerEnvironment = BrokerEnvironment.Paper,
            BrokerOrderType = BrokerOrderType.Market,
            BrokerAlgorithm = BrokerAlgorithm.Adaptive
        };

        var created = BrokerOrderRequestMapper.TryCreate(order, Guid.NewGuid(),
            order.Components[0].ComponentId, Guid.NewGuid(), out var request, out var reason);

        created.Should().BeTrue(reason);
        request!.Environment.Should().Be(AppBrokerEnvironment.Paper);
        request.OrderType.Should().Be(AppBrokerOrderType.Market);
        request.Algorithm.Should().Be(AppBrokerAlgorithm.Adaptive);
    }

    [Fact]
    public void Legacy_execution_profile_defaults_to_limit_without_algorithm()
    {
        var order = ApprovedOrder() with
        {
            BrokerOrderType = BrokerOrderType.Unknown,
            BrokerAlgorithm = BrokerAlgorithm.None
        };

        var created = BrokerOrderRequestMapper.TryCreate(order, Guid.NewGuid(),
            order.Components[0].ComponentId, Guid.NewGuid(), out var request, out var reason);

        created.Should().BeTrue(reason);
        request!.OrderType.Should().Be(AppBrokerOrderType.Limit);
        request.Algorithm.Should().Be(AppBrokerAlgorithm.None);
    }

    static TradeOrderDefinition ApprovedOrder()
    {
        var componentId = Guid.NewGuid();
        return new TradeOrderDefinition
        {
            Id = new TradeOrderId(1, 2, 3),
            Revision = 1,
            Status = TradeOrderStatus.Approved,
            ValueDate = new DateOnly(2026, 9, 20),
            ValidUntilUtc = DateTime.UtcNow.AddMinutes(5),
            Origin = "test",
            PositionType = TradeOrderPositionType.Opening,
            DefinitionHash = new string('a', 64),
            BrokerAccountAlias = "DU123",
            BrokerEnvironment = BrokerEnvironment.Emulator,
            PortfolioApprovalId = Guid.NewGuid(),
            MicroExecutionProfileId = "legacy",
            MicroExecutionProfileVersion = 1,
            MicroExecutionProfileHash = new string('b', 64),
            Components =
            [
                new TradeOrderComponentDefinition
                {
                    ComponentId = componentId,
                    StrategyKind = TradeStrategyKind.FuturesOutright,
                    ReservedTradeId = 4,
                    SignedNetDebitLimit = 6000m,
                    MinimumSignedNetDebitLimit = 5999m,
                    MaximumSignedNetDebitLimit = 6001m,
                    TickIncrement = 0.25m,
                    Legs =
                    [
                        new TradeLegDefinition
                        {
                            TradeLegId = Guid.NewGuid(), ContractId = "ESZ6",
                            ContractKey = "ESZ6", AssetFamily = TradeAssetFamily.Futures,
                            SignedQuantity = 1, CashMultiplier = 50m
                        }
                    ]
                }
            ]
        };
    }
}
