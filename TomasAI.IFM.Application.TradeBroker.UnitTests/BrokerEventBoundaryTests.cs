using System.Reflection;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Order.Broker.Command;
using TomasAI.IFM.Domain.Trade.Order.Broker.Command.Actor;
using TomasAI.IFM.Domain.Trade.Order.Broker.Event;
using TomasAI.IFM.Domain.Trade.Order.Broker.Event.Actor;
using TomasAI.IFM.Domain.Trade.Order.Broker.Query;
using TomasAI.IFM.Domain.Trade.Order.Broker.Query.Actor;
using TomasAI.IFM.Domain.Trade.Order.Broker.Realtime;
using TomasAI.IFM.Domain.Trade.Order.Broker.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order.Broker;
using TomasAI.IFM.Shared.EventModelActor;
using Xunit;

namespace TomasAI.IFM.Application.TradeBroker.UnitTests;

public sealed class BrokerEventBoundaryTests
{
    [Fact]
    public void Broker_order_event_contract_round_trips_and_has_one_mapped_handler()
    {
        var id = new BrokerOrderId(new(new(1, 2, 3), Guid.NewGuid()), Guid.NewGuid());
        var expected = new BrokerOrderObservationReceivedEvent
        {
            Id = Guid.NewGuid(),
            CommandId = Guid.NewGuid(),
            Subject = new(ActorType.Event, BrokerOrderEventActor.ActorName,
                BrokerOrderObservationReceivedEvent.Verb, id.Format()),
            EntityId = id,
            AggregateId = id.Format(),
            EventSource = "unit-test",
            ReceivedOn = new DateTime(2026, 9, 16, 14, 0, 0, DateTimeKind.Utc),
            Observation = new BrokerOrderObservationEvidence
            {
                ObservationId = Guid.NewGuid(),
                Kind = BrokerOrderObservationKind.Execution,
                AccountAlias = "IFM-EMULATOR-PAPER",
                OperationId = Guid.NewGuid(),
                ComponentId = id.ComponentId,
                LegId = Guid.NewGuid(),
                ContractId = "ESZ6",
                ExternalExecutionId = "EM-1",
                SignedQuantity = 1,
                Price = 6000m,
                OrderRevision = 1,
                SourceEpoch = 1,
                SourceSequence = 17,
                OccurredAtUtc = new DateTime(2026, 9, 16, 14, 0, 1, DateTimeKind.Utc),
                ContentHash = "HASH"
            }
        };

        var actual = MessagePackSerializer.Deserialize<BrokerOrderObservationReceivedEvent>(
            MessagePackSerializer.Serialize(expected));

        Assert.Equal(expected, actual);
        AssertSingleMapEntry(typeof(BrokerOrderEventActor), "_parseMap");
        AssertSingleMapEntry(typeof(BrokerOrderEventActor), "_receiveMap");
        AssertSingleExecuteHandler(typeof(BrokerOrderObservationReceived));
    }

    [Fact]
    public void Broker_actor_maps_have_exact_role_parity_and_dedicated_single_message_handlers()
    {
        AssertMapCount(typeof(BrokerOrderCommandActor), "_parseMap", 6);
        AssertMapCount(typeof(BrokerOrderCommandActor), "_validationMap", 6);
        AssertMapCount(typeof(BrokerOrderCommandActor), "_receiveMap", 6);
        AssertMapCount(typeof(BrokerOrderQueryActor), "_parseMap", 2);
        AssertMapCount(typeof(BrokerOrderQueryActor), "_receiveMap", 2);
        AssertMapCount(typeof(BrokerOrderQueryActor), "_exceptionMap", 2);
        AssertMapCount(typeof(BrokerOrderRealtimeActor), "_parseMap", 1);
        AssertMapCount(typeof(BrokerOrderRealtimeActor), "_receiveMap", 1);

        foreach (var handler in new[]
        {
            typeof(CreateBrokerOrder), typeof(RecordBrokerDispatch),
            typeof(RecordBrokerOrderObservation), typeof(RequestBrokerOrderLimitUpdate),
            typeof(RequestBrokerOrderCancel), typeof(ReconcileBrokerOrder), typeof(GetBrokerOrder), typeof(GetBrokerOrdersForTradeOrder),
            typeof(FuturesTickQuoteDataChanged)
        })
            AssertSingleHandler(handler);
    }

    private static void AssertSingleMapEntry(Type actorType, string fieldName)
    {
        var field = actorType.GetField(fieldName,
            BindingFlags.Static | BindingFlags.NonPublic) ??
            throw new Xunit.Sdk.XunitException($"{actorType.Name}.{fieldName} was not found.");
        var value = field.GetValue(null) ??
            throw new Xunit.Sdk.XunitException($"{actorType.Name}.{fieldName} was null.");
        var count = (int)(value.GetType().GetProperty("Count")?.GetValue(value) ?? -1);
        Assert.Equal(1, count);
    }

    private static void AssertMapCount(Type actorType, string fieldName, int expected) =>
        Assert.Equal(expected, ReadMapCount(actorType, fieldName));

    private static int ReadMapCount(Type actorType, string fieldName)
    {
        var field = actorType.GetField(fieldName,
            BindingFlags.Static | BindingFlags.NonPublic) ??
            throw new Xunit.Sdk.XunitException($"{actorType.Name}.{fieldName} was not found.");
        var value = field.GetValue(null) ??
            throw new Xunit.Sdk.XunitException($"{actorType.Name}.{fieldName} was null.");
        return (int)(value.GetType().GetProperty("Count")?.GetValue(value) ?? -1);
    }

    private static void AssertSingleExecuteHandler(Type handlerType)
    {
        var methods = handlerType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name == "ExecuteAsync")
            .ToArray();
        Assert.Single(methods);
    }

    private static void AssertSingleHandler(Type handlerType)
    {
        var methods = handlerType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name is "Execute" or "ExecuteAsync")
            .ToArray();
        Assert.Single(methods);
    }
}
