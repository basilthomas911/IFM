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
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

namespace TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests.FuturesAtrSignal;

public class FuturesAtrSignalCommandApiTests(WebApplicationFactory<Program> factory, MarketDataAnalyticsFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<MarketDataAnalyticsFixture>
{
    readonly IActorProducer _actorProducer = factory.Services.GetRequiredService<IActorProducer>();
    readonly ILogger<NatsActorEventListener> _logger = Substitute.For<ILogger<NatsActorEventListener>>();

    [Fact]
    public async Task GenerateFuturesAtrSignal_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        FuturesAtrSignalGeneratedEvent futuresAtrSignalGeneratedEvent = default!;
        FuturesAtrSignalGeneratedCompleteEvent futuresAtrSignalGeneratedCompleteEvent = default!;
        FuturesAtrSignalGeneratedFailEvent futuresAtrSignalGeneratedFailEvent = default!;
        var contractId = SampleData.ContractId;
        var valueDate = SampleData.ValueDate;
        var atrSignalId = SampleData.AtrSignalId with { TimePeriod = TimeFrameType.FifteenSeconds };
        var entityId = atrSignalId.ToEntityId();
        var series = MarketSeriesIdentity.ForContract(contractId);
        var intervalEnd = new DateTimeOffset(2026, 9, 14, 14, 0, 15, TimeSpan.Zero);
        var observation = new FuturesTradeSessionBarReadModel
        {
            MarketSeriesIdentity = series,
            ObservationId = FuturesTradeSessionBarId.Create(series, atrSignalId.TimePeriod, intervalEnd, 1),
            ContractId = contractId,
            ValueDate = valueDate,
            TimeFrame = atrSignalId.TimePeriod,
            IntervalStartUtc = intervalEnd.AddSeconds(-15),
            IntervalEndUtc = intervalEnd,
            Open = 100m,
            High = 101m,
            Low = 99m,
            Close = 100m,
            Volume = 100m,
            TradeCount = 10,
            PriceVolumeSum = 10_000m,
            FirstSourceSequence = 1,
            LastSourceSequence = 1,
            FirstMarketEventUtc = intervalEnd.AddSeconds(-10),
            LastMarketEventUtc = intervalEnd,
            CalculatedAtUtc = intervalEnd,
            SchemaVersion = 1,
            CalculationVersion = "integration-v1",
            IsComplete = true,
            IsValid = true,
            ValidationIssues = [],
            CalculationMethod = MarketSignalCalculationMethod.ClosedObservation
        };

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, FuturesAtrSignalGeneratedEvent.Actor)] =
                [
                    FuturesAtrSignalGeneratedEvent.Verb,
                    FuturesAtrSignalGeneratedCompleteEvent.Verb,
                    FuturesAtrSignalGeneratedFailEvent.Verb
                ]
            },
            EventHandlerAsync
        );

        var subject = new ActorSubject(ActorType.Command, GenerateFuturesAtrSignalCommand.Actor, GenerateFuturesAtrSignalCommand.Verb, entityId.Format());
        var eventStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{subject.ThreadId}");
        if (eventStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(eventStreamId);
        await dbFixture.MarketDataDb.DeleteFuturesAtrSignalAsync(contractId, valueDate, atrSignalId.TimePeriod, atrSignalId.PeriodLength);

        // act...
        var marketDataAnalyticsApi = new MarketDataAnalyticsCommandApi(_actorProducer);
        var response = await marketDataAnalyticsApi.GenerateFuturesAtrSignalAsync(atrSignalId, observation);

        await Task.Delay(1000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
        futuresAtrSignalGeneratedEvent.Should().NotBeNull();
        futuresAtrSignalGeneratedCompleteEvent.Should().NotBeNull();
        futuresAtrSignalGeneratedFailEvent.Should().BeNull();

        futuresAtrSignalGeneratedEvent.FuturesAtrSignal.Should().NotBeNull();
        futuresAtrSignalGeneratedEvent.FuturesAtrSignal.ContractId.Should().Be(contractId);
        futuresAtrSignalGeneratedEvent.FuturesAtrSignal.ValueDate.Should().Be(valueDate);

        var lastSignal = await dbFixture.MarketDataDb.GetLastFuturesAtrSignalAsync(contractId, valueDate, atrSignalId.TimePeriod, atrSignalId.PeriodLength);
        lastSignal.Should().NotBeNull();
        lastSignal!.ContractId.Should().Be(contractId);
        lastSignal.ValueDate.Should().Be(valueDate);

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == FuturesAtrSignalGeneratedEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesAtrSignalGeneratedEvent>()!),
                _ when eventVerb == FuturesAtrSignalGeneratedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesAtrSignalGeneratedCompleteEvent>()!),
                _ when eventVerb == FuturesAtrSignalGeneratedFailEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesAtrSignalGeneratedFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is IEvent<FuturesAtrSignalEntityId> routed
                    && routed.EntityId != entityId)
                    return @event;
                if (@event is FuturesAtrSignalGeneratedEvent generated)
                    futuresAtrSignalGeneratedEvent = generated;
                if (@event is FuturesAtrSignalGeneratedCompleteEvent generatedComplete)
                    futuresAtrSignalGeneratedCompleteEvent = generatedComplete;
                if (@event is FuturesAtrSignalGeneratedFailEvent generatedFail)
                    futuresAtrSignalGeneratedFailEvent = generatedFail;
                return @event;
            }
        }
    }
}
