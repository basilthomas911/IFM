using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Net;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.OperationsHealth;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesMarketPrice.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests.FuturesItiSignal;

/// <summary>Verifies Core NATS routing into the thin event-sourced Daily ITI command boundary.</summary>
[Trait("Category", "Integration")]
[Collection(ItiPipelineIntegrationCollection.Name)]
public sealed class FuturesItiSignalRealtimeActorIntegrationTests
{
    const string EsContractId = "ES-ITI-INGRESS-INTEGRATION";
    const string VxContractId = "VX-ITI-INGRESS-INTEGRATION";
    static readonly DateOnly ValueDate = new(2026, 9, 14);
    readonly string url = Environment.GetEnvironmentVariable("IFM_NATS_URL") ?? "nats://localhost:4222";

    [Fact]
    public async Task CoreNatsCurrentEsTradeRoutesToExactlyOneDailyCommand()
    {
        var commandObserved = new TaskCompletionSource<GenerateFuturesItiSignalCommand>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var queues = Substitute.For<IActorThreadQueues>();
        var mailbox = Substitute.For<IActorMailbox>();
        mailbox.ThreadQueues.Returns(queues);
        var producer = Substitute.For<IActorProducer>();
        producer.StartAsync(Arg.Any<ActorMailboxId>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.CompletedTask);
        producer.StopAsync().Returns(ValueTask.CompletedTask);
        producer.RequestAsync<GenerateFuturesItiSignalCommand, FuturesItiSignalEntityId, GuidResult>(
                Arg.Any<ActorSubject>(), Arg.Any<GenerateFuturesItiSignalCommand>(),
                Arg.Any<FuturesItiSignalEntityId>())
            .Returns(call =>
            {
                var command = call.ArgAt<GenerateFuturesItiSignalCommand>(1);
                commandObserved.TrySetResult(command);
                return ValueTask.FromResult<ServiceResult<GuidResult>>(
                    new ServiceOk<GuidResult>(new GuidResult(command.CommandId)));
            });
        var supervisor = Substitute.For<IActorSupervisor>();
        supervisor.CreateMailbox(Arg.Any<ActorMailboxId>()).Returns(mailbox);
        supervisor.GetProducer(Arg.Any<ActorMailboxId>()).Returns(producer);
        var primary = new FuturesMarketPriceRealtimeActor(new FuturesMarketPriceRealtimeContext(
            supervisor, Substitute.For<ILogger<FuturesMarketPriceRealtimeActor>>()));
        var telemetry = new FuturesItiSignalRuntimeTelemetry(TimeProvider.System);
        var iti = new FuturesItiSignalRealtimeActor(new FuturesItiSignalRealtimeContext(
            supervisor, MarketData(), new LivePipelineEvidence(TimeProvider.System), telemetry,
            Substitute.For<ILogger<FuturesItiSignalRealtimeActor>>()));
        supervisor.ActorExists(primary.Id).Returns(true);
        supervisor.GetRealtimeRoutes(Arg.Any<ActorTypeId>())
            .Returns(ImmutableArray.Create(new RealtimeActorRoute(iti.Id)));
        supervisor.Children.Returns(new Dictionary<ActorMailboxId, IActor>
        {
            [primary.Id] = primary,
            [iti.Id] = iti
        });
        queues.TryAdmitAsync(Arg.Any<IActorMessage>(), Arg.Any<ActorSubject>(), Arg.Any<CancellationToken>())
            .Returns(call => AdmitAsync(primary, iti, call));
        var consumer = new NatsActorConsumer(new NatsConsumerOptions
        {
            Url = url,
            DispatcherCount = 1,
            DispatcherCapacity = 16,
            SubscriptionCapacity = 16,
            FireAndForgetTraffic = new Dictionary<ActorType, CoreNatsTrafficClass>
            {
                [ActorType.Realtime] = CoreNatsTrafficClass.Optional
            }
        }, Substitute.For<ILogger>());
        var publisher = new NatsActorProducer(new NatsProducerOptions { Url = url }, Substitute.For<ILogger>());
        var @event = Event();

        try
        {
            await primary.StartAsync(supervisor);
            await iti.StartAsync(supervisor);
            await consumer.StartAsync(supervisor, ActorType.Realtime, $"iti-ingress-{Guid.NewGuid():N}");
            await Task.Delay(250);

            await publisher.SendAsync<FuturesMarketPriceUpdatedRealtimeEvent, TickDataEntityId>(
                @event.Subject, @event);

            var command = await commandObserved.Task.WaitAsync(TimeSpan.FromSeconds(10));
            command.TimePeriod.Should().Be(TimeFrameType.Daily);
            command.CommandId.Should().Be(@event.Id);
            command.FuturesPrice.Should().Be(5450.25);
            command.VixFuturesPrice.Should().Be(22.75);
            telemetry.GetSnapshot().CommandRequests.Should().Be(1);
            await producer.Received(1)
                .RequestAsync<GenerateFuturesItiSignalCommand, FuturesItiSignalEntityId, GuidResult>(
                    Arg.Any<ActorSubject>(), Arg.Any<GenerateFuturesItiSignalCommand>(),
                    Arg.Any<FuturesItiSignalEntityId>());
        }
        finally
        {
            await publisher.StopAsync();
            await consumer.StopAsync();
            await iti.StopAsync();
            await primary.StopAsync();
        }
    }

