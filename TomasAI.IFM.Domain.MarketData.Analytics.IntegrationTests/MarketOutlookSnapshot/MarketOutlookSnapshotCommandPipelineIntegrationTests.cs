using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests.MarketOutlookSnapshot;

/// <summary>Verifies the command-owned Market Outlook state and projection pipeline.</summary>
[Trait("Category", "Integration")]
public sealed class MarketOutlookSnapshotCommandPipelineIntegrationTests(
    WebApplicationFactory<Program> factory,
    MarketDataAnalyticsFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<MarketDataAnalyticsFixture>
{
    readonly IActorProducer _producer = factory.Services.GetRequiredService<IActorProducer>();

    /// <summary>Commits a component and EOD checkpoint, projects both models, and publishes one UI notification.</summary>
    [Fact]
    public async Task ObserveThenPublish_ProjectsWorkingStateSnapshotAndNotification()
    {
        var contractId = $"ESMO{Guid.NewGuid():N}"[..18];
        var valueDate = new DateOnly(2099, 12, 30);
        var entityId = new MarketOutlookEntityId(contractId, valueDate);
        var observedAt = DateTime.UtcNow;
        var observeId = Guid.NewGuid();
        var publishId = Guid.NewGuid();
        var terminal = new TaskCompletionSource<MarketOutlookUpdatedNotifyEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var listener = new NatsActorEventListener(
            new NatsEventListenerOptions(),
            Substitute.For<ILogger<NatsActorEventListener>>());
        await listener.StartAsync(
            $"market-outlook-{Guid.NewGuid():N}",
            new()
            {
                [new ActorMailboxId(ActorType.Notify, MarketOutlookUpdatedNotifyEvent.Actor)] =
                [
                    MarketOutlookUpdatedNotifyEvent.Verb
                ]
            },
            OnNotificationAsync);

        try
        {
            var rsi = SampleData.CreateRsiSignalsForAtr()[0] with
            {
                ContractId = contractId,
                ValueDate = valueDate,
                PeriodLength = FuturesIntradaySignalActivationProfile.RsiPeriodLength,
                IsWarm = true
            };
            var iti = new FuturesItiSignalV2ReadModel
            {
                ContractId = contractId,
                ValueDate = valueDate,
                TimePeriod = TimeFrameType.Daily,
                IntrinsicTimeMode = IntrinsicTimeModeType.Trending,
                IntrinsicTimeTrend = IntrinsicTimeTrendType.UpTrend,
                TrendDelta = 44.5,
                IntrinsicPrice = 5425
            };
            var series = MarketSeriesIdentity.ForFuturesSeries(
                new FuturesSeriesId("ES", "calendar-front", "unadjusted", 1));
            var dailyEnd = new DateTimeOffset(2099, 12, 30, 21, 0, 0, TimeSpan.Zero);
            var metadata = new MarketAnalyticsSignalMetadata
            {
                SignalKey = new(series, MarketAnalyticsSignalKind.Ema, TimeFrameType.Daily, "daily-v1"),
                ContractId = contractId,
                ValueDate = valueDate,
                ObservationId = FuturesTradeSessionBarId.Create(series, TimeFrameType.Daily, dailyEnd, 2),
                MarketDataAsOfUtc = dailyEnd,
                CalculatedAtUtc = dailyEnd,
                SourceSequence = 2,
                SchemaVersion = 1,
                CalculationVersion = "daily-v1",
                IsValid = true
            };
            var ema = new FuturesEmaSignalReadModel
            {
                Metadata = metadata,
                Ema50 = 5300m,
                Ema200 = 5000m,
                IsWarm = true
            };
            var bb = new FuturesBbSignalReadModel
            {
                Metadata = metadata with
                {
                    SignalKey = metadata.SignalKey with
                    {
                        SignalKind = MarketAnalyticsSignalKind.BollingerBand
                    }
                },
                Ema20Center = 5400m,
                StandardDeviation20 = 25m,
                Upper20 = 5450m,
                Lower20 = 5350m,
                IsWarm = true
            };
            var observe = new ObserveMarketOutlookComponentCommand(
                entityId,
                observeId,
                1,
                observedAt,
                "integration-rsi",
                futuresRsiSignal: rsi,
                futuresItiSignal: iti,
                futuresEmaSignal: ema,
                futuresBbSignal: bb)
            {
                CommandId = observeId,
                Subject = CommandSubject(ObserveMarketOutlookComponentCommand.Verb, entityId)
            };
            var observeResult = await _producer.RequestAsync<
                ObserveMarketOutlookComponentCommand,
                MarketOutlookEntityId,
                GuidResult>(observe.Subject, observe, entityId);
            observeResult.Success.Should().BeTrue(observeResult.ErrorMessage);

            var eod = SampleData.FuturesEodData with
            {
                ContractId = contractId,
                ValueDate = valueDate,
                Symbol = "ES",
                OpenPrice = 5400m,
                HighPrice = 5500m,
                LowPrice = 5350m,
                ClosePrice = 5425m,
                DailyPercentChange = 0.0046,
                PriceDirection = PriceDirectionType.Rising
            };
            var publish = new PublishMarketOutlookSnapshotCommand(
                entityId,
                publishId,
                2,
                observedAt.AddMinutes(1),
                eod,
                futuresRsiSignal: rsi,
                futuresEmaSignal: ema,
                futuresBbSignal: bb)
            {
                CommandId = publishId,
                Subject = CommandSubject(PublishMarketOutlookSnapshotCommand.Verb, entityId)
            };
            var publishResult = await _producer.RequestAsync<
                PublishMarketOutlookSnapshotCommand,
                MarketOutlookEntityId,
                GuidResult>(publish.Subject, publish, entityId);
            publishResult.Success.Should().BeTrue(publishResult.ErrorMessage);

            var notification = await terminal.Task.WaitAsync(TimeSpan.FromSeconds(20));
            notification.CommandId.Should().Be(publishId);
            notification.EntityId.Should().Be(entityId);
            notification.MarketOutlook.Revision.Should().Be(2);
            notification.MarketOutlook.MissingInputs.Should().NotContain("TDI");
            notification.MarketOutlook.FuturesRsiSignal.Should().BeEquivalentTo(rsi);
            notification.MarketOutlook.LatestItiTrendSignal.Should().BeEquivalentTo(iti);
            notification.MarketOutlook.FuturesEmaSignal.Should().BeEquivalentTo(ema);
            notification.MarketOutlook.FuturesBbSignal.Should().BeEquivalentTo(bb);
            notification.MarketOutlook.FuturesEodData.Should().BeEquivalentTo(eod);

            var workingState = await dbFixture.MarketDataDb.GetMarketOutlookWorkingStateAsync(
                contractId,
                valueDate);
            var snapshot = await dbFixture.MarketDataDb.GetMarketOutlookSnapshotAsync(
                contractId,
                valueDate);
            workingState.Should().NotBeNull();
            workingState!.Revision.Should().Be(2);
            workingState.Status.Should().Be(MarketOutlookStateStatus.Published);
            workingState.FuturesRsiSignal.Should().BeEquivalentTo(rsi);
            workingState.LatestItiTrendSignal.Should().BeEquivalentTo(iti);
            workingState.FuturesEmaSignal.Should().BeEquivalentTo(ema);
            workingState.FuturesBbSignal.Should().BeEquivalentTo(bb);
            workingState.FuturesEodData.Should().BeEquivalentTo(eod);
            snapshot.Should().BeEquivalentTo(
                notification.MarketOutlook,
                options => options.Excluding(value => value.UpdatedOn));
            snapshot!.UpdatedOn.Should().BeCloseTo(
                notification.MarketOutlook.UpdatedOn,
                TimeSpan.FromMilliseconds(1));

            var queryApi = new MarketDataAnalyticsQueryApi(_producer);
            var queryResult = await queryApi.GetMarketOutlookSnapshotAsync(contractId, valueDate);
            queryResult.Success.Should().BeTrue(queryResult.ErrorMessage);
            queryResult.Value.Should().BeEquivalentTo(
                snapshot,
                options => options.Excluding(value => value.UpdatedOn));
            queryResult.Value!.UpdatedOn.Should().BeCloseTo(
                snapshot.UpdatedOn,
                TimeSpan.FromMilliseconds(1));
            queryResult.Value.FuturesEodData.OpenPrice.Should().Be(5400m);
            queryResult.Value.FuturesEodData.ClosePrice.Should().Be(5425m);
            queryResult.Value.FuturesEodData.DailyPercentChange.Should().Be(0.0046);
            queryResult.Value.FuturesEmaSignal.Should().BeEquivalentTo(ema);
            queryResult.Value.FuturesBbSignal.Should().BeEquivalentTo(bb);

            var streamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync(
                CommandSubject(ObserveMarketOutlookComponentCommand.Verb, entityId).ThreadId.ToString());
            streamId.Should().BeGreaterThan(0);
        }
        finally
        {
            await listener.StopAsync();
        }

        ValueTask OnNotificationAsync(string verb, NatsMsg<byte[]> message)
        {
            if (verb == MarketOutlookUpdatedNotifyEvent.Verb)
            {
                var notification = message.AsEvent<MarketOutlookUpdatedNotifyEvent>();
                if (notification is not null
                    && notification.EntityId == entityId
                    && notification.CommandId == publishId)
                    terminal.TrySetResult(notification);
            }
            return ValueTask.CompletedTask;
        }
    }

    static ActorSubject CommandSubject(string verb, MarketOutlookEntityId entityId)
        => new(
            ActorType.Command,
            ObserveMarketOutlookComponentCommand.Actor,
            verb,
            entityId.Format());
}
