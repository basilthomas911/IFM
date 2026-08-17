using FluentAssertions;
using System.Collections.Concurrent;
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
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests.FuturesTradeSignal;

public class FuturesTradeSignalCommandApiTests(WebApplicationFactory<Program> factory, MarketDataAnalyticsFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<MarketDataAnalyticsFixture>
{
    readonly IActorProducer _actorProducer = factory.Services.GetRequiredService<IActorProducer>();
    readonly ILogger<NatsActorEventListener> _logger = Substitute.For<ILogger<NatsActorEventListener>>();

    [Fact]
    public async Task UpdateFuturesTradeSignal_Ok()
    {
        // arrange...
        var notificationListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        var notifications = new ConcurrentDictionary<Guid, FuturesTradeSignalUpdatedNotifyEvent>();
        await notificationListener.StartAsync(
            "TestFuturesTradeSignalNotificationListener",
            new()
            {
                [new ActorMailboxId(
                    ActorType.Notify,
                    FuturesTradeSignalUpdatedNotifyEvent.Actor)] =
                [
                    FuturesTradeSignalUpdatedNotifyEvent.Verb
                ]
            },
            NotificationHandlerAsync);

        var contractId = $"ESIT{Guid.NewGuid():N}";
        var valueDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var futuresEodData = SampleData.FuturesEodData with
        {
            ContractId = contractId,
            ValueDate = valueDate
        };
        var rsiSignal = CreateRsiSignal() with
        {
            ContractId = contractId,
            ValueDate = valueDate
        };
        var tdiSignal = CreateTdiSignal() with
        {
            ContractId = contractId,
            ValueDate = valueDate
        };
        var entityId = new FuturesTradeSignalEntityId(contractId, valueDate, TimeFrameType.FifteenSeconds);
        var subject = new ActorSubject(ActorType.Command, UpdateFuturesTradeSignalCommand.Actor, UpdateFuturesTradeSignalCommand.Verb, entityId.Format());
        var eventStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{subject.ThreadId}");
        if (eventStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(eventStreamId);

        // act...
        var analyticsApi = new MarketDataAnalyticsCommandApi(_actorProducer);
        var response = await analyticsApi.UpdateFuturesTradeSignalAsync(
            futuresEodData,
            rsiSignal,
            tdiSignal,
            CreateItiSignalData(contractId, valueDate),
            20m);

        response.Should().NotBeNull();
        response.Success.Should().BeTrue(response.ErrorMessage);
        response.Value.Should().NotBe(Guid.Empty);
        var expectedCommandId = response.Value;
        await WaitForAsync(() => notifications.ContainsKey(expectedCommandId));
        var futuresTradeSignalNotification = notifications[expectedCommandId];

        // assert...
        futuresTradeSignalNotification.Should().NotBeNull();
        futuresTradeSignalNotification.Subject.ActorType.Should().Be(ActorType.Notify);
        futuresTradeSignalNotification.CommandId.Should().Be(response.Value);
        futuresTradeSignalNotification.EntityId.Should().Be(entityId);
        futuresTradeSignalNotification.FuturesTradeSignal.Should().NotBeNull();
        futuresTradeSignalNotification.FuturesTradeSignal!.ContractId.Should().Be(contractId);
        futuresTradeSignalNotification.FuturesTradeSignal.ValueDate.Should().Be(valueDate);

        var lastSignal = await dbFixture.MarketDataDb.GetLastFuturesTradeSignalAsync(contractId, valueDate);
        lastSignal.Should().NotBeNull();
        lastSignal!.ContractId.Should().Be(contractId);
        lastSignal.ValueDate.Should().Be(valueDate);
        futuresTradeSignalNotification.FuturesTradeSignal.Should().BeEquivalentTo(lastSignal);

        await notificationListener.StopAsync();

        ValueTask NotificationHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            if (eventVerb == FuturesTradeSignalUpdatedNotifyEvent.Verb)
            {
                var notification = eventMsg.AsEvent<FuturesTradeSignalUpdatedNotifyEvent>()!;
                notifications[notification.CommandId] = notification;
            }
            return ValueTask.CompletedTask;
        }

        static async Task WaitForAsync(Func<bool> predicate)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (!predicate())
                await Task.Delay(TimeSpan.FromMilliseconds(25), timeout.Token);
        }
    }

    static FuturesRsiSignalReadModel CreateRsiSignal()
        => new(
            SampleData.ContractId,
            SampleData.ValueDate,
            SampleData.RSITimePeriod,
            14,
            TimeOnly.FromDateTime(SampleData.Timestamp),
            (decimal)SampleData.FuturesPrice,
            1m,
            1m,
            0m,
            0.5m,
            0.3m,
            1.67,
            SampleData.FuturesRSI,
            SampleData.FuturesRSI,
            SampleData.FuturesRSISlope);

    static FuturesTdiSignalReadModel CreateTdiSignal()
        => new(
            SampleData.ContractId,
            SampleData.ValueDate,
            TimeFrameType.FifteenSeconds,
            TimeOnly.FromDateTime(SampleData.Timestamp),
            2,
            1,
            FuturesTrendDirectionType.UpTrending,
            FuturesTrendDirectionStrengthType.Medium);

    static FuturesItiSignalDataReadModel CreateItiSignalData(string contractId, DateOnly valueDate)
    {
        var signal = SampleData.StartOfDayEvent.FuturesItiSignal! with
        {
            ContractId = contractId,
            ValueDate = valueDate,
            SequenceId = 1,
            TradingDays = 1
        };
        return new(signal, signal, signal);
    }
}
