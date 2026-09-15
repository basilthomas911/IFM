using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Event;
using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.HistoricalDataLoader;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.HistoricalDataLoader;

/// <summary>Verifies the requested Event handler's durable terminal outcomes.</summary>
public sealed class HistoricalDataLoaderEventHandlerTests
{
    /// <summary>A previously completed request publishes a correlated Completed event without reacquisition.</summary>
    [Fact]
    public async Task CompletedRequestHash_PublishesCompletedWithoutAcquiringAgain()
    {
        var request = Request(automatic: false);
        var (context, provider, store, logger) = Scenario();
        var estimate = new MarketDataHistoricalEstimate(
            request.EntityId.Value, 0m, 0, 0, "request-sha", DateTimeOffset.UtcNow);
        provider.EstimateAsync(Arg.Any<MarketDataHistoricalRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<MarketDataHistoricalEstimate>(estimate));
        store.GetCompletedByRequestHashAsync("request-sha", Arg.Any<CancellationToken>())
            .Returns(new ValueTask<HistoricalDataLoaderState?>(new HistoricalDataLoaderState
            {
                DataLoadAttemptId = request.EntityId.Value,
                RequestSha256 = "request-sha",
                Status = HistoricalDataLoaderStatus.Completed,
                Checkpoint = new HistoricalAcquisitionCheckpoint
                {
                    DataLoadAttemptId = request.EntityId.Value,
                    Stage = HistoricalAcquisitionStage.Completed
                },
                UpdatedAtUtc = DateTimeOffset.UtcNow
            }));

        await request.ExecuteAsync(context, logger);

        await context.Received(1).SendAsync<
            FuturesAnalyticsHistoricalDataLoaderCompletedEvent,
            FuturesAnalyticsHistoricalDataLoaderEntityId>(
            Arg.Is<FuturesAnalyticsHistoricalDataLoaderCompletedEvent>(terminal =>
                terminal.EntityId == request.EntityId
                && terminal.CommandId == request.CommandId
                && terminal.RequestSha256 == "request-sha"));
        await provider.DidNotReceiveWithAnyArgs().AcquireAsync(
            default!, default!, default!, default);
    }

    /// <summary>A provider failure publishes the correlated Failed event and keeps error detail.</summary>
    [Fact]
    public async Task ProviderEstimateFailure_PublishesFailedWithCause()
    {
        var request = Request(automatic: false);
        var (context, provider, store, logger) = Scenario();
        provider.EstimateAsync(Arg.Any<MarketDataHistoricalRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<MarketDataHistoricalEstimate>(
                Task.FromException<MarketDataHistoricalEstimate>(
                    new InvalidOperationException("provider unavailable"))));

        await request.ExecuteAsync(context, logger);

        await context.Received(1).SendAsync<
            FuturesAnalyticsHistoricalDataLoaderFailedEvent,
            FuturesAnalyticsHistoricalDataLoaderEntityId>(
            Arg.Is<FuturesAnalyticsHistoricalDataLoaderFailedEvent>(terminal =>
                terminal.EntityId == request.EntityId
                && terminal.CommandId == request.CommandId
                && terminal.ErrorMessage.Contains("provider unavailable", StringComparison.Ordinal)));
        await store.Received().SaveAsync(
            Arg.Is<HistoricalDataLoaderState>(state => state.Status == HistoricalDataLoaderStatus.Failed),
            Arg.Any<CancellationToken>());
    }

    /// <summary>An old automatic startup request does no provider or financial storage work.</summary>
    [Fact]
    public async Task StaleAutomaticRequest_IsIgnoredBeforeProviderOrStoreWork()
    {
        var request = Request(automatic: true) with { ReceivedOn = DateTime.UtcNow.AddMinutes(-6) };
        var (context, provider, store, logger) = Scenario();

        await request.ExecuteAsync(context, logger);

        await provider.DidNotReceiveWithAnyArgs().EstimateAsync(default!, default);
        await store.DidNotReceiveWithAnyArgs().SaveAsync(default!, default);
        await context.DidNotReceiveWithAnyArgs().SendAsync<
            FuturesAnalyticsHistoricalDataLoaderCompletedEvent,
            FuturesAnalyticsHistoricalDataLoaderEntityId>(default!);
        await context.DidNotReceiveWithAnyArgs().SendAsync<
            FuturesAnalyticsHistoricalDataLoaderFailedEvent,
            FuturesAnalyticsHistoricalDataLoaderEntityId>(default!);
    }

    static FuturesAnalyticsHistoricalDataLoaderRequestedEvent Request(bool automatic)
    {
        var id = new FuturesAnalyticsHistoricalDataLoaderEntityId(Guid.NewGuid());
        return new FuturesAnalyticsHistoricalDataLoaderRequestedEvent
        {
            Subject = new(ActorType.Event,
                FuturesAnalyticsHistoricalDataLoaderRequestedEvent.Actor,
                FuturesAnalyticsHistoricalDataLoaderRequestedEvent.Verb, id.Format()),
            Id = Guid.NewGuid(),
            CommandId = Guid.NewGuid(),
            EntityId = id,
            ReceivedOn = DateTime.UtcNow,
            Parameters = new FuturesAnalyticsHistoricalDataLoaderParameters
            {
                AutomaticStartupWarmup = automatic,
                StartDate = new(2026, 8, 17),
                EndDate = new(2026, 8, 17),
                RequestedBy = "unit-test"
            }
        };
    }

    static (IFuturesAnalyticsHistoricalDataLoaderEventContext Context,
        IMarketDataHistoricalApi Provider, IHistoricalDataLoaderStore Store,
        ILogger<FuturesAnalyticsHistoricalDataLoaderEventActor> Logger) Scenario()
    {
        var provider = Substitute.For<IMarketDataHistoricalApi>();
        var store = Substitute.For<IHistoricalDataLoaderStore>();
        var observationStore = Substitute.For<IHistoricalObservationStore>();
        var replayPublisher = Substitute.For<IHistoricalReplayPublisher>();
        var calendar = Substitute.For<IMarketSessionCalendar>();
        var logger = Substitute.For<ILogger<FuturesAnalyticsHistoricalDataLoaderEventActor>>();
        var loader = new TomasAI.IFM.Application.MarketData.Historical.HistoricalDataLoader(
            provider, store, observationStore, replayPublisher, calendar, TimeProvider.System);
        var context = Substitute.For<IFuturesAnalyticsHistoricalDataLoaderEventContext>();
        context.DataLoader.Returns(loader);
        context.DataLoaderStore.Returns(store);
        context.Logger.Returns(logger);
        store.GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<HistoricalDataLoaderState?>((HistoricalDataLoaderState?)null));
        return (context, provider, store, logger);
    }
}
