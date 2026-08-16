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
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests.FuturesTdiSignal;

public class FuturesTdiSignalCommandApiTests(WebApplicationFactory<Program> factory, MarketDataAnalyticsFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<MarketDataAnalyticsFixture>
{
    readonly IActorProducer _actorProducer = factory.Services.GetRequiredService<IActorProducer>();
    readonly ILogger<NatsActorEventListener> _logger = Substitute.For<ILogger<NatsActorEventListener>>();

    [Fact]
    public async Task GenerateFuturesTdiSignal_Ok()
    {
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        FuturesTdiSignalGeneratedEvent futuresTdiSignalGeneratedEvent = default!;
        FuturesTdiSignalGeneratedCompleteEvent futuresTdiSignalGeneratedCompleteEvent = default!;
        FuturesTdiSignalGeneratedFailEvent futuresTdiSignalGeneratedFailEvent = default!;

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, FuturesTdiSignalGeneratedEvent.Actor)] =
                [
                    FuturesTdiSignalGeneratedEvent.Verb,
                    FuturesTdiSignalGeneratedCompleteEvent.Verb,
                    FuturesTdiSignalGeneratedFailEvent.Verb
                ]
            },
            EventHandlerAsync);

        var valueDate = new DateOnly(2099, 12, 31);
        var timestamp = new TimeOnly(10, 0, 0);
        var futuresTdiSignalId = new FuturesTdiSignalId(SampleData.ContractId, valueDate, timestamp);
        var entityId = new FuturesTdiSignalEntityId(
            SampleData.ContractId,
            valueDate,
            TimeFrameType.OneMinute,
            FuturesTdiConfiguration.StandardConfigurationId);
        var subject = new ActorSubject(ActorType.Command, GenerateFuturesTdiSignalCommand.Actor, GenerateFuturesTdiSignalCommand.Verb, entityId.Format());
        var eventStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{subject.ThreadId}");
        if (eventStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(eventStreamId);

        var analyticsApi = new MarketDataAnalyticsCommandApi(_actorProducer);
        var response = await analyticsApi.GenerateFuturesTdiSignalAsync(futuresTdiSignalId, CreateRsiSignals(valueDate));

        await Task.Delay(1000);

        response.Should().NotBeNull();
        response.Success.Should().BeTrue(response.ErrorMessage);
        response.Value.Should().NotBe(Guid.Empty);
        futuresTdiSignalGeneratedEvent.Should().NotBeNull();
        futuresTdiSignalGeneratedCompleteEvent.Should().NotBeNull();
        futuresTdiSignalGeneratedFailEvent.Should().BeNull();
        futuresTdiSignalGeneratedEvent.FuturesTdiSignal.Should().NotBeNull();
        futuresTdiSignalGeneratedEvent.FuturesTdiSignal.ContractId.Should().Be(SampleData.ContractId);
        futuresTdiSignalGeneratedEvent.FuturesTdiSignal.ValueDate.Should().Be(valueDate);
        futuresTdiSignalGeneratedEvent.FuturesTdiSignal.TimePeriod.Should().Be(TimeFrameType.OneMinute);
        futuresTdiSignalGeneratedEvent.FuturesTdiSignal.Timestamp.Should().Be(timestamp);

        var lastSignal = await dbFixture.MarketDataDb.GetLastFuturesTdiSignalAsync(SampleData.ContractId, valueDate);
        lastSignal.Should().NotBeNull();
        lastSignal!.ContractId.Should().Be(SampleData.ContractId);
        lastSignal.ValueDate.Should().Be(valueDate);
        lastSignal.Timestamp.Should().Be(timestamp);

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == FuturesTdiSignalGeneratedEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesTdiSignalGeneratedEvent>()!),
                _ when eventVerb == FuturesTdiSignalGeneratedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesTdiSignalGeneratedCompleteEvent>()!),
                _ when eventVerb == FuturesTdiSignalGeneratedFailEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesTdiSignalGeneratedFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is FuturesTdiSignalGeneratedEvent generated)
                    futuresTdiSignalGeneratedEvent = generated;
                if (@event is FuturesTdiSignalGeneratedCompleteEvent generatedComplete)
                    futuresTdiSignalGeneratedCompleteEvent = generatedComplete;
                if (@event is FuturesTdiSignalGeneratedFailEvent generatedFail)
                    futuresTdiSignalGeneratedFailEvent = generatedFail;
                return @event;
            }
        }
    }

    static FuturesRsiSignalReadModel[] CreateRsiSignals(DateOnly valueDate)
        => Enumerable.Range(0, FuturesTdiConfiguration.Standard.RequiredRsiSamples)
            .Select(index => new FuturesRsiSignalReadModel(
                SampleData.ContractId,
                valueDate,
                TimeFrameType.OneMinute,
                FuturesTdiConfiguration.Standard.RsiPeriod,
                new TimeOnly(9, 27).AddMinutes(index),
                5500m + index,
                1m,
                1m,
                0m,
                1m,
                0.5m,
                2d,
                40d + index,
                0d,
                1d,
                index + 1,
                valueDate.ToDateTime(new TimeOnly(9, 27)).AddMinutes(index)))
            .ToArray();
}
