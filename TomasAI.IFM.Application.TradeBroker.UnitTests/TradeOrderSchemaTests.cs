using MessagePack;
using TomasAI.IFM.Domain.Trade.Order.Broker.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using Xunit;

namespace TomasAI.IFM.Application.TradeBroker.UnitTests;

public sealed class TradeOrderSchemaTests
{
    [Fact]
    public void Immutable_old_order_reads_but_cannot_be_dispatched_without_new_approval_fields()
    {
        var old = new LegacyTradeOrder
        {
            SchemaVersion = 3, Id = new TradeOrderId(1, 2, 3), Revision = 1,
            Status = TradeOrderStatus.Approved, ValueDate = new DateOnly(2026, 9, 16),
            ValidUntilUtc = new DateTime(2026, 9, 16, 14, 5, 0, DateTimeKind.Utc),
            Components = [new TradeOrderComponentDefinition { ComponentId = Guid.NewGuid(), StrategyKind = TradeStrategyKind.FuturesOutright }],
            DefinitionHash = "OLD-DEFINITION", PositionType = TradeOrderPositionType.Opening
        };
        var payload = MessagePackSerializer.Serialize(old);
        var newReader = MessagePackSerializer.Deserialize<TradeOrderDefinition>(payload);
        Assert.Equal((ushort)3, newReader.SchemaVersion);
        Assert.Equal(old.Id, newReader.Id);
        Assert.Equal(old.Components[0].ComponentId, newReader.Components[0].ComponentId);
        Assert.False(BrokerOrderRequestMapper.TryCreate(newReader, Guid.NewGuid(), old.Components[0].ComponentId, Guid.NewGuid(), out _, out var reason));
        Assert.Equal("BO.APPROVAL.INCOMPLETE", reason);
    }

    [Theory]
    [InlineData(TradeStrategyKind.FuturesOutright, 1)]
    [InlineData(TradeStrategyKind.VerticalSpread, 2)]
    [InlineData(TradeStrategyKind.IronCondor, 4)]
    public void Accepted_manual_definition_maps_all_exact_ids_and_price_bounds(TradeStrategyKind strategy, int legCount)
    {
        var componentId = Guid.NewGuid();
        var attemptId = Guid.NewGuid();
        var operationId = Guid.NewGuid();
        var order = new TradeOrderDefinition
        {
            Id = new(7, 8, 9), Revision = 1, Status = TradeOrderStatus.Approved,
            PositionType = TradeOrderPositionType.Opening, PortfolioApprovalId = Guid.NewGuid(),
            BrokerAccountAlias = "EMU", BrokerEnvironment = BrokerEnvironment.Emulator,
            DefinitionHash = "hash", MicroExecutionProfileHash = "profile",
            RequiredCapital = 1_000m, MaximumLoss = 1_000m,
            ValidUntilUtc = new DateTime(2026, 9, 16, 14, 5, 0, DateTimeKind.Utc),
            Components = [new TradeOrderComponentDefinition
            {
                ComponentId = componentId, ReservedTradeId = 10, StrategyKind = strategy,
                SignedNetDebitLimit = -1m, MinimumSignedNetDebitLimit = -1.5m,
                MaximumSignedNetDebitLimit = -0.5m, TickIncrement = 0.05m,
                Legs = [.. Enumerable.Range(0, legCount).Select(i => new TradeLegDefinition
                {
                    TradeLegId = Guid.NewGuid(), ContractId = $"CONTRACT-{i}", SignedQuantity = i % 2 == 0 ? 1 : -1,
                    AssetFamily = strategy == TradeStrategyKind.FuturesOutright ? TradeAssetFamily.Futures : TradeAssetFamily.FuturesOption,
                    CashMultiplier = 1m
                })]
            }]
        };
        Assert.True(BrokerOrderRequestMapper.TryCreate(order, attemptId, componentId, operationId, out var mapped, out var reason), reason);
        Assert.NotNull(mapped);
        Assert.Equal($"{new OrderExecutionId(order.Id, attemptId).Format()}.{componentId:N}", mapped.BrokerOrderId);
        Assert.Equal(order.Components[0].Legs.Select(x => x.ContractId), mapped.Legs.Select(x => x.ContractId));
        Assert.Equal(-1m, mapped.SignedNetDebitLimit);
        Assert.Equal(-1.5m, mapped.MinimumLimit);
        Assert.Equal(-0.5m, mapped.MaximumLimit);
        Assert.Equal(1_000m, mapped.RequiredCapital);
    }

    [MessagePackObject]
    public sealed record LegacyTradeOrder
    {
        [Key(0)] public ushort SchemaVersion { get; init; }
        [Key(1)] public TradeOrderId Id { get; init; }
        [Key(2)] public int Revision { get; init; }
        [Key(3)] public TradeOrderStatus Status { get; init; }
        [Key(4)] public DateOnly ValueDate { get; init; }
        [Key(5)] public DateTime ValidUntilUtc { get; init; }
        [Key(6)] public string Origin { get; init; } = string.Empty;
        [Key(7)] public TradeOrderComponentDefinition[] Components { get; init; } = [];
        [Key(8)] public string DefinitionHash { get; init; } = string.Empty;
        [Key(9)] public Guid? BoundExecutionAttemptId { get; init; }
        [Key(10)] public ExecutionChannel? BoundExecutionChannel { get; init; }
        [Key(11)] public DateTime? ExecutionBoundAtUtc { get; init; }
        [Key(12)] public TradeOrderPositionType PositionType { get; init; }
        [Key(13)] public StrategyPositionId? TargetPositionId { get; init; }
    }
}
