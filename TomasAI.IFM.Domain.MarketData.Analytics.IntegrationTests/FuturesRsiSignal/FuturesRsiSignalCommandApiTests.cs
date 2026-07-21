using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Shared.MarketDataAnalytics.Commands;

namespace TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests.FuturesRsiSignal;

public class FuturesRsiSignalCommandApiTests(WebApplicationFactory<Program> factory, MarketDataAnalyticsFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<MarketDataAnalyticsFixture>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();
    readonly ILogger<NatsActorEventListener> _logger = Substitute.For<ILogger<NatsActorEventListener>>();

    [Fact]
    public async Task StartFuturesRsiSignal_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        FuturesRsiSignalStartedEvent futuresRsiSignalStartedEvent = default!;

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, FuturesRsiSignalStartedEvent.Actor)] = [FuturesRsiSignalStartedEvent.Verb]
            },
            EventHandlerAsync
        );

        var entityId = SampleData.RsiEntityId;

        var subject = new ActorSubject(ActorType.Command, StartFuturesRsiSignalCommand.Actor, StartFuturesRsiSignalCommand.Verb, entityId.Format());
        var eventStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{subject.ThreadId}");
        if (eventStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(eventStreamId);

        // act...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var marketDataAnalyticsApi = new MarketDataAnalyticsCommandApi(commandServiceApi);
        var response = await marketDataAnalyticsApi.StartFuturesRsiSignalAsync(entityId);

        await Task.Delay(1000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
        futuresRsiSignalStartedEvent.Should().NotBeNull();
        futuresRsiSignalStartedEvent.EntityId.Should().Be(entityId);

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == FuturesRsiSignalStartedEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesRsiSignalStartedEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is FuturesRsiSignalStartedEvent started)
                    futuresRsiSignalStartedEvent = started;
                return @event;
            }
        }
    }

    [Fact]
    public async Task StopFuturesRsiSignal_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        FuturesRsiSignalStartedEvent futuresRsiSignalStartedEvent = default!;
        FuturesRsiSignalStoppedEvent futuresRsiSignalStoppedEvent = default!;

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, FuturesRsiSignalStartedEvent.Actor)] = [FuturesRsiSignalStartedEvent.Verb, FuturesRsiSignalStoppedEvent.Verb]
            },
            EventHandlerAsync
        );

        var entityId = SampleData.RsiEntityId;

        var startSubject = new ActorSubject(ActorType.Command, StartFuturesRsiSignalCommand.Actor, StartFuturesRsiSignalCommand.Verb, entityId.Format());
        var startEventStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{startSubject.ThreadId}");
        if (startEventStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(startEventStreamId);

        var stopSubject = new ActorSubject(ActorType.Command, StopFuturesRsiSignalCommand.Actor, StopFuturesRsiSignalCommand.Verb, entityId.Format());
        var stopEventStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{stopSubject.ThreadId}");
        if (stopEventStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(stopEventStreamId);

        // act...

        // step 1: start signal to establish state
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var marketDataAnalyticsApi = new MarketDataAnalyticsCommandApi(commandServiceApi);
        var startResponse = await marketDataAnalyticsApi.StartFuturesRsiSignalAsync(entityId);

        await Task.Delay(1000);

        startResponse.Should().NotBeNull();
        startResponse.Success.Should().BeTrue();

        // step 2: stop signal
        futuresRsiSignalStartedEvent = default!;
        var stopResponse = await marketDataAnalyticsApi.StopFuturesRsiSignalAsync(entityId);

        await Task.Delay(1000);

        // assert...
        stopResponse.Should().NotBeNull();
        stopResponse.Success.Should().BeTrue();
        stopResponse.Value.Should().NotBe(Guid.Empty);
        futuresRsiSignalStoppedEvent.Should().NotBeNull();
        futuresRsiSignalStoppedEvent.EntityId.Should().Be(entityId);

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == FuturesRsiSignalStartedEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesRsiSignalStartedEvent>()!),
                _ when eventVerb == FuturesRsiSignalStoppedEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesRsiSignalStoppedEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is FuturesRsiSignalStartedEvent started)
                    futuresRsiSignalStartedEvent = started;
                if (@event is FuturesRsiSignalStoppedEvent stopped)
                    futuresRsiSignalStoppedEvent = stopped;
                return @event;
            }
        }
    }

    [Fact]
    public async Task GenerateFuturesRsiSignal_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        FuturesRsiSignalGeneratedEvent futuresRsiSignalGeneratedEvent = default!;

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, FuturesRsiSignalGeneratedEvent.Actor)] = [FuturesRsiSignalGeneratedEvent.Verb]
            },
            EventHandlerAsync
        );

        var entityId = SampleData.RsiEntityId;
        var futuresEodData = SampleData.FuturesEodData;

        var subject = new ActorSubject(ActorType.Command, GenerateFuturesRsiSignalCommand.Actor, GenerateFuturesRsiSignalCommand.Verb, entityId.Format());
        var eventStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{subject.ThreadId}");
        if (eventStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(eventStreamId);

        // act...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var marketDataAnalyticsApi = new MarketDataAnalyticsCommandApi(commandServiceApi);
        var response = await marketDataAnalyticsApi.GenerateFuturesRsiSignalAsync(futuresEodData, TradeTimePeriodType.FifteenMinutes, 14);

        await Task.Delay(1000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
        futuresRsiSignalGeneratedEvent.Should().NotBeNull();
        futuresRsiSignalGeneratedEvent.FuturesRsiSignal.Should().NotBeNull();
        futuresRsiSignalGeneratedEvent.FuturesRsiSignal.ContractId.Should().Be(SampleData.ContractId);
        futuresRsiSignalGeneratedEvent.FuturesRsiSignal.ValueDate.Should().Be(SampleData.ValueDate);
        futuresRsiSignalGeneratedEvent.FuturesRsiSignal.Price.Should().Be((decimal)SampleData.FuturesPrice);

        var lastSignal = await dbFixture.MarketDataDb.GetLastFuturesRsiSignalAsync(SampleData.ContractId, SampleData.ValueDate, TradeTimePeriodType.OneMinute, 14);
        lastSignal.Should().NotBeNull();
        lastSignal!.ContractId.Should().Be(SampleData.ContractId);
        lastSignal.ValueDate.Should().Be(SampleData.ValueDate);

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == FuturesRsiSignalGeneratedEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesRsiSignalGeneratedEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is FuturesRsiSignalGeneratedEvent generated)
                    futuresRsiSignalGeneratedEvent = generated;
                return @event;
            }
        }
    }

    [Fact]
    public async Task GenerateFuturesRsiDailySignal_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        FuturesRsiDailySignalGeneratedEvent futuresRsiDailySignalGeneratedEvent = default!;

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, FuturesRsiDailySignalGeneratedEvent.Actor)] = [FuturesRsiDailySignalGeneratedEvent.Verb]
            },
            EventHandlerAsync
        );

        var futuresEodData = SampleData.FuturesEodData;
        var entityId = new FuturesRsiSignalEntityId(futuresEodData.ContractId ?? string.Empty, futuresEodData.ValueDate, SampleData.RSITimePeriod, 14);

        var subject = new ActorSubject(ActorType.Command, GenerateFuturesRsiDailySignalCommand.Actor, GenerateFuturesRsiDailySignalCommand.Verb, entityId.Format());
        var eventStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{subject.ThreadId}");
        if (eventStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(eventStreamId);

        // act...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var marketDataAnalyticsApi = new MarketDataAnalyticsCommandApi(commandServiceApi);
        var response = await marketDataAnalyticsApi.GenerateFuturesRsiDailySignalAsync(futuresEodData, TradeTimePeriodType.Daily, 14);

        await Task.Delay(1000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
        futuresRsiDailySignalGeneratedEvent.Should().NotBeNull();
        futuresRsiDailySignalGeneratedEvent.FuturesRsiSignal.Should().NotBeNull();
        futuresRsiDailySignalGeneratedEvent.FuturesRsiSignal.ContractId.Should().Be(SampleData.ContractId);
        futuresRsiDailySignalGeneratedEvent.FuturesRsiSignal.ValueDate.Should().Be(SampleData.ValueDate);
        futuresRsiDailySignalGeneratedEvent.FuturesRsiSignal.Price.Should().Be((decimal)SampleData.FuturesPrice);

        var lastSignal = await dbFixture.MarketDataDb.GetLastFuturesRsiSignalAsync(SampleData.ContractId, SampleData.ValueDate, TradeTimePeriodType.Daily, 14);
        lastSignal.Should().NotBeNull();
        lastSignal!.ContractId.Should().Be(SampleData.ContractId);
        lastSignal.ValueDate.Should().Be(SampleData.ValueDate);

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == FuturesRsiDailySignalGeneratedEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesRsiDailySignalGeneratedEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is FuturesRsiDailySignalGeneratedEvent generated)
                    futuresRsiDailySignalGeneratedEvent = generated;
                return @event;
            }
        }
    }
}
