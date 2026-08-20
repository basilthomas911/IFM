using FluentAssertions;
using Newtonsoft.Json;
using TomasAI.IFM.Domain.Trade.Option.Command.State;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Option;

public sealed class OptionTradeCommandStateReplayTests
{
    [Fact]
    public void To_open_event_restores_nested_trade_order_data_from_persisted_json()
    {
        var tradeOrder = SampleData.CreateTradeOrder(orderId: 801, tradeId: 3101);
        var original = new OptionTradeToOpenEvent
        {
            Subject = new ActorSubject(
                ActorType.Event,
                OptionTradeToOpenEvent.Actor,
                OptionTradeToOpenEvent.Verb,
                new OptionTradeEntityId(tradeOrder.OrderId, tradeOrder.TradeId).Format()),
            Id = Guid.NewGuid(),
            CommandId = Guid.NewGuid(),
            EntityId = new OptionTradeEntityId(tradeOrder.OrderId, tradeOrder.TradeId),
            AggregateId = $"{tradeOrder.OrderId}:{tradeOrder.TradeId}",
            EventSource = "unit-test",
            ReceivedOn = DateTime.UtcNow,
            TradeOrder = tradeOrder,
            OptionTrade = SampleData.CreateOptionTrade(tradeOrder.OrderId, tradeOrder.TradeId),
            OpenedOn = DateTime.UtcNow,
            OpenedBy = "unit-test"
        };
        var persisted = new EventStreamReadModel
        {
            EventVersion = 1,
            EventTypeName = typeof(OptionTradeToOpenEvent).AssemblyQualifiedName!,
            EventData = JsonConvert.SerializeObject(original)
        };
        var rehydrated = persisted.ToDomainEvent().Should().BeOfType<OptionTradeToOpenEvent>().Subject;

        rehydrated.TradeOrder.TradeLimit.Should().NotBeNull();
        rehydrated.TradeOrder.TradeLimit.TradeId.Should().Be(tradeOrder.TradeId);
        var state = new OptionTradeCommandState();

        var replay = () => state.ReplayEvents([persisted]);

        replay.Should().NotThrow();
        state.Events.Should().BeEmpty();
        state.Updated.Should().BeFalse();
    }
}
