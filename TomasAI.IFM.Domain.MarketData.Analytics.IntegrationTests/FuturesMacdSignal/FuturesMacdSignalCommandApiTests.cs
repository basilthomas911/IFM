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

namespace TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests.FuturesMacdSignal;

public class FuturesMacdSignalCommandApiTests(WebApplicationFactory<Program> factory, MarketDataAnalyticsFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<MarketDataAnalyticsFixture>
{
    readonly IActorProducer _actorProducer = factory.Services.GetRequiredService<IActorProducer>();
    readonly ILogger<NatsActorEventListener> _logger = Substitute.For<ILogger<NatsActorEventListener>>();

    [Fact]
    public async Task GenerateFuturesMacdSignal_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        FuturesMacdSignalGeneratedEvent futuresMacdSignalGeneratedEvent = default!;
        FuturesMacdSignalGeneratedCompleteEvent futuresMacdSignalGeneratedCompleteEvent = default!;
        FuturesMacdSignalGeneratedFailEvent futuresMacdSignalGeneratedFailEvent = default!;
        var terminalEventReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, FuturesMacdSignalGeneratedEvent.Actor)] =
                [
                    FuturesMacdSignalGeneratedEvent.Verb,
                    FuturesMacdSignalGeneratedCompleteEvent.Verb,
                    FuturesMacdSignalGeneratedFailEvent.Verb
                ]
            },
            EventHandlerAsync
        );

        var contractId = SampleData.ContractId;
        var valueDate = SampleData.ValueDate;
        var macdSignalId = SampleData.MacdSignalId;
        var futuresPrice = (decimal)SampleData.FuturesPrice;

        var entityId = SampleData.MacdEntityId;
        var subject = new ActorSubject(ActorType.Command, GenerateFuturesMacdSignalCommand.Actor, GenerateFuturesMacdSignalCommand.Verb, entityId.Format());
        var eventStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{subject.ThreadId}");
        if (eventStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(eventStreamId);

        // act...
        var marketDataAnalyticsApi = new MarketDataAnalyticsCommandApi(_actorProducer);
        var response = await marketDataAnalyticsApi.GenerateFuturesMacdSignalAsync(macdSignalId, futuresPrice);

        await terminalEventReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
        futuresMacdSignalGeneratedEvent.Should().NotBeNull();
        futuresMacdSignalGeneratedCompleteEvent.Should().NotBeNull();
        futuresMacdSignalGeneratedFailEvent.Should().BeNull();

        futuresMacdSignalGeneratedEvent.FuturesMacdSignal.Should().NotBeNull();
        futuresMacdSignalGeneratedEvent.FuturesMacdSignal.ContractId.Should().Be(contractId);
        futuresMacdSignalGeneratedEvent.FuturesMacdSignal.ValueDate.Should().Be(valueDate);
        futuresMacdSignalGeneratedEvent.EntityId.SignalEmaPeriod.Should().Be(9);
        futuresMacdSignalGeneratedEvent.EntityId.FastEmaPeriod.Should().Be(12);
        futuresMacdSignalGeneratedEvent.EntityId.SlowEmaPeriod.Should().Be(26);

        var lastSignal = await dbFixture.MarketDataDb.GetLastFuturesMacdSignalAsync(
            contractId,
            valueDate,
            SampleData.TimePeriod,
            macdSignalId.SignalEmaPeriod,
            macdSignalId.FastEmaPeriod,
            macdSignalId.SlowEmaPeriod);
        lastSignal.Should().NotBeNull();
        lastSignal!.ContractId.Should().Be(contractId);
        lastSignal.ValueDate.Should().Be(valueDate);

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == FuturesMacdSignalGeneratedEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesMacdSignalGeneratedEvent>()!),
                _ when eventVerb == FuturesMacdSignalGeneratedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesMacdSignalGeneratedCompleteEvent>()!),
                _ when eventVerb == FuturesMacdSignalGeneratedFailEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesMacdSignalGeneratedFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is FuturesMacdSignalGeneratedEvent generated)
                    futuresMacdSignalGeneratedEvent = generated;
                if (@event is FuturesMacdSignalGeneratedCompleteEvent generatedComplete)
                {
                    futuresMacdSignalGeneratedCompleteEvent = generatedComplete;
                    terminalEventReceived.TrySetResult();
                }
                if (@event is FuturesMacdSignalGeneratedFailEvent generatedFail)
                {
                    futuresMacdSignalGeneratedFailEvent = generatedFail;
                    terminalEventReceived.TrySetResult();
                }
                return @event;
            }
        }
    }
}
