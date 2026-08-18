using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests.TickAggregation;

/// <summary>
/// Proves the hosted normalized-trade and quote-midpoint to realtime VX EOD projection boundary with real actors and transports.
/// Only the external market feed is deterministic.
/// </summary>
public sealed class FuturesTickTradeToEodRealtimeIntegrationTests(
    WebApplicationFactory<Program> factory,
    MarketDataFeedFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<MarketDataFeedFixture>
{
    const string ContractId = "VX20261216";
    static readonly DateOnly MaturityDate = new(2026, 12, 16);

    readonly WebApplicationFactory<Program> _factory = factory;
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();
    readonly ILogger<NatsActorEventListener> _logger =
        Substitute.For<ILogger<NatsActorEventListener>>();

    [Fact]
    public async Task NormalizedVxTradeAndQuote_RouteThroughRealtimeActors_AndPersistEod()
    {
        var valueDate = DateOnly.FromDateTime(DateTime.UtcNow)
            .AddDays(-Random.Shared.Next(10_001, 20_000));
        var contract = new FuturesContractV2ReadModel(
            ContractId,
            "VX Futures Dec 2026",
            "VX",
            "VXZ6",
            "FUT",
            "USD",
            "CFE",
            "1000",
            MaturityDate,
            true);
        var entityId = new TickDataEntityId(
            ContractId,
            valueDate,
            AssetTypeId.Futures);
        var tickDataId = new TickDataId(
            ContractId,
            valueDate,
            91,
            DateTime.UtcNow);
        const decimal tradePrice = 18.75m;
        const uint tradeSize = 37;
        var sourceCommandId = Guid.NewGuid();
        var timestampNanoseconds =
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000_000;
        var normalizedTrade = new FuturesTickTradeDataChangedEvent
        {
            Subject = new ActorSubject(
                ActorType.Realtime,
                FuturesTickTradeDataChangedEvent.Actor,
                FuturesTickTradeDataChangedEvent.Verb,
                entityId.Format()),
            Id = Guid.NewGuid(),
            EntityId = entityId,
            CommandId = sourceCommandId,
            AggregateId = entityId.Format(),
            EventSource = nameof(FuturesTickTradeToEodRealtimeIntegrationTests),
            ReceivedOn = DateTime.UtcNow,
            SchemaVersion = 1,
            TickDataId = tickDataId,
            AssetTypeId = AssetTypeId.Futures,
            Dataset = "GLBX.MDP3",
            DefinitionDate = valueDate,
            PublisherId = 1,
            InstrumentId = 2,
            TradeData = new FuturesTickTradeData(
                91,
                timestampNanoseconds,
                timestampNanoseconds,
                0,
                decimal.ToInt64(tradePrice * 1_000_000_000m),
                tradePrice,
                tradeSize,
                1,
                2,
                0)
        };

        var startTerminal = new TaskCompletionSource<IEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var persistedTradeReceived = new TaskCompletionSource<FuturesTickTradeDataInsertedEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var eodTerminal = new TaskCompletionSource<IEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var quoteEodTerminal = new TaskCompletionSource<IEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        VixFuturesEodDataInsertedEvent? inserted = null;
        VixFuturesEodDataInsertedEvent? quoteInserted = null;
        var listener = new NatsActorEventListener(
            new NatsEventListenerOptions(),
            _logger);
        var realtimeListener = new NatsActorEventListener(
            new NatsEventListenerOptions(),
            _logger);
        var marketDataApi = _factory.Services.GetRequiredService<DatabentoMarketDataApi>();

        await listener.StartAsync(
            $"{nameof(NormalizedVxTradeAndQuote_RouteThroughRealtimeActors_AndPersistEod)}-event-{Guid.NewGuid():N}",
            new()
            {
                [new ActorMailboxId(
                    ActorType.Event,
                    FuturesTickDataStreamingStartedEvent.Actor)] =
                [
                    FuturesTickDataStreamingStartedCompleteEvent.Verb,
                    FuturesTickDataStreamingStartedFailEvent.Verb
                ]
            },
            EventHandlerAsync);
        await realtimeListener.StartAsync(
            $"{nameof(NormalizedVxTradeAndQuote_RouteThroughRealtimeActors_AndPersistEod)}-realtime-{Guid.NewGuid():N}",
            new()
            {
                [new ActorMailboxId(
                    ActorType.Realtime,
                    VixFuturesEodDataInsertedEvent.Actor)] =
                [
                    VixFuturesEodDataInsertedEvent.Verb,
                    VixFuturesEodDataInsertedCompleteEvent.Verb,
                    VixFuturesEodDataInsertedFailEvent.Verb
                ],
                [new ActorMailboxId(
                    ActorType.Realtime,
                    FuturesTickTradeDataInsertedEvent.Actor)] =
                [
                    FuturesTickTradeDataInsertedEvent.Verb
                ]
            },
            EventHandlerAsync);

        try
        {
            if (marketDataApi.ActiveValueDate is { } activeValueDate)
                await marketDataApi.StopAsync(activeValueDate);
            await dbFixture.MarketDataDb.DeleteVixFuturesEodDataAsync(
                ContractId,
                valueDate);

            _httpClientFactory.CreateClient();
            var commandServiceApi = new CommandServiceApiClient(
                _httpClientFactory,
                _jsonSerializer,
                new CommandServiceApiOptions("http://localhost"));
            var feedApi = new MarketDataFeedCommandApi(commandServiceApi);

            var startResponse = await feedApi.StartFuturesTickDataStreamingAsync(
                contract,
                valueDate,
                false);
            startResponse.Success.Should().BeTrue(startResponse.ErrorMessage);

            var startResult = await startTerminal.Task.WaitAsync(
                TimeSpan.FromSeconds(10));
            if (startResult is FuturesTickDataStreamingStartedFailEvent startFailed)
            {
                throw new InvalidOperationException(
                    $"Deterministic VX stream start failed with {startFailed.ErrorCode}: "
                    + startFailed.ErrorMessage);
            }
            startResult.Should().BeOfType<FuturesTickDataStreamingStartedCompleteEvent>();

            var publisher = _factory.Services
                .GetRequiredService<ITickAggregationEventPublisher>();
            publisher.IsRunning.Should().BeTrue();
            await publisher.PublishAsync(normalizedTrade);

            var persistedTrade = await persistedTradeReceived.Task.WaitAsync(
                TimeSpan.FromSeconds(15));
            persistedTrade.CommandId.Should().Be(sourceCommandId);
            persistedTrade.EntityId.Should().Be(entityId);
            persistedTrade.TradeData.Price.Should().Be(tradePrice);

            var terminalResult = await eodTerminal.Task.WaitAsync(
                TimeSpan.FromSeconds(15));

            terminalResult.Should().BeOfType<VixFuturesEodDataInsertedCompleteEvent>();
            inserted.Should().NotBeNull();
            inserted!.VixFuturesTickData.ContractId.Should().Be(ContractId);
            inserted.VixFuturesTickData.ValueDate.Should().Be(valueDate);
            inserted.VixFuturesTickData.TickId.Should().Be(tickDataId.SequenceId);
            inserted.VixFuturesTickData.Price.Should().Be(tradePrice);
            inserted.VixFuturesTickData.Size.Should().Be((int)tradeSize);

            var stored = await dbFixture.MarketDataDb.GetVixFuturesEodDataAsync(
                ContractId,
                valueDate);
            stored.Should().NotBeNull();
            stored!.ClosePrice.Should().Be(tradePrice);
            stored.Volume.Should().Be((int)tradeSize);

            var quoteTimestamp = DateTimeOffset.UtcNow.AddMilliseconds(1);
            const decimal bidPrice = 18.80m;
            const decimal askPrice = 19.00m;
            var quotePrice = new FuturesMarketPriceUpdatedRealtimeEvent
            {
                Subject = new ActorSubject(
                    ActorType.Realtime,
                    FuturesMarketPriceUpdatedRealtimeEvent.Actor,
                    FuturesMarketPriceUpdatedRealtimeEvent.Verb,
                    entityId.Format()),
                Id = Guid.NewGuid(),
                EntityId = entityId,
                CommandId = Guid.NewGuid(),
                AggregateId = entityId.Format(),
                EventSource = nameof(NormalizedVxTradeAndQuote_RouteThroughRealtimeActors_AndPersistEod),
                ReceivedOn = quoteTimestamp.UtcDateTime,
                UpdateSource = FuturesMarketPriceUpdateSource.Quote,
                Price = new FuturesMarketPriceSnapshot(
                    ContractId,
                    2,
                    1,
                    AssetTypeId.Futures,
                    valueDate,
                    new FuturesMarketQuoteSnapshot(
                        bidPrice,
                        4,
                        askPrice,
                        5,
                        1,
                        1,
                        92,
                        quoteTimestamp,
                        quoteTimestamp),
                    new FuturesMarketTradeSnapshot(
                        tradePrice,
                        tradeSize,
                        91,
                        quoteTimestamp.AddSeconds(-1),
                        quoteTimestamp.AddSeconds(-1)))
            };
            await publisher.PublishAsync(quotePrice);

            var quoteTerminalResult = await quoteEodTerminal.Task.WaitAsync(
                TimeSpan.FromSeconds(15));
            if (quoteTerminalResult is VixFuturesEodDataInsertedFailEvent quoteFailed)
            {
                throw new InvalidOperationException(
                    $"Deterministic VX quote EOD update failed with {quoteFailed.ErrorCode}: "
                    + quoteFailed.ErrorMessage);
            }
            quoteTerminalResult.Should().BeOfType<VixFuturesEodDataInsertedCompleteEvent>();
            quoteInserted.Should().NotBeNull();
            quoteInserted!.VixFuturesTickData.Price.Should().Be(18.90m);
            quoteInserted.VixFuturesTickData.Size.Should().Be(0);

            var quoteStored = await dbFixture.MarketDataDb.GetVixFuturesEodDataAsync(
                ContractId,
                valueDate);
            quoteStored.Should().NotBeNull();
            quoteStored!.ClosePrice.Should().Be(18.90m);
            quoteStored.Volume.Should().Be((int)tradeSize);
        }
        finally
        {
            try
            {
                if (marketDataApi.ActiveValueDate is { } activeValueDate)
                    await marketDataApi.StopAsync(activeValueDate);
            }
            finally
            {
                await listener.StopAsync();
                await realtimeListener.StopAsync();
            }
        }

        ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            switch (eventVerb)
            {
                case FuturesTickDataStreamingStartedCompleteEvent.Verb:
                {
                    var completed = eventMsg
                        .AsEvent<FuturesTickDataStreamingStartedCompleteEvent>();
                    if (completed?.EntityId.ValueDate == valueDate)
                        startTerminal.TrySetResult(completed);
                    break;
                }
                case FuturesTickDataStreamingStartedFailEvent.Verb:
                {
                    var failed = eventMsg
                        .AsEvent<FuturesTickDataStreamingStartedFailEvent>();
                    if (failed?.EntityId.ValueDate == valueDate)
                        startTerminal.TrySetResult(failed);
                    break;
                }
                case VixFuturesEodDataInsertedEvent.Verb:
                {
                    var received = eventMsg.AsEvent<VixFuturesEodDataInsertedEvent>();
                    if (received?.EntityId == new FuturesEodDataId(ContractId, valueDate))
                    {
                        if (received.VixFuturesTickData.Size == 0)
                            quoteInserted = received;
                        else
                            inserted = received;
                    }
                    break;
                }
                case VixFuturesEodDataInsertedCompleteEvent.Verb:
                {
                    var completed = eventMsg
                        .AsEvent<VixFuturesEodDataInsertedCompleteEvent>();
                    if (completed?.EntityId == new FuturesEodDataId(ContractId, valueDate))
                    {
                        if (completed.VixFuturesTickData.Size == 0)
                            quoteEodTerminal.TrySetResult(completed);
                        else
                            eodTerminal.TrySetResult(completed);
                    }
                    break;
                }
                case VixFuturesEodDataInsertedFailEvent.Verb:
                {
                    var failed = eventMsg
                        .AsEvent<VixFuturesEodDataInsertedFailEvent>();
                    if (failed?.EntityId == new FuturesEodDataId(ContractId, valueDate))
                    {
                        eodTerminal.TrySetResult(failed);
                        quoteEodTerminal.TrySetResult(failed);
                    }
                    break;
                }
                case FuturesTickTradeDataInsertedEvent.Verb:
                {
                    var received = eventMsg
                        .AsEvent<FuturesTickTradeDataInsertedEvent>();
                    if (received?.EntityId == entityId)
                        persistedTradeReceived.TrySetResult(received);
                    break;
                }
            }
            return ValueTask.CompletedTask;
        }
    }
}
