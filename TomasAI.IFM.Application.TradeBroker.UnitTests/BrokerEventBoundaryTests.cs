using System.Reflection;
using MessagePack;
using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Domain.BrokerAccount.Event;
using TomasAI.IFM.Domain.BrokerAccount.Event.Actor;
using TomasAI.IFM.Domain.BrokerAccount.Command;
using TomasAI.IFM.Domain.BrokerAccount.Command.Actor;
using TomasAI.IFM.Domain.BrokerAccount.Query;
using TomasAI.IFM.Domain.BrokerAccount.Query.Actor;
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
using AppBrokerEnvironment = TomasAI.IFM.Application.TradeBroker.Contracts.BrokerEnvironment;

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
    public void Broker_account_event_contract_round_trips_and_has_one_mapped_handler()
    {
        var id = new BrokerAccountId("IFM-EMULATOR-PAPER");
        var expected = new BrokerAccountSnapshotObservedEvent
        {
            Id = Guid.NewGuid(),
            CommandId = Guid.NewGuid(),
            Subject = new(ActorType.Event, BrokerAccountEventActor.ActorName,
                BrokerAccountSnapshotObservedEvent.Verb, id.Format()),
            EntityId = id,
            AggregateId = id.Format(),
            EventSource = "unit-test",
            ReceivedOn = new DateTime(2026, 9, 16, 14, 0, 0, DateTimeKind.Utc),
            Environment = AppBrokerEnvironment.Emulator,
            Snapshot = new BrokerAccountSnapshotEvidence
            {
                AccountAlias = id.AccountAlias,
                Currency = "USD",
                CashBalance = 100_000m,
                AvailableFunds = 90_000m,
                Complete = true,
                NewRiskAllowed = true,
                Generation = 3,
                AsOfUtc = new DateTime(2026, 9, 16, 14, 0, 0, DateTimeKind.Utc),
                Positions = [new() { ContractId = "ESZ6", SignedQuantity = 1, AveragePrice = 6000m }]
            }
        };

        var actual = MessagePackSerializer.Deserialize<BrokerAccountSnapshotObservedEvent>(
            MessagePackSerializer.Serialize(expected));

        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.CommandId, actual.CommandId);
        Assert.Equal(expected.Subject, actual.Subject);
        Assert.Equal(expected.EntityId, actual.EntityId);
        Assert.Equal(expected.Environment, actual.Environment);
        Assert.Equal(expected.Snapshot.AccountAlias, actual.Snapshot.AccountAlias);
        Assert.Equal(expected.Snapshot.Currency, actual.Snapshot.Currency);
        Assert.Equal(expected.Snapshot.CashBalance, actual.Snapshot.CashBalance);
        Assert.Equal(expected.Snapshot.AvailableFunds, actual.Snapshot.AvailableFunds);
        Assert.Equal(expected.Snapshot.Complete, actual.Snapshot.Complete);
        Assert.Equal(expected.Snapshot.NewRiskAllowed, actual.Snapshot.NewRiskAllowed);
        Assert.Equal(expected.Snapshot.Generation, actual.Snapshot.Generation);
        Assert.Equal(expected.Snapshot.AsOfUtc, actual.Snapshot.AsOfUtc);
        Assert.Equal(expected.Snapshot.Positions, actual.Snapshot.Positions);
        AssertSingleMapEntry(typeof(BrokerAccountEventActor), "_parseMap");
        AssertSingleMapEntry(typeof(BrokerAccountEventActor), "_receiveMap");
        AssertSingleExecuteHandler(typeof(BrokerAccountSnapshotObserved));
    }

    [Fact]
    public void Broker_actor_maps_have_exact_role_parity_and_dedicated_single_message_handlers()
    {
        AssertMapCount(typeof(BrokerOrderCommandActor), "_parseMap", 5);
        AssertMapCount(typeof(BrokerOrderCommandActor), "_validationMap", 5);
        AssertMapCount(typeof(BrokerOrderCommandActor), "_receiveMap", 5);
        AssertMapCount(typeof(BrokerAccountCommandActor), "_parseMap", 7);
        AssertMapCount(typeof(BrokerAccountCommandActor), "_validationMap", 7);
        AssertMapCount(typeof(BrokerAccountCommandActor), "_receiveMap", 7);
        AssertMapCount(typeof(BrokerOrderQueryActor), "_parseMap", 2);
        AssertMapCount(typeof(BrokerOrderQueryActor), "_receiveMap", 2);
        AssertMapCount(typeof(BrokerOrderQueryActor), "_exceptionMap", 2);
        AssertMapCount(typeof(BrokerAccountQueryActor), "_parseMap", 1);
        AssertMapCount(typeof(BrokerAccountQueryActor), "_receiveMap", 1);
        AssertMapCount(typeof(BrokerAccountQueryActor), "_exceptionMap", 1);
        AssertMapCount(typeof(BrokerOrderRealtimeActor), "_parseMap", 1);
        AssertMapCount(typeof(BrokerOrderRealtimeActor), "_receiveMap", 1);

        foreach (var handler in new[]
        {
            typeof(CreateBrokerOrder), typeof(RecordBrokerDispatch),
            typeof(RecordBrokerOrderObservation), typeof(RequestBrokerOrderLimitUpdate),
            typeof(RequestBrokerOrderCancel), typeof(RecordBrokerAccountSnapshot),
            typeof(SubmitAccountQualificationEvidence), typeof(AcceptAccountQualification),
            typeof(RevokeAccountQualification), typeof(SetManualTradingHold),
            typeof(ReleaseManualTradingHold), typeof(RequestBrokerAccountResynchronization),
            typeof(GetBrokerOrder), typeof(GetBrokerOrdersForTradeOrder),
            typeof(GetBrokerAccount), typeof(FuturesTickQuoteDataChanged)
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
