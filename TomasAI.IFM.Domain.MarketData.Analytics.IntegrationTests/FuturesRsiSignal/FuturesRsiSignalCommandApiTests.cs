using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;

namespace TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests.FuturesRsiSignal;

public class FuturesRsiSignalCommandApiTests(WebApplicationFactory<Program> factory, MarketDataAnalyticsFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<MarketDataAnalyticsFixture>
{
    readonly IActorProducer _actorProducer = factory.Services.GetRequiredService<IActorProducer>();
    readonly ILogger<NatsActorEventListener> _logger = Substitute.For<ILogger<NatsActorEventListener>>();

    [Fact]
    public async Task StartFuturesRsiSignal_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        FuturesRsiSignalStartedEvent futuresRsiSignalStartedEvent = default!;
        var eventReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

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
        var marketDataAnalyticsApi = new MarketDataAnalyticsCommandApi(_actorProducer);
        var response = await marketDataAnalyticsApi.StartFuturesRsiSignalAsync(entityId);

        await eventReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue(response.ErrorMessage);
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
                {
                    futuresRsiSignalStartedEvent = started;
                    eventReceived.TrySetResult();
                }
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
        var eventReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

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
        var marketDataAnalyticsApi = new MarketDataAnalyticsCommandApi(_actorProducer);
        var startResponse = await marketDataAnalyticsApi.StartFuturesRsiSignalAsync(entityId);

        await eventReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));

        startResponse.Should().NotBeNull();
        startResponse.Success.Should().BeTrue(startResponse.ErrorMessage);

        // step 2: stop signal
        futuresRsiSignalStartedEvent = default!;
        eventReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopResponse = await marketDataAnalyticsApi.StopFuturesRsiSignalAsync(entityId);

        await eventReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // assert...
        stopResponse.Should().NotBeNull();
        stopResponse.Success.Should().BeTrue(stopResponse.ErrorMessage);
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
                {
                    futuresRsiSignalStartedEvent = started;
                    eventReceived.TrySetResult();
                }
                if (@event is FuturesRsiSignalStoppedEvent stopped)
                {
                    futuresRsiSignalStoppedEvent = stopped;
                    eventReceived.TrySetResult();
                }
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
        FuturesRsiSignalGeneratedCompleteEvent futuresRsiSignalGeneratedCompleteEvent = default!;
        FuturesRsiSignalGeneratedFailEvent futuresRsiSignalGeneratedFailEvent = default!;
        var terminalEventReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entityId = new FuturesRsiSignalEntityId(
            SampleData.ContractId,
            SampleData.ValueDate,
            TimeFrameType.FifteenMinutes,
            14);

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, FuturesRsiSignalGeneratedEvent.Actor)] =
                [
                    FuturesRsiSignalGeneratedEvent.Verb,
                    FuturesRsiSignalGeneratedCompleteEvent.Verb,
                    FuturesRsiSignalGeneratedFailEvent.Verb
                ]
            },
            EventHandlerAsync
        );

        var futuresEodData = SampleData.FuturesEodData;

        var subject = new ActorSubject(ActorType.Command, GenerateFuturesRsiSignalCommand.Actor, GenerateFuturesRsiSignalCommand.Verb, entityId.Format());
        var eventStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{subject.ThreadId}");
        if (eventStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(eventStreamId);

        // act...
        var marketDataAnalyticsApi = new MarketDataAnalyticsCommandApi(_actorProducer);
        var response = await marketDataAnalyticsApi.GenerateFuturesRsiSignalAsync(futuresEodData, TimeFrameType.FifteenMinutes, 14);

        await terminalEventReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue(response.ErrorMessage);
        response.Value.Should().NotBe(Guid.Empty);
        futuresRsiSignalGeneratedEvent.Should().NotBeNull();
        futuresRsiSignalGeneratedCompleteEvent.Should().NotBeNull(futuresRsiSignalGeneratedFailEvent?.ErrorMessage);
        futuresRsiSignalGeneratedFailEvent.Should().BeNull();
        futuresRsiSignalGeneratedEvent.FuturesRsiSignal.Should().NotBeNull();
        futuresRsiSignalGeneratedEvent.FuturesRsiSignal.ContractId.Should().Be(SampleData.ContractId);
        futuresRsiSignalGeneratedEvent.FuturesRsiSignal.ValueDate.Should().Be(SampleData.ValueDate);
        futuresRsiSignalGeneratedEvent.FuturesRsiSignal.Price.Should().Be((decimal)SampleData.FuturesPrice);

        var lastSignal = await dbFixture.MarketDataDb.GetLastFuturesRsiSignalAsync(SampleData.ContractId, SampleData.ValueDate, TimeFrameType.FifteenMinutes, 14);
        lastSignal.Should().NotBeNull();
        lastSignal!.ContractId.Should().Be(SampleData.ContractId);
        lastSignal.ValueDate.Should().Be(SampleData.ValueDate);

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == FuturesRsiSignalGeneratedEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesRsiSignalGeneratedEvent>()!),
                _ when eventVerb == FuturesRsiSignalGeneratedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesRsiSignalGeneratedCompleteEvent>()!),
                _ when eventVerb == FuturesRsiSignalGeneratedFailEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesRsiSignalGeneratedFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is IEvent<FuturesRsiSignalEntityId> routed
                    && routed.EntityId != entityId)
                    return @event;
                if (@event is FuturesRsiSignalGeneratedEvent generated)
                    futuresRsiSignalGeneratedEvent = generated;
                if (@event is FuturesRsiSignalGeneratedCompleteEvent generatedComplete)
                {
                    futuresRsiSignalGeneratedCompleteEvent = generatedComplete;
                    terminalEventReceived.TrySetResult();
                }
                if (@event is FuturesRsiSignalGeneratedFailEvent generatedFail)
                {
                    futuresRsiSignalGeneratedFailEvent = generatedFail;
                    terminalEventReceived.TrySetResult();
                }
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
        FuturesRsiDailySignalGeneratedCompleteEvent futuresRsiDailySignalGeneratedCompleteEvent = default!;
        FuturesRsiDailySignalGeneratedFailEvent futuresRsiDailySignalGeneratedFailEvent = default!;
        var terminalEventReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, FuturesRsiDailySignalGeneratedEvent.Actor)] =
                [
                    FuturesRsiDailySignalGeneratedEvent.Verb,
                    FuturesRsiDailySignalGeneratedCompleteEvent.Verb,
                    FuturesRsiDailySignalGeneratedFailEvent.Verb
                ]
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
        var marketDataAnalyticsApi = new MarketDataAnalyticsCommandApi(_actorProducer);
        var response = await marketDataAnalyticsApi.GenerateFuturesRsiDailySignalAsync(futuresEodData, TimeFrameType.Daily, 14);

        await terminalEventReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue(response.ErrorMessage);
        response.Value.Should().NotBe(Guid.Empty);
        futuresRsiDailySignalGeneratedEvent.Should().NotBeNull();
        futuresRsiDailySignalGeneratedCompleteEvent.Should().NotBeNull(futuresRsiDailySignalGeneratedFailEvent?.ErrorMessage);
        futuresRsiDailySignalGeneratedFailEvent.Should().BeNull();
        futuresRsiDailySignalGeneratedEvent.FuturesRsiSignal.Should().NotBeNull();
        futuresRsiDailySignalGeneratedEvent.FuturesRsiSignal.ContractId.Should().Be(SampleData.ContractId);
        futuresRsiDailySignalGeneratedEvent.FuturesRsiSignal.ValueDate.Should().Be(SampleData.ValueDate);
        futuresRsiDailySignalGeneratedEvent.FuturesRsiSignal.Price.Should().Be((decimal)SampleData.FuturesPrice);

        var lastSignal = await dbFixture.MarketDataDb.GetLastFuturesRsiSignalAsync(SampleData.ContractId, SampleData.ValueDate, TimeFrameType.Daily, 14);
        lastSignal.Should().NotBeNull();
        lastSignal!.ContractId.Should().Be(SampleData.ContractId);
        lastSignal.ValueDate.Should().Be(SampleData.ValueDate);

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == FuturesRsiDailySignalGeneratedEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesRsiDailySignalGeneratedEvent>()!),
                _ when eventVerb == FuturesRsiDailySignalGeneratedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesRsiDailySignalGeneratedCompleteEvent>()!),
                _ when eventVerb == FuturesRsiDailySignalGeneratedFailEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesRsiDailySignalGeneratedFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is FuturesRsiDailySignalGeneratedEvent generated)
                    futuresRsiDailySignalGeneratedEvent = generated;
                if (@event is FuturesRsiDailySignalGeneratedCompleteEvent generatedComplete)
                {
                    futuresRsiDailySignalGeneratedCompleteEvent = generatedComplete;
                    terminalEventReceived.TrySetResult();
                }
                if (@event is FuturesRsiDailySignalGeneratedFailEvent generatedFail)
                {
                    futuresRsiDailySignalGeneratedFailEvent = generatedFail;
                    terminalEventReceived.TrySetResult();
                }
                return @event;
            }
        }
    }
}
