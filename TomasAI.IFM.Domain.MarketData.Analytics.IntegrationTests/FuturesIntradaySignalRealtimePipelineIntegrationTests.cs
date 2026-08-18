using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests;

/// <summary>
/// Proves that all six intraday configurations traverse Core NATS realtime actors,
/// persist once to Scylla, and do not create durable Generate command streams.
/// </summary>
[Trait("Category", "Integration")]
public sealed class FuturesIntradaySignalRealtimePipelineIntegrationTests(
    WebApplicationFactory<Program> factory,
    MarketDataAnalyticsFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<MarketDataAnalyticsFixture>
{
    static readonly DateOnly ValueDate = new(2026, 8, 17);
    readonly IActorProducer _producer = factory.Services.GetRequiredService<IActorProducer>();

    [Fact]
    public async Task AllSixPeriods_ProjectRsiAtrAdxMacdAndTdiWithoutDurableGenerationStreams()
    {
        var contractId = $"ESRT{Guid.NewGuid():N}"[..18];
        var profile = FuturesIntradaySignalActivationProfile.Create(contractId, ValueDate);
        var timestamp = new DateTime(2026, 8, 17, 13, 30, 0, DateTimeKind.Utc);

        foreach (var activation in profile)
        {
            await PublishAsync(AtrSample(activation.Atr, timestamp));
            await PublishAsync(AdxSample(activation.Adx, timestamp));
            await PublishAsync(MacdSample(activation.Macd, timestamp));
        }

        // Forty-seven samples contain the 13-sample RSI warm-up plus the 34
        // valid RSI values required by the conventional TDI configuration.
        for (var sequence = 1; sequence <= 47; sequence++)
        {
            foreach (var activation in profile)
            {
                await PublishAsync(RsiSample(
                    activation.Rsi,
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
            rsi!.SourceSequence.Should().Be(47);

            (await dbFixture.MarketDataDb.GetLastFuturesAtrSignalAsync(
                contractId, ValueDate, activation.TimeFrame, activation.Atr.PeriodLength))
                .Should().NotBeNull();
            (await dbFixture.MarketDataDb.GetLastFuturesAdxSignalAsync(
                contractId, ValueDate, activation.TimeFrame, activation.Adx.PeriodLength))
                .Should().NotBeNull();
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

            await AssertNoDurableGenerateStreamAsync(
                GenerateFuturesRsiSignalCommand.Actor,
                GenerateFuturesRsiSignalCommand.Verb,
                activation.Rsi.Format());
            await AssertNoDurableGenerateStreamAsync(
                GenerateFuturesAtrSignalCommand.Actor,
                GenerateFuturesAtrSignalCommand.Verb,
                activation.Atr.Format());
            await AssertNoDurableGenerateStreamAsync(
                GenerateFuturesAdxSignalCommand.Actor,
                GenerateFuturesAdxSignalCommand.Verb,
                activation.Adx.Format());
            await AssertNoDurableGenerateStreamAsync(
                GenerateFuturesMacdSignalCommand.Actor,
                GenerateFuturesMacdSignalCommand.Verb,
                activation.Macd.Format());
            await AssertNoDurableGenerateStreamAsync(
                GenerateFuturesTdiSignalCommand.Actor,
                GenerateFuturesTdiSignalCommand.Verb,
                new FuturesTdiSignalEntityId(
                    contractId,
                    ValueDate,
                    activation.TimeFrame,
                    FuturesTdiConfiguration.StandardConfigurationId).Format());
        }
    }

    async Task WaitForStoredSignalsAsync(
        IReadOnlyList<FuturesIntradaySignalActivation> profile,
        string contractId,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var complete = true;
            foreach (var activation in profile)
            {
                var tdi = await dbFixture.MarketDataDb.GetLastFuturesTdiSignalAsync(
                    contractId,
                    ValueDate,
                    activation.TimeFrame,
                    FuturesTdiConfiguration.StandardConfigurationId);
                if (tdi is null)
                {
                    complete = false;
                    break;
                }
            }
            if (complete)
                return;
            await Task.Delay(250);
        }
        throw new TimeoutException("All six realtime TDI projections were not stored before the deadline.");
    }

    async ValueTask PublishAsync(FuturesRsiSignalSampledRealtimeEvent @event) =>
        await _producer.SendAsync<FuturesRsiSignalSampledRealtimeEvent, FuturesRsiSignalEntityId>(
            @event.Subject, @event);

    async ValueTask PublishAsync(FuturesAtrSignalSampledRealtimeEvent @event) =>
        await _producer.SendAsync<FuturesAtrSignalSampledRealtimeEvent, FuturesAtrSignalEntityId>(
            @event.Subject, @event);

    async ValueTask PublishAsync(FuturesAdxSignalSampledRealtimeEvent @event) =>
        await _producer.SendAsync<FuturesAdxSignalSampledRealtimeEvent, FuturesAdxSignalEntityId>(
            @event.Subject, @event);

    async ValueTask PublishAsync(FuturesMacdSignalSampledRealtimeEvent @event) =>
        await _producer.SendAsync<FuturesMacdSignalSampledRealtimeEvent, FuturesMacdSignalEntityId>(
            @event.Subject, @event);

    async Task AssertNoDurableGenerateStreamAsync(string actor, string verb, string entityId)
    {
        var subject = new ActorSubject(ActorType.Command, actor, verb, entityId);
        (await dbFixture.ActorEventSourceDb.GetEventStreamIdFromDbAsync($"{subject.ThreadId}"))
            .Should().BeNull();
    }

    static FuturesRsiSignalSampledRealtimeEvent RsiSample(
        FuturesRsiSignalEntityId entityId,
        long sequence,
        DateTime timestamp,
        decimal price) => new()
    {
        Subject = new(ActorType.Realtime, FuturesRsiSignalSampledRealtimeEvent.Actor,
            FuturesRsiSignalSampledRealtimeEvent.Verb, entityId.Format()),
        Id = Guid.NewGuid(), EntityId = entityId, CommandId = Guid.NewGuid(),
        AggregateId = entityId.Format(), EventSource = "integration-test", ReceivedOn = timestamp,
        FuturesPrice = price, SourceSequence = sequence, SourceEventTimestamp = timestamp
    };

    static FuturesAtrSignalSampledRealtimeEvent AtrSample(
        FuturesAtrSignalEntityId entityId,
        DateTime timestamp) => new()
    {
        Subject = new(ActorType.Realtime, FuturesAtrSignalSampledRealtimeEvent.Actor,
            FuturesAtrSignalSampledRealtimeEvent.Verb, entityId.Format()),
        Id = Guid.NewGuid(), EntityId = entityId, CommandId = Guid.NewGuid(),
        AggregateId = entityId.Format(), EventSource = "integration-test", ReceivedOn = timestamp,
        FuturesPrice = 5401m, SourceSequence = 1, SourceEventTimestamp = timestamp
    };

    static FuturesAdxSignalSampledRealtimeEvent AdxSample(
        FuturesAdxSignalEntityId entityId,
        DateTime timestamp) => new()
    {
        Subject = new(ActorType.Realtime, FuturesAdxSignalSampledRealtimeEvent.Actor,
            FuturesAdxSignalSampledRealtimeEvent.Verb, entityId.Format()),
        Id = Guid.NewGuid(), EntityId = entityId, CommandId = Guid.NewGuid(),
        AggregateId = entityId.Format(), EventSource = "integration-test", ReceivedOn = timestamp,
        FuturesPrice = 5401m, SourceSequence = 1, SourceEventTimestamp = timestamp
    };

    static FuturesMacdSignalSampledRealtimeEvent MacdSample(
        FuturesMacdSignalEntityId entityId,
        DateTime timestamp) => new()
    {
        Subject = new(ActorType.Realtime, FuturesMacdSignalSampledRealtimeEvent.Actor,
            FuturesMacdSignalSampledRealtimeEvent.Verb, entityId.Format()),
        Id = Guid.NewGuid(), EntityId = entityId, CommandId = Guid.NewGuid(),
        AggregateId = entityId.Format(), EventSource = "integration-test", ReceivedOn = timestamp,
        FuturesPrice = 5401m, SourceSequence = 1, SourceEventTimestamp = timestamp
    };
}
