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

namespace TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests.FuturesAdxSignal;

public class FuturesAdxSignalCommandApiTests(WebApplicationFactory<Program> factory, MarketDataAnalyticsFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<MarketDataAnalyticsFixture>
{
    readonly IActorProducer _actorProducer = factory.Services.GetRequiredService<IActorProducer>();
    readonly ILogger<NatsActorEventListener> _logger = Substitute.For<ILogger<NatsActorEventListener>>();

    [Fact]
    public async Task GenerateFuturesAdxSignal_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        FuturesAdxSignalGeneratedEvent futuresAdxSignalGeneratedEvent = default!;
        FuturesAdxSignalGeneratedCompleteEvent futuresAdxSignalGeneratedCompleteEvent = default!;
        FuturesAdxSignalGeneratedFailEvent futuresAdxSignalGeneratedFailEvent = default!;
        var contractId = SampleData.ContractId;
        var valueDate = SampleData.ValueDate;
        var adxSignalId = SampleData.AdxSignalId;
        var entityId = SampleData.AdxEntityId;

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, FuturesAdxSignalGeneratedEvent.Actor)] =
                [
                    FuturesAdxSignalGeneratedEvent.Verb,
                    FuturesAdxSignalGeneratedCompleteEvent.Verb,
                    FuturesAdxSignalGeneratedFailEvent.Verb
                ]
            },
            EventHandlerAsync
        );

        var futuresItiSignals = SampleData.CreateItiSignalsForAtr();

        var subject = new ActorSubject(ActorType.Command, GenerateFuturesAdxSignalCommand.Actor, GenerateFuturesAdxSignalCommand.Verb, entityId.Format());
        var eventStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{subject.ThreadId}");
        if (eventStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(eventStreamId);
        await dbFixture.MarketDataDb.DeleteFuturesAdxSignalAsync(contractId, valueDate, adxSignalId.TimePeriod, adxSignalId.PeriodLength);

        // act...
        var marketDataAnalyticsApi = new MarketDataAnalyticsCommandApi(_actorProducer);
        var response = await marketDataAnalyticsApi.GenerateFuturesAdxSignalAsync(adxSignalId, (decimal)SampleData.FuturesPrice);

        await Task.Delay(1000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
        futuresAdxSignalGeneratedEvent.Should().NotBeNull();
        futuresAdxSignalGeneratedCompleteEvent.Should().NotBeNull();
        futuresAdxSignalGeneratedFailEvent.Should().BeNull();

        futuresAdxSignalGeneratedEvent.FuturesAdxSignal.Should().NotBeNull();
        futuresAdxSignalGeneratedEvent.FuturesAdxSignal.ContractId.Should().Be(contractId);
        futuresAdxSignalGeneratedEvent.FuturesAdxSignal.ValueDate.Should().Be(valueDate);

        var lastSignal = await dbFixture.MarketDataDb.GetLastFuturesAdxSignalAsync(contractId, valueDate, SampleData.TimePeriod, SampleData.PeriodLength);
        lastSignal.Should().NotBeNull();
        lastSignal!.ContractId.Should().Be(contractId);
        lastSignal.ValueDate.Should().Be(valueDate);

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == FuturesAdxSignalGeneratedEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesAdxSignalGeneratedEvent>()!),
                _ when eventVerb == FuturesAdxSignalGeneratedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesAdxSignalGeneratedCompleteEvent>()!),
                _ when eventVerb == FuturesAdxSignalGeneratedFailEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesAdxSignalGeneratedFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is IEvent<FuturesAdxSignalEntityId> routed
                    && routed.EntityId != entityId)
                    return @event;
                if (@event is FuturesAdxSignalGeneratedEvent generated)
                    futuresAdxSignalGeneratedEvent = generated;
                if (@event is FuturesAdxSignalGeneratedCompleteEvent generatedComplete)
                    futuresAdxSignalGeneratedCompleteEvent = generatedComplete;
                if (@event is FuturesAdxSignalGeneratedFailEvent generatedFail)
                    futuresAdxSignalGeneratedFailEvent = generatedFail;
                return @event;
            }
        }
    }
}
