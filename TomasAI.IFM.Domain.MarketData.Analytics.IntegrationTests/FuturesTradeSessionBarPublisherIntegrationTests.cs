using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Observation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests;

/// <summary>Verifies the live-to-durable Futures Trade Session Bar Publisher path.</summary>
[Trait("Category", "Integration")]
public sealed class FuturesTradeSessionBarPublisherIntegrationTests(
    WebApplicationFactory<Program> factory,
    MarketDataAnalyticsFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<MarketDataAnalyticsFixture>
{
    readonly IActorProducer producer = factory.Services.GetRequiredService<IActorProducer>();

    /// <summary>Publishes one live trade and observes a projected terminal bar event.</summary>
    [Fact]
    public async Task LiveTrade_ProducesDurableProjectedSessionBar()
    {
        var contractId = $"TBAR{Guid.NewGuid():N}"[..18];
        var timestamp = DateTimeOffset.UtcNow;
        var valueDate = factory.Services.GetRequiredService<IMarketSessionCalendar>()
            .GetValueDate(timestamp);
        var epoch = Guid.NewGuid();
        var terminal = new TaskCompletionSource<FuturesTradeSessionBarPublishedCompleteEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var listener = new NatsActorEventListener(
            new NatsEventListenerOptions(),
            Substitute.For<ILogger<NatsActorEventListener>>());

        await listener.StartAsync(
            $"trade-session-bar-{Guid.NewGuid():N}",
            new()
            {
                [new ActorMailboxId(ActorType.Event, FuturesTradeSessionBarPublishedEvent.Actor)] =
                [
                    FuturesTradeSessionBarPublishedCompleteEvent.Verb,
                    FuturesTradeSessionBarPublishedFailEvent.Verb
                ]
            },
            OnEventAsync);

        try
        {
            var marketEvent = CreateTrade(contractId, valueDate, timestamp, epoch, 1, 6500.25m);
            await producer.SendAsync<FuturesMarketPriceUpdatedRealtimeEvent, TickDataEntityId>(
                marketEvent.Subject,
                marketEvent);
            var closingEvent = CreateTrade(
                contractId,
                valueDate,
                timestamp.AddSeconds(16),
                epoch,
                2,
                6501m);
            await producer.SendAsync<FuturesMarketPriceUpdatedRealtimeEvent, TickDataEntityId>(
                closingEvent.Subject,
                closingEvent);

            var completed = await terminal.Task.WaitAsync(TimeSpan.FromSeconds(20));

            completed.Bar.ContractId.Should().Be(contractId);
            completed.Bar.TimeFrame.Should().Be(TimeFrameType.FifteenSeconds);
            completed.Bar.Open.Should().Be(6500.25m);
            completed.Bar.Close.Should().Be(6500.25m);
            completed.Bar.Volume.Should().Be(3m);
            completed.Bar.TradeCount.Should().Be(1);
            completed.CommandId.Should().Be(completed.Bar.ObservationId.Value);

            var commandSubject = new ActorSubject(
                ActorType.Command,
                PublishFuturesTradeSessionBarCommand.Actor,
                PublishFuturesTradeSessionBarCommand.Verb,
                completed.EntityId.Format());
            (await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync(commandSubject.ThreadId.ToString()))
                .Should().BeGreaterThan(0);
        }
        finally
        {
            await listener.StopAsync();
        }

        ValueTask OnEventAsync(string verb, NatsMsg<byte[]> message)
        {
            if (verb == FuturesTradeSessionBarPublishedCompleteEvent.Verb)
            {
                var completed = message.AsEvent<FuturesTradeSessionBarPublishedCompleteEvent>();
                if (completed is not null
                    && completed.Bar.ContractId == contractId
                    && completed.Bar.TimeFrame == TimeFrameType.FifteenSeconds)
                    terminal.TrySetResult(completed);
            }
            else if (verb == FuturesTradeSessionBarPublishedFailEvent.Verb)
            {
                var failed = message.AsEvent<FuturesTradeSessionBarPublishedFailEvent>();
                if (failed is not null && failed.EntityId.MarketSeriesIdentity.Format().Contains(
                        contractId,
                        StringComparison.Ordinal))
                    terminal.TrySetException(new InvalidOperationException(failed.ErrorMessage));
            }
            return ValueTask.CompletedTask;
        }
    }

    static FuturesMarketPriceUpdatedRealtimeEvent CreateTrade(
        string contractId,
        DateOnly valueDate,
        DateTimeOffset timestamp,
        Guid epoch,
        long ordinal,
        decimal price)
    {
        var entityId = new TickDataEntityId(contractId, valueDate, AssetTypeId.Futures);
        return new()
        {
            Subject = new(
                ActorType.Realtime,
                FuturesMarketPriceUpdatedRealtimeEvent.Actor,
                FuturesMarketPriceUpdatedRealtimeEvent.Verb,
                entityId.Format()),
            Id = Guid.NewGuid(),
            EntityId = entityId,
            AggregateId = entityId.Format(),
            EventSource = nameof(FuturesTradeSessionBarPublisherIntegrationTests),
            ReceivedOn = timestamp.UtcDateTime,
            UpdateSource = FuturesMarketPriceUpdateSource.Trade,
            Price = new FuturesMarketPriceSnapshot(
                contractId,
                checked((uint)ordinal),
                checked((ushort)ordinal),
                AssetTypeId.Futures,
                valueDate,
                null,
                new FuturesMarketTradeSnapshot(
                    price,
                    3,
                    ordinal,
                    timestamp,
                    timestamp,
                    NormalizedTradeAction.New,
                    NormalizedTradeSide.Buy,
                    NormalizedTradeConditionFlags.None,
                    epoch,
                    ordinal))
        };
    }
}
