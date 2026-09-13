using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Order.Command.State;
using TomasAI.IFM.Domain.Trade.Order.Model;
using TomasAI.IFM.Domain.Trade.Shared.Order;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Trade.IntegratedTests.TradeFlow;

public sealed class TradeFlowSerializationAndRecoveryTests
{
    [Fact]
    public void Trade_order_messagepack_round_trip_preserves_stable_ownership_and_leg_identity()
    {
        var legId = Guid.NewGuid();
        var order = CreateOrder(legId);

        var payload = MessagePackSerializer.Serialize(order);
        var restored = MessagePackSerializer.Deserialize<TradeOrderDefinition>(payload);

        restored.Should().BeEquivalentTo(order);
        restored.Id.Format().Should().Be("1.2.3");
        restored.Components[0].Legs[0].TradeLegId.Should().Be(legId);
    }

    [Fact]
    public void Replayed_order_state_continues_from_exact_committed_status()
    {
        var original = new TradeOrderActorStateMachine();
        original.Create(CreateOrder(Guid.NewGuid()));
        original.Approve();
        var committed = original.Ready().Value!;
        var payload = MessagePackSerializer.Serialize(committed);

        var recovered = new TradeOrderActorStateMachine();
        recovered.Replay(MessagePackSerializer.Deserialize<TradeOrderDefinition>(payload));

        recovered.Current.Should().BeEquivalentTo(committed);
        recovered.BindExecution(Guid.NewGuid(), ExecutionChannel.Manual, DateTime.UtcNow)
            .Value!.Status.Should().Be(TradeOrderStatus.Executing);
    }

    [Fact]
    public void Append_only_component_key_preserves_older_payload_compatibility()
    {
        var legacyPayload = MessagePackSerializer.Serialize(new LegacyComponent(
            Guid.NewGuid(), TradeStrategyKind.FuturesOutright,
            [new TradeLegDefinition { TradeLegId = Guid.NewGuid(), LegacyMarketInstrumentId = 8, AssetFamily = TradeAssetFamily.Futures, SignedQuantity = 1 }],
            false));

        var restored = MessagePackSerializer.Deserialize<TradeOrderComponentDefinition>(legacyPayload);

        restored.ReservedTradeId.Should().Be(0);
        restored.Legs.Should().ContainSingle();
    }

    [Fact]
    public void Typed_command_and_event_round_trip_and_replay_without_json_fallback()
    {
        var order = CreateOrder(Guid.NewGuid());
        var command = new CreateTradeOrderCommand
        {
            CommandId = Guid.NewGuid(), EntityId = order.Id, Order = order,
            Subject = new ActorSubject(ActorType.Command, TradeOrderActorNames.Command,
                CreateTradeOrderCommand.Verb, order.Id.Format())
        };
        var commandCopy = MessagePackSerializer.Deserialize<CreateTradeOrderCommand>(
            MessagePackSerializer.Serialize(command));
        var changed = new TradeOrderChangedEvent
        {
            EntityId = order.Id, State = order
        };
        var eventCopy = MessagePackSerializer.Deserialize<TradeOrderChangedEvent>(
            MessagePackSerializer.Serialize(changed));
        var state = new TradeOrderCommandState();

        state.ReplayEvents([eventCopy]);

        commandCopy.Should().BeEquivalentTo(command);
        state.Current.Should().BeEquivalentTo(order);
    }

    static TradeOrderDefinition CreateOrder(Guid legId) => new()
    {
        Id = new TradeOrderId(1, 2, 3),
        Revision = 1,
        ValueDate = new DateOnly(2026, 9, 12),
        ValidUntilUtc = new DateTime(2026, 9, 12, 16, 0, 0, DateTimeKind.Utc),
        Origin = "IntegrationTest",
        DefinitionHash = "hash",
        Components = [new TradeOrderComponentDefinition
        {
            ComponentId = Guid.NewGuid(),
            ReservedTradeId = 4,
            StrategyKind = TradeStrategyKind.FuturesOutright,
            Legs = [new TradeLegDefinition
            {
                TradeLegId = legId,
                ContractId = "ESZ6",
                AssetFamily = TradeAssetFamily.Futures,
                SignedQuantity = 1,
                ContractKey = "ESZ6"
            }]
        }]
    };

    [MessagePackObject(AllowPrivate = true)]
    internal sealed record LegacyComponent(
        [property: Key(0)] Guid ComponentId,
        [property: Key(1)] TradeStrategyKind StrategyKind,
        [property: Key(2)] TradeLegDefinition[] Legs,
        [property: Key(3)] bool PermitBalancedPartialAcceptance);
}