    static async ValueTask<ActorAdmissionResult> AdmitAsync(
        FuturesMarketPriceRealtimeActor primary,
        FuturesItiSignalRealtimeActor iti,
        NSubstitute.Core.CallInfo call)
    {
        var message = call.Arg<IActorMessage>();
        var subject = call.Arg<ActorSubject>();
        if (subject.ActorId == primary.Id)
            await primary.HandleMessageAsync(message, subject.ThreadId).ConfigureAwait(false);
        else if (subject.ActorId == iti.Id)
            await iti.HandleMessageAsync(message, subject.ThreadId).ConfigureAwait(false);
        else
            return ActorAdmissionResult.Rejected(ActorAdmissionReason.MailboxRetired);
        return ActorAdmissionResult.AcceptedResult;
    }

    static IMarketDataApi MarketData()
    {
        var api = Substitute.For<IMarketDataApi>();
        var es = Contract("ES", EsContractId, "ESU6", new DateOnly(2026, 9, 18));
        var vx = Contract("VX", VxContractId, "VXU6", new DateOnly(2026, 9, 16));
        api.TryGetOnTheRunFuturesContract("ES", out Arg.Any<FuturesContractV3ReadModel>()!)
            .Returns(call => { call[1] = es; return true; });
        api.TryGetOnTheRunFuturesContract("VX", out Arg.Any<FuturesContractV3ReadModel>()!)
            .Returns(call => { call[1] = vx; return true; });
        api.TryGetLastTickPrice(VxContractId, out Arg.Any<FuturesMarketPriceSnapshot>())
            .Returns(call => { call[1] = Price(VxContractId, 22.75m); return true; });
        return api;
    }

    static FuturesMarketPriceUpdatedRealtimeEvent Event()
    {
        var entity = new TickDataEntityId(EsContractId, ValueDate, AssetTypeId.Futures);
        var timestamp = DateTimeOffset.UtcNow;
        return new()
        {
            Subject = new(ActorType.Realtime, FuturesMarketPriceUpdatedRealtimeEvent.Actor,
                FuturesMarketPriceUpdatedRealtimeEvent.Verb, entity.Format()),
            Id = Guid.NewGuid(),
            EntityId = entity,
            CommandId = Guid.NewGuid(),
            AggregateId = entity.Format(),
            EventSource = "integration-test",
            ReceivedOn = timestamp.UtcDateTime,
            Price = Price(EsContractId, 5450.25m),
            UpdateSource = FuturesMarketPriceUpdateSource.Trade
        };
    }

    static FuturesMarketPriceSnapshot Price(string contractId, decimal value)
    {
        var timestamp = DateTimeOffset.UtcNow;
        return new(contractId, 42, 7, AssetTypeId.Futures, ValueDate, null,
            new(value, 5, 101, timestamp, timestamp, NormalizedTradeAction.New,
                NormalizedTradeSide.Buy, NormalizedTradeConditionFlags.None, Guid.NewGuid(), 77));
    }

    static FuturesContractV3ReadModel Contract(string symbol, string contractId, string localSymbol, DateOnly maturity)
        => new(contractId, $"{symbol} future", symbol, localSymbol, "FUT", "USD",
            symbol == "VX" ? "CFE" : "CME", symbol == "VX" ? "1000" : "50", maturity, true);
}
