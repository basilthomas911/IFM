using System.Collections.Concurrent;
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
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests.FuturesItiSignal;

/// <summary>
/// Verifies the durable ITI strategy-level lifecycle across actors, messages, and Scylla projections.
/// </summary>
[Trait("Category", "Integration")]
[Collection(ItiPipelineIntegrationCollection.Name)]
public sealed class FuturesItiSignalLevelCycleIntegrationTests(
    WebApplicationFactory<Program> factory,
    MarketDataAnalyticsFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>,
      IClassFixture<MarketDataAnalyticsFixture>
{
    const string ContractId = "ES-ITI-LEVEL-CYCLE";
    const double StartPrice = 5_000;
    const double VixPrice = 20;
    static readonly DateOnly ValueDate = new(2026, 8, 18);
    static readonly DateTime FirstTimestamp = new(2026, 8, 18, 14, 30, 0, DateTimeKind.Utc);
    static readonly double[] ValidationLevels =
    [
        0.11, 0.22, 0.33, 0.44, 0.55,
        0.66, 0.77, 0.88, 0.99, 1.10,
        1.30
    ];

    readonly IActorProducer _actorProducer =
        factory.Services.GetRequiredService<IActorProducer>();

    /// <summary>
    /// Runs an up/down/up trend cycle through every ten-percent threshold level and beyond one full threshold.
    /// </summary>
    [Fact]
    public async Task DurablePipeline_UpDownUpCycle_PreservesEveryCalculatedStrategyLevel()
    {
        var entityId = new FuturesItiSignalEntityId(
            ContractId,
            ValueDate,
            TimeFrameType.Daily);
        await ResetAsync(entityId);

        var generated = new ConcurrentQueue<FuturesItiSignalGeneratedEvent>();
        TaskCompletionSource<FuturesItiSignalGeneratedCompleteEvent>? pending = null;
        var timestamp = FirstTimestamp.AddSeconds(-1);
        var listener = new NatsActorEventListener(
            new NatsEventListenerOptions(),
            Substitute.For<ILogger<NatsActorEventListener>>());

        await listener.StartAsync(
            $"iti-level-cycle-{Guid.NewGuid():N}",
            new Dictionary<ActorMailboxId, List<string>>
            {
                [new ActorMailboxId(ActorType.Event, FuturesItiSignalGeneratedEvent.Actor)] =
                [
                    FuturesItiSignalGeneratedEvent.Verb,
                    FuturesItiSignalGeneratedCompleteEvent.Verb,
                    FuturesItiSignalGeneratedFailEvent.Verb
                ]
            },
            HandleEventAsync);

        try
        {
            var api = new MarketDataAnalyticsCommandApi(_actorProducer);

            async Task<FuturesItiSignalV2ReadModel> GenerateAsync(double price)
            {
                pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
                var response = await api.GenerateFuturesItiSignalAsync(
                    ContractId,
                    ValueDate,
                    TimeFrameType.Daily,
                    timestamp = timestamp.AddSeconds(1),
                    price,
                    VixPrice);

                response.Success.Should().BeTrue();
                var completed = await pending.Task.WaitAsync(TimeSpan.FromSeconds(20));
                completed.FuturesItiSignal.Should().NotBeNull();
                var signal = completed.FuturesItiSignal!;
                AssertCalculatedLevels(signal);
                return signal;
            }

            // Start the first uptrend and publish every ten-percent validation rung.
            var upStart = await GenerateAsync(StartPrice);
            AssertDirectionChange(upStart, IntrinsicTimeTrendType.UpTrend, expectedGroup: 0);
            var upExtremes = new List<FuturesItiSignalV2ReadModel>();
            foreach (var targetLevel in ValidationLevels)
            {
                var signal = await GenerateAsync(
                    upStart.TrendPrice + upStart.Threshold * targetLevel);
                signal.IntrinsicTimeMode.Should().Be(IntrinsicTimeModeType.TrendExtremeChanged);
                signal.IntrinsicTimeTrend.Should().Be(IntrinsicTimeTrendType.UpTrend);
                signal.ReversalLevel.Should().Be(0);
                upExtremes.Add(signal);
            }
            AssertValidationRungs(upExtremes);

            var upExtended = upExtremes[^1];
            upExtended.BandLevel.Should().BeGreaterThan(1);
            var upReversal = await GenerateAsync(
                upExtended.TrendReversal - upExtended.BandSize * 1.01);
            upReversal.IntrinsicTimeMode.Should().Be(IntrinsicTimeModeType.TrendReversalChanged);
            upReversal.ReversalLevel.Should().BeGreaterThan(0);
            var upTrending = await GenerateAsync(
                upReversal.BandAnchorPrice + upReversal.BandSize * 1.01);
            upTrending.IntrinsicTimeMode.Should().Be(IntrinsicTimeModeType.Trending);
            upTrending.ReversalLevel.Should().BeLessThan(upReversal.ReversalLevel);

            // Cross the trigger, start the downtrend, and repeat every validation rung.
            var downStart = await GenerateAsync(upTrending.DownTrendTrigger);
            AssertDirectionChange(downStart, IntrinsicTimeTrendType.DownTrend, expectedGroup: 1);
            var downExtremes = new List<FuturesItiSignalV2ReadModel>();
            foreach (var targetLevel in ValidationLevels)
            {
                var signal = await GenerateAsync(
                    downStart.TrendPrice - downStart.Threshold * targetLevel);
                signal.IntrinsicTimeMode.Should().Be(IntrinsicTimeModeType.TrendExtremeChanged);
                signal.IntrinsicTimeTrend.Should().Be(IntrinsicTimeTrendType.DownTrend);
                signal.ReversalLevel.Should().Be(0);
                downExtremes.Add(signal);
            }
            AssertValidationRungs(downExtremes);

            var downExtended = downExtremes[^1];
            downExtended.BandLevel.Should().BeGreaterThan(1);
            var downReversal = await GenerateAsync(
                downExtended.TrendReversal + downExtended.BandSize * 1.01);
            downReversal.IntrinsicTimeMode.Should().Be(IntrinsicTimeModeType.TrendReversalChanged);
            downReversal.ReversalLevel.Should().BeGreaterThan(0);
            var downTrending = await GenerateAsync(
                downReversal.BandAnchorPrice - downReversal.BandSize * 1.01);
            downTrending.IntrinsicTimeMode.Should().Be(IntrinsicTimeModeType.Trending);
            downTrending.ReversalLevel.Should().BeLessThan(downReversal.ReversalLevel);

            var secondUpStart = await GenerateAsync(downTrending.UpTrendTrigger);
            AssertDirectionChange(secondUpStart, IntrinsicTimeTrendType.UpTrend, expectedGroup: 2);

            var completedSignals = generated
                .Select(@event => @event.FuturesItiSignal!)
                .OrderBy(signal => signal.IntrinsicTime)
                .ToArray();
            completedSignals.Should().HaveCount(29);
            foreach (var signal in completedSignals)
                AssertCalculatedLevels(signal);

            var stored = (await dbFixture.MarketDataDb.GetFuturesItiSignalsAsync(entityId))
                .OrderBy(signal => signal.IntrinsicTime)
                .ToArray();
            stored.Should().HaveCount(completedSignals.Length);
            stored.Select(ToLevelSnapshot).Should().Equal(completedSignals.Select(ToLevelSnapshot));

            var activeState = await dbFixture.MarketDataDb.GetFuturesItiTimeFrameStateAsync(
                ContractId,
                TimeFrameType.Daily,
                ValueDate);
            activeState.Should().NotBeNull();
            ToLevelSnapshot(activeState!).Should().Be(ToLevelSnapshot(secondUpStart));
        }
        finally
        {
            await listener.StopAsync();
            await ResetAsync(entityId);
        }

        ValueTask HandleEventAsync(string eventVerb, NatsMsg<byte[]> message)
        {
            if (eventVerb == FuturesItiSignalGeneratedEvent.Verb)
            {
                var @event = message.AsEvent<FuturesItiSignalGeneratedEvent>()!;
                if (Matches(@event.EntityId))
                    generated.Enqueue(@event);
            }
            else if (eventVerb == FuturesItiSignalGeneratedCompleteEvent.Verb)
            {
                var @event = message.AsEvent<FuturesItiSignalGeneratedCompleteEvent>()!;
                if (Matches(@event.EntityId))
                    pending?.TrySetResult(@event);
            }
            else if (eventVerb == FuturesItiSignalGeneratedFailEvent.Verb)
            {
                var @event = message.AsEvent<FuturesItiSignalGeneratedFailEvent>()!;
                if (Matches(@event.EntityId))
                    pending?.TrySetException(new InvalidOperationException(@event.ErrorMessage));
            }

            return ValueTask.CompletedTask;
        }

        bool Matches(FuturesItiSignalEntityId id) =>
            StringComparer.Ordinal.Equals(id.ContractId, ContractId)
            && id.ValueDate == ValueDate
            && id.TimePeriod == TimeFrameType.Daily;
    }

    async Task ResetAsync(FuturesItiSignalEntityId entityId)
    {
        var subject = new ActorSubject(
            ActorType.Command,
            GenerateFuturesItiSignalCommand.Actor,
            GenerateFuturesItiSignalCommand.Verb,
            entityId.Format());
        var streamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{subject.ThreadId}");
        if (streamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(streamId);
        await dbFixture.MarketDataDb.DeleteFuturesItiSignalAsync(
            entityId.ContractId,
            entityId.ValueDate,
            entityId.TimePeriod);
    }

    static void AssertDirectionChange(
        FuturesItiSignalV2ReadModel signal,
        IntrinsicTimeTrendType expectedTrend,
        int expectedGroup)
    {
        signal.IntrinsicTimeMode.Should().Be(IntrinsicTimeModeType.TrendDirectionChanged);
        signal.IntrinsicTimeTrend.Should().Be(expectedTrend);
        signal.IntrinsicTimeGroupId.Should().Be(expectedGroup);
        signal.BandLevel.Should().Be(0);
        signal.ReversalLevel.Should().Be(0);
    }

    static void AssertValidationRungs(IReadOnlyList<FuturesItiSignalV2ReadModel> signals)
    {
        signals.Should().HaveCount(ValidationLevels.Length);
        signals.Select(signal => signal.BandLevel).Should().BeInAscendingOrder();
        for (var index = 0; index < 10; index++)
        {
            var lowerBound = (index + 1) / 10.0;
            signals[index].BandLevel.Should().BeGreaterThanOrEqualTo(lowerBound);
            signals[index].BandLevel.Should().BeLessThan(lowerBound + 0.15);
        }
    }

    static void AssertCalculatedLevels(FuturesItiSignalV2ReadModel signal)
    {
        var directionalMovement = signal.IntrinsicTimeTrend == IntrinsicTimeTrendType.UpTrend
            ? signal.IntrinsicPrice - signal.TrendPrice
            : signal.TrendPrice - signal.IntrinsicPrice;
        var expectedBandLevel = signal.Threshold <= 0
            ? 0
            : directionalMovement / signal.Threshold;
        signal.BandLevel.Should().BeApproximately(expectedBandLevel, 1e-10);

        var trendExcursion = signal.IntrinsicTimeTrend == IntrinsicTimeTrendType.UpTrend
            ? signal.TrendExtreme - signal.TrendPrice
            : signal.TrendPrice - signal.TrendExtreme;
        var retracement = signal.IntrinsicTimeTrend == IntrinsicTimeTrendType.UpTrend
            ? signal.TrendExtreme - signal.IntrinsicPrice
            : signal.IntrinsicPrice - signal.TrendExtreme;
        var expectedReversalLevel = trendExcursion <= 0
            ? 0
            : Math.Max(0, retracement / trendExcursion);
        signal.ReversalLevel.Should().BeApproximately(expectedReversalLevel, 1e-10);
    }

    static object ToLevelSnapshot(FuturesItiSignalV2ReadModel signal) => new
    {
        signal.IntrinsicTime,
        signal.IntrinsicPrice,
        signal.IntrinsicTimeGroupId,
        signal.IntrinsicTimeTrend,
        signal.IntrinsicTimeMode,
        signal.TrendPrice,
        signal.TrendExtreme,
        signal.Threshold,
        signal.BandLevel,
        signal.ReversalLevel
    };
}
