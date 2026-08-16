using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Net;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesMarketPrice.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests.FuturesItiSignal;

[Trait("Category", "Integration")]
[Collection(ItiPipelineIntegrationCollection.Name)]
public sealed class FuturesItiSignalRealtimeActorIntegrationTests
{
    const string EsContractId = "ES20260918";
    const string VxContractId = "VX20260916";
    static readonly DateOnly ValueDate = new(2026, 8, 14);
    static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);
    readonly string _url =
        Environment.GetEnvironmentVariable("IFM_NATS_URL") ?? "nats://localhost:4222";

    [Fact]
    public async Task CoreNatsMarketPrice_RoutesToItiActor_AndHandsOffDurableCommand()
    {
        var commandObserved = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var queues = Substitute.For<IActorThreadQueues>();
        var mailbox = Substitute.For<IActorMailbox>();
        mailbox.ThreadQueues.Returns(queues);
        var producer = Substitute.For<IActorProducer>();
        producer.StartAsync(Arg.Any<ActorMailboxId>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.CompletedTask);
        producer.StopAsync().Returns(ValueTask.CompletedTask);
        var supervisor = Substitute.For<IActorSupervisor>();
        supervisor.CreateMailbox(Arg.Any<ActorMailboxId>()).Returns(mailbox);
        supervisor.GetProducer(Arg.Any<ActorMailboxId>()).Returns(producer);

        var commandApi = Substitute.For<IActorMarketDataAnalyticsCommandApi>();
        commandApi.GenerateFuturesItiSignalAsync(
                Arg.Any<string>(),
                Arg.Any<DateOnly>(),
                Arg.Any<TimeFrameType>(),
                Arg.Any<DateTime>(),
                Arg.Any<double>(),
                Arg.Any<double>(),
                Arg.Any<Guid?>(),
                Arg.Any<DateOnly?>())
            .Returns(call =>
            {
                commandObserved.TrySetResult(true);
                return ValueTask.FromResult<ServiceResult<GuidResult>>(
                    new ServiceOk<GuidResult>(new GuidResult(Guid.NewGuid())));
            });
        var commandApiFactory = Substitute.For<IActorMarketDataAnalyticsCommandApiFactory>();
        commandApiFactory.Create(Arg.Any<IEventActorContext>()).Returns(commandApi);
        var marketDataApi = CreateReadyMarketDataApi();
        var primaryActor = new FuturesMarketPriceRealtimeActor(
            supervisor,
            Substitute.For<ILogger<FuturesMarketPriceRealtimeActor>>());
        var itiActor = new FuturesItiSignalRealtimeActor(
            supervisor,
            commandApiFactory,
            marketDataApi,
            CreateDbFactory(),
            Substitute.For<ILogger<FuturesItiSignalRealtimeActor>>());
        supervisor.ActorExists(primaryActor.Id).Returns(true);
        supervisor.GetRealtimeRoutes(Arg.Any<ActorTypeId>())
            .Returns(ImmutableHashSet.Create(itiActor.Id));
        supervisor.Children.Returns(new Dictionary<ActorMailboxId, IActor>
        {
            [primaryActor.Id] = primaryActor,
            [itiActor.Id] = itiActor
        });
        queues.TryAdmitAsync(
                Arg.Any<IActorMessage>(),
                Arg.Any<ActorSubject>(),
                Arg.Any<CancellationToken>())
            .Returns(call => AdmitAsync(primaryActor, itiActor, call));

        var consumer = new NatsActorConsumer(
            new NatsConsumerOptions
            {
                Url = _url,
                DispatcherCount = 1,
                DispatcherCapacity = 16,
                SubscriptionCapacity = 16,
                FireAndForgetTraffic = new Dictionary<ActorType, CoreNatsTrafficClass>
                {
                    [ActorType.Realtime] = CoreNatsTrafficClass.Optional
                }
            },
            Substitute.For<ILogger>());
        var publisher = new NatsActorProducer(
            new NatsProducerOptions { Url = _url },
            Substitute.For<ILogger>());
        var @event = CreateEvent();

        try
        {
            await primaryActor.StartAsync(supervisor);
            await itiActor.StartAsync(supervisor);
            await consumer.StartAsync(
                supervisor,
                ActorType.Realtime,
                $"futures-iti-realtime-{Guid.NewGuid():N}");
            await Task.Delay(250);

            await publisher.SendAsync<FuturesMarketPriceUpdatedRealtimeEvent, TickDataEntityId>(
                @event.Subject,
                @event);

            (await commandObserved.Task.WaitAsync(TestTimeout)).Should().BeTrue();
            await commandApi.Received(1).GenerateFuturesItiSignalAsync(
                EsContractId,
                ValueDate,
                TimeFrameType.Daily,
                @event.Price.Trade!.Value.EventTimestamp.UtcDateTime,
                5450.25,
                22.75,
                null,
                ValueDate);
        }
        finally
        {
            await publisher.StopAsync();
            await consumer.StopAsync();
            await itiActor.StopAsync();
            await primaryActor.StopAsync();
        }
    }

    static async ValueTask<ActorAdmissionResult> AdmitAsync(
        FuturesMarketPriceRealtimeActor primaryActor,
        FuturesItiSignalRealtimeActor itiActor,
        NSubstitute.Core.CallInfo call)
    {
        var message = call.Arg<IActorMessage>();
        var subject = call.Arg<ActorSubject>();
        if (subject.ActorId == primaryActor.Id)
            await primaryActor.HandleMessageAsync(message, subject.ThreadId).ConfigureAwait(false);
        else if (subject.ActorId == itiActor.Id)
            await itiActor.HandleMessageAsync(message, subject.ThreadId).ConfigureAwait(false);
        else
            return ActorAdmissionResult.Rejected(ActorAdmissionReason.MailboxRetired);
        return ActorAdmissionResult.AcceptedResult;
    }

    static IMarketDataApi CreateReadyMarketDataApi()
    {
        var api = Substitute.For<IMarketDataApi>();
        var es = Contract("ES", EsContractId, "ESU6", new DateOnly(2026, 9, 18));
        var vx = Contract("VX", VxContractId, "VXU6", new DateOnly(2026, 9, 16));
        api.TryGetCurrentlyTradedFuturesContract("ES", out Arg.Any<FuturesContractV2ReadModel>()!)
            .Returns(call =>
            {
                call[1] = es;
                return true;
            });
        api.TryGetCurrentlyTradedFuturesContract("VX", out Arg.Any<FuturesContractV2ReadModel>()!)
            .Returns(call =>
            {
                call[1] = vx;
                return true;
            });
        api.IsTickDataStreamActive(EsContractId).Returns(true);
        api.IsTickDataStreamActive(VxContractId).Returns(true);
        api.GetFuturesPriceAsync(VxContractId).Returns(Task.FromResult(22.75m));
        return api;
    }

    static IDbContextFactory CreateDbFactory()
    {
        var marketDataDb = Substitute.For<IMarketDataDbContext>();
        marketDataDb.GetFuturesItiTimeFrameStateAsync(
                Arg.Any<string>(),
                Arg.Any<TimeFrameType>(),
                Arg.Any<DateOnly>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<FuturesItiSignalV2ReadModel?>(null));
        marketDataDb.GetFuturesItiSignalsForContractAsync(
                Arg.Any<string>(),
                Arg.Any<DateOnly>(),
                Arg.Any<DateOnly>())
            .Returns(Task.FromResult<ICollection<FuturesItiSignalV2ReadModel>>([]));
        var factory = Substitute.For<IDbContextFactory>();
        factory.MarketDataDb.Returns(marketDataDb);
        return factory;
    }

    static FuturesMarketPriceUpdatedRealtimeEvent CreateEvent()
    {
        var entityId = new TickDataEntityId(EsContractId, ValueDate, AssetTypeId.Futures);
        var timestamp = DateTimeOffset.UtcNow;
        return new FuturesMarketPriceUpdatedRealtimeEvent
        {
            Subject = new ActorSubject(
                ActorType.Realtime,
                FuturesMarketPriceUpdatedRealtimeEvent.Actor,
                FuturesMarketPriceUpdatedRealtimeEvent.Verb,
                entityId.Format()),
            Id = Guid.NewGuid(),
            EntityId = entityId,
            CommandId = Guid.NewGuid(),
            AggregateId = entityId.Format(),
            EventSource = "integration-test",
            ReceivedOn = timestamp.UtcDateTime,
            Price = new FuturesMarketPriceSnapshot(
                EsContractId,
                42,
                7,
                AssetTypeId.Futures,
                ValueDate,
                null,
                new FuturesMarketTradeSnapshot(
                    5450.25m,
                    5,
                    101,
                    timestamp,
                    timestamp))
        };
    }

    static FuturesContractV2ReadModel Contract(
        string symbol,
        string contractId,
        string localSymbol,
        DateOnly maturity) => new(
            contractId,
            $"{symbol} future",
            symbol,
            localSymbol,
            "FUT",
            "USD",
            symbol == "VX" ? "CFE" : "CME",
            symbol == "VX" ? "1000" : "50",
            maturity,
            true);
}
