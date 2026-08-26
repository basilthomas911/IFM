using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Observation;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;

namespace TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests;

/// <summary>
/// Proves that all six intraday configurations traverse Core NATS realtime actors,
/// persist once to Scylla, and route each calculation through its durable command stream.
/// </summary>
[Trait("Category", "Integration")]
public sealed class FuturesIntradaySignalRealtimePipelineIntegrationTests(
    WebApplicationFactory<Program> factory,
    MarketDataAnalyticsFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<MarketDataAnalyticsFixture>
{
    static readonly DateOnly ValueDate = new(2026, 8, 17);
    readonly IActorProducer _producer = factory.Services.GetRequiredService<IActorProducer>();
    readonly IMarketDataAnalyticsCommandApi _commandApi = new MarketDataAnalyticsCommandApi(
        factory.Services.GetRequiredService<IActorProducer>());

    [Fact]
    public void IntradayRealtimeActors_RegisterClosedObservationRoute()
    {
        var routes = factory.Services.GetRequiredService<IActorSupervisor>()
            .GetRealtimeRoutes(new ActorTypeId(ActorType.Realtime,
                FuturesTradeSessionBarClosedRealtimeEvent.Actor,
                FuturesTradeSessionBarClosedRealtimeEvent.Verb));
        routes.Should().Contain([
            new ActorMailboxId(ActorType.Realtime, FuturesRsiSignalRealtimeActor.ActorName),
            new ActorMailboxId(ActorType.Realtime, FuturesAtrSignalRealtimeActor.ActorName),
            new ActorMailboxId(ActorType.Realtime, FuturesAdxSignalRealtimeActor.ActorName),
            new ActorMailboxId(ActorType.Realtime, FuturesMacdSignalRealtimeActor.ActorName)
        ]);
    }

    [Fact]
    public async Task AdxStorage_AcceptsObservationLineage()
    {
        var contractId = $"ESMD{Guid.NewGuid():N}"[..18];
        var observation = ClosedObservation(contractId, TimeFrameType.OneMinute, 1,
            DateTime.UtcNow, 5401m).Observation;
        var signal = new FuturesAdxSignalReadModel(
            contractId,
            ValueDate,
            TimeFrameType.OneMinute,
            14,
            TimeOnly.FromDateTime(observation.LastMarketEventUtc.UtcDateTime),
            observation.Close,
            1d,
            1d,
            1d,
            FuturesTrendDirectionType.Init,
            FuturesTrendDirectionStrengthType.Low)
        {
            Metadata = new()
            {
                SignalKey = new(
                    observation.MarketSeriesIdentity,
                    MarketAnalyticsSignalKind.Adx,
                    observation.TimeFrame,
                    "adx-legacy-compatible-v1"),
                ContractId = observation.ContractId,
                ValueDate = observation.ValueDate,
                ObservationId = observation.ObservationId,
                MarketDataAsOfUtc = observation.LastMarketEventUtc,
                CalculatedAtUtc = DateTimeOffset.UtcNow,
                SourceSequence = observation.LastSourceSequence,
                SchemaVersion = 1,
                CalculationVersion = "adx-legacy-compatible-v1",
                CalculationMethod = observation.CalculationMethod,
                IsValid = true
            }
        };

        await dbFixture.MarketDataDb.InsertFuturesAdxSignalAsync(signal);

        (await dbFixture.MarketDataDb.GetLastFuturesAdxSignalAsync(
            contractId,
            ValueDate,
            TimeFrameType.OneMinute,
            14))
            .Should().NotBeNull();
    }

    [Fact]
    public async Task AllSixPeriods_ProjectRsiAtrAdxMacdAndTdiWithDurableGenerationStreams()
    {
        var contractId = $"ESRT{Guid.NewGuid():N}"[..18];
        var profile = FuturesIntradaySignalActivationProfile.Create(contractId, ValueDate);
        var timestamp = new DateTime(2026, 8, 17, 13, 30, 0, DateTimeKind.Utc);

        ClearAttachments();
        foreach (var activation in profile)
        {
            (await _commandApi.StartFuturesRsiSignalAsync(activation.Rsi)).Success.Should().BeTrue();
            (await _commandApi.StartFuturesAtrSignalAsync(activation.Atr)).Success.Should().BeTrue();
            (await _commandApi.StartFuturesAdxSignalAsync(activation.Adx)).Success.Should().BeTrue();
            (await _commandApi.StartFuturesMacdSignalAsync(activation.Macd)).Success.Should().BeTrue();
            FuturesTradeSessionBarAttachmentRegistry<FuturesRsiSignalEntityId>.Attach(activation.Rsi);
            FuturesTradeSessionBarAttachmentRegistry<FuturesAtrSignalEntityId>.Attach(activation.Atr);
            FuturesTradeSessionBarAttachmentRegistry<FuturesAdxSignalEntityId>.Attach(activation.Adx);
            FuturesTradeSessionBarAttachmentRegistry<FuturesMacdSignalEntityId>.Attach(activation.Macd);
        }
        try
        {
            // Sixty observations exceed the 13-sample RSI warm-up and retain the
            // 34 valid RSI values required by the conventional TDI configuration.
            for (var sequence = 1; sequence <= 60; sequence++)
            {
                foreach (var activation in profile)
                {
                    await PublishAsync(ClosedObservation(
                        contractId,
                        activation.TimeFrame,
                        sequence,
                        timestamp.AddSeconds(sequence),
                        5400m + sequence % 7 - sequence % 3));
                }
            }

            await WaitForStoredSignalsAsync(profile, contractId, TimeSpan.FromSeconds(60));

            foreach (var activation in profile)
            {
                var rsi = await dbFixture.MarketDataDb.GetLastFuturesRsiSignalAsync(
                    contractId, ValueDate, activation.TimeFrame, activation.Rsi.PeriodLength);
                rsi.Should().NotBeNull();
                rsi!.SourceSequence.Should().Be(60);

                (await dbFixture.MarketDataDb.GetLastFuturesAtrSignalAsync(
                    contractId, ValueDate, activation.TimeFrame, activation.Atr.PeriodLength))
                    .Should().NotBeNull();
                var adx = await dbFixture.MarketDataDb.GetLastFuturesAdxSignalAsync(
                    contractId, ValueDate, activation.TimeFrame, activation.Adx.PeriodLength);
                adx.Should().NotBeNull();
                adx!.Metadata.Should().NotBeNull();
                var macd = await dbFixture.MarketDataDb.GetLastFuturesMacdSignalAsync(
                    contractId,
                    ValueDate,
                    activation.TimeFrame,
                    activation.Macd.SignalEmaPeriod,
                    activation.Macd.FastEmaPeriod,
                    activation.Macd.SlowEmaPeriod);
                macd.Should().NotBeNull();
                macd!.SignalEmaPeriod.Should().Be(FuturesMacdConfiguration.ConventionalSignalEmaPeriod);
                macd.FastEmaPeriod.Should().Be(FuturesMacdConfiguration.ConventionalFastEmaPeriod);
                macd.SlowEmaPeriod.Should().Be(FuturesMacdConfiguration.ConventionalSlowEmaPeriod);

                var tdi = await dbFixture.MarketDataDb.GetLastFuturesTdiSignalAsync(
                    contractId,
                    ValueDate,
                    activation.TimeFrame,
                    FuturesTdiConfiguration.StandardConfigurationId);
                tdi.Should().NotBeNull();
                tdi!.SchemaVersion.Should().Be(FuturesTdiConfiguration.CurrentSchemaVersion);

                await AssertDurableGenerateStreamAsync(
                    GenerateFuturesRsiSignalCommand.Actor,
                    GenerateFuturesRsiSignalCommand.Verb,
                    activation.Rsi.Format());
                await AssertDurableGenerateStreamAsync(
                    GenerateFuturesAtrSignalCommand.Actor,
                    GenerateFuturesAtrSignalCommand.Verb,
                    activation.Atr.Format());
                await AssertDurableGenerateStreamAsync(
                    GenerateFuturesAdxSignalCommand.Actor,
                    GenerateFuturesAdxSignalCommand.Verb,
                    activation.Adx.Format());
                await AssertDurableGenerateStreamAsync(
                    GenerateFuturesMacdSignalCommand.Actor,
                    GenerateFuturesMacdSignalCommand.Verb,
                    activation.Macd.Format());
                await AssertDurableGenerateStreamAsync(
                    GenerateFuturesTdiSignalCommand.Actor,
                    GenerateFuturesTdiSignalCommand.Verb,
                    new FuturesTdiSignalEntityId(
                        contractId,
                        ValueDate,
                        activation.TimeFrame,
                        FuturesTdiConfiguration.StandardConfigurationId).Format());
            }
        }
        finally
        {
            ClearAttachments();
        }
    }

    async Task WaitForStoredSignalsAsync(
        IReadOnlyList<FuturesIntradaySignalActivation> profile,
        string contractId,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        var missing = new List<string>();
        while (DateTime.UtcNow < deadline)
        {
            var complete = true;
            missing.Clear();
            foreach (var activation in profile)
            {
                var rsi = await dbFixture.MarketDataDb.GetLastFuturesRsiSignalAsync(
                    contractId, ValueDate, activation.TimeFrame, activation.Rsi.PeriodLength);
                if (rsi is null || rsi.SourceSequence != 60)
                {
                    complete = false;
                    missing.Add($"RSI:{activation.TimeFrame}");
                }
                if (await dbFixture.MarketDataDb.GetLastFuturesAtrSignalAsync(
                        contractId, ValueDate, activation.TimeFrame, activation.Atr.PeriodLength) is null)
                {
                    complete = false;
                    missing.Add($"ATR:{activation.TimeFrame}");
                }
                var tdi = await dbFixture.MarketDataDb.GetLastFuturesTdiSignalAsync(
                    contractId,
                    ValueDate,
                    activation.TimeFrame,
                    FuturesTdiConfiguration.StandardConfigurationId);
                if (tdi is null)
                {
                    complete = false;
                    missing.Add($"TDI:{activation.TimeFrame}");
                }
                var adx = await dbFixture.MarketDataDb.GetLastFuturesAdxSignalAsync(
                    contractId,
                    ValueDate,
                    activation.TimeFrame,
                    activation.Adx.PeriodLength);
                if (adx is null)
                {
                    complete = false;
                    missing.Add($"ADX:{activation.TimeFrame}");
                }
                if (await dbFixture.MarketDataDb.GetLastFuturesMacdSignalAsync(
                        contractId, ValueDate, activation.TimeFrame,
                        activation.Macd.SignalEmaPeriod, activation.Macd.FastEmaPeriod,
                        activation.Macd.SlowEmaPeriod) is null)
                {
                    complete = false;
                    missing.Add($"MACD:{activation.TimeFrame}");
                }
            }
            if (complete)
                return;
            await Task.Delay(250);
        }
        var adxDiagnostics = new List<string>();
        foreach (var activation in profile)
        {
            var subject = new ActorSubject(
                ActorType.Command,
                GenerateFuturesAdxSignalCommand.Actor,
                GenerateFuturesAdxSignalCommand.Verb,
                activation.Adx.Format());
            var streamId = await dbFixture.ActorEventSourceDb
                .GetEventStreamIdFromDbAsync($"{subject.ThreadId}");
            adxDiagnostics.Add($"{activation.TimeFrame}:stream={streamId is not null}");
        }
        var rsiEventTypes = new List<string>();
        var rsiSubject = new ActorSubject(ActorType.Command,
            GenerateFuturesRsiSignalCommand.Actor,
            GenerateFuturesRsiSignalCommand.Verb,
            profile[0].Rsi.Format());
        var rsiStream = await dbFixture.ActorEventSourceDb
            .GetEventStreamIdFromDbAsync($"{rsiSubject.ThreadId}");
        if (rsiStream is not null)
            await dbFixture.ActorEventSourceDb.MapReduceActorEventStreamAsync<FuturesRsiSignalCommandState>(
                rsiStream.EventStreamId,
                rows => rsiEventTypes.AddRange(rows.Select(row =>
                    Type.GetType(row.EventTypeName, false)?.Name ?? row.EventTypeName)));
        var tdiRoutes = factory.Services.GetRequiredService<IActorSupervisor>()
            .GetEventRoutes(new ActorTypeId(ActorType.Event,
                FuturesRsiSignalsGeneratedEvent.Actor,
                FuturesRsiSignalsGeneratedEvent.Verb));
        var failedProjection = (await dbFixture.ActorEventSourceDb
                .GetEventProjectorOperationalStatePageAsync(
                    "FuturesAdxSignalEventProjector",
                    EventProjectorOperationalStatus.Failed,
                    0,
                    256))
            .OrderByDescending(state => state.EventId)
            .FirstOrDefault();
        var pendingProjection = (await dbFixture.ActorEventSourceDb
                .GetEventProjectorOperationalStatePageAsync(
                    "FuturesAdxSignalEventProjector",
                    EventProjectorOperationalStatus.Pending,
                    0,
                    256))
            .OrderByDescending(state => state.EventId)
            .FirstOrDefault();
        var blockedProjection = (await dbFixture.ActorEventSourceDb
                .GetEventProjectorOperationalStatePageAsync(
                    "FuturesAdxSignalEventProjector",
                    EventProjectorOperationalStatus.Blocked,
                    0,
                    256))
            .OrderByDescending(state => state.EventId)
            .FirstOrDefault();
        throw new TimeoutException(
            $"Realtime projections were not stored before the deadline for {contractId}: {string.Join(", ", missing)}; "
            + $"ADX diagnostics: {string.Join(", ", adxDiagnostics)}; "
            + $"RSI events: {string.Join(", ", rsiEventTypes.GroupBy(x => x).Select(x => $"{x.Key}={x.Count()}"))}; "
            + $"TDI routes: {string.Join(", ", tdiRoutes)}; "
            + $"latest projection failure: {failedProjection?.ErrorMessage ?? "none"}; "
            + $"latest pending projection: {pendingProjection?.EventId.ToString() ?? "none"}; "
            + $"latest blocked projection: {blockedProjection?.EventId.ToString() ?? "none"}/"
            + $"{blockedProjection?.BlockedReason ?? "none"}/"
            + $"{blockedProjection?.ErrorMessage ?? "none"}.");
    }

    async ValueTask PublishAsync(FuturesTradeSessionBarClosedRealtimeEvent @event) =>
        await _producer.SendAsync<FuturesTradeSessionBarClosedRealtimeEvent, FuturesTradeSessionBarEntityId>(
            @event.Subject, @event);

    async Task AssertDurableGenerateStreamAsync(string actor, string verb, string entityId)
    {
        var subject = new ActorSubject(ActorType.Command, actor, verb, entityId);
        (await dbFixture.ActorEventSourceDb.GetEventStreamIdFromDbAsync($"{subject.ThreadId}"))
            .Should().NotBeNull();
    }

    static FuturesTradeSessionBarClosedRealtimeEvent ClosedObservation(
        string contractId,
        TimeFrameType timeFrame,
        long sequence,
        DateTime timestamp,
        decimal price)
    {
        var series = MarketSeriesIdentity.ForContract(contractId);
        var end = new DateTimeOffset(timestamp, TimeSpan.Zero);
        var observationEntityId = new FuturesTradeSessionBarEntityId(series, timeFrame);
        var observation = new FuturesTradeSessionBarReadModel
        {
            MarketSeriesIdentity = series,
            ObservationId = FuturesTradeSessionBarId.Create(series, timeFrame, end, sequence),
            ContractId = contractId,
            ValueDate = ValueDate,
            TimeFrame = timeFrame,
            IntervalStartUtc = end.AddMinutes(-1),
            IntervalEndUtc = end,
            Open = price - 1m,
            High = price + 1m,
            Low = price - 2m,
            Close = price,
            Volume = 10m,
            TradeCount = 2,
            PriceVolumeSum = price * 10m,
            FirstSourceSequence = sequence,
            LastSourceSequence = sequence,
            FirstMarketEventUtc = end.AddSeconds(-30),
            LastMarketEventUtc = end,
            CalculatedAtUtc = end,
            CalculationVersion = "integration-test-v1",
            IsComplete = true,
            IsValid = true,
            CalculationMethod = MarketSignalCalculationMethod.ClosedObservation
        };
        return new()
        {
            Subject = new ActorSubject(
                ActorType.Realtime,
                FuturesTradeSessionBarClosedRealtimeEvent.Actor,
                FuturesTradeSessionBarClosedRealtimeEvent.Verb,
                observationEntityId.Format()),
            Id = Guid.NewGuid(),
            EntityId = observationEntityId,
            CommandId = Guid.NewGuid(),
            AggregateId = observationEntityId.Format(),
            EventSource = "integration-test",
            ReceivedOn = timestamp,
            Observation = observation
        };
    }

    static void ClearAttachments()
    {
        FuturesTradeSessionBarAttachmentRegistry<FuturesRsiSignalEntityId>.Clear();
        FuturesTradeSessionBarAttachmentRegistry<FuturesAtrSignalEntityId>.Clear();
        FuturesTradeSessionBarAttachmentRegistry<FuturesAdxSignalEntityId>.Clear();
        FuturesTradeSessionBarAttachmentRegistry<FuturesMacdSignalEntityId>.Clear();
    }
}
