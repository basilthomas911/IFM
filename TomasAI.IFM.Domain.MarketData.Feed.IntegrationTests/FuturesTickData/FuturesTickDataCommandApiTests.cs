using TomasAI.IFM.Domain.MarketData.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;

namespace TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests.FuturesTickData;

public class FuturesTickDataCommandApiTests(WebApplicationFactory<Program> factory, MarketDataFeedFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<MarketDataFeedFixture>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();
    readonly ILogger<NatsActorEventListener> _logger = Substitute.For<ILogger<NatsActorEventListener>>();

    [Fact]
    public async Task InsertFuturesTickData_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        FuturesTickDataInsertedEvent futuresTickDataInsertedEvent = default!;
        FuturesTickDataInsertedCompleteEvent futuresTickDataInsertedCompleteEvent = default!;
        FuturesTickDataInsertedFailEvent futuresTickDataInsertedFailEvent = default!;
        var domainEventReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var terminalEventReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await eventListener.StartAsync(
            $"{nameof(InsertFuturesTickData_Ok)}-{Guid.NewGuid():N}",
            new()
            {
                [new ActorMailboxId(ActorType.Event, FuturesTickDataInsertedEvent.Actor)] =
                [
                    FuturesTickDataInsertedEvent.Verb,
                    FuturesTickDataInsertedCompleteEvent.Verb,
                    FuturesTickDataInsertedFailEvent.Verb
                ]
            },
            EventHandlerAsync
        );

        var contract = SampleData.FuturesContract;
        var tickData = SampleData.UnderlyingFuturesTickData;
        dbFixture.BlackboardService.MarketDataFeed.FuturesEodDataRange.Remove(SampleData.UnderlyingFuturesTickData.ContractId, SampleData.UnderlyingFuturesTickData.ValueDate);
        await dbFixture.MarketDataDb.DeleteFuturesEodDataAsync(SampleData.FuturesEodData.ContractId, SampleData.FuturesEodData.ValueDate);
        await dbFixture.MarketDataDb.InsertFuturesEodDataAsync(SampleData.FuturesEodData);
        await dbFixture.MarketDataDb.DeleteFuturesTickDataAsync(SampleData.UnderlyingFuturesTickData.ContractId, SampleData.UnderlyingFuturesTickData.ValueDate);
        await dbFixture.MarketDataDb.InsertFuturesTickDataAsync(SampleData.UnderlyingFuturesTickData);
        var contractId = SampleData.FuturesContractId;
        var eodDataRange = SampleData.FuturesEodDataRange;
        var startDate = eodDataRange.Last().ValueDate;
        var endDate = eodDataRange.First().ValueDate;

        foreach (var eodData in eodDataRange)
            await dbFixture.MarketDataDb.DeleteFuturesEodDataAsync(eodData.ContractId, eodData.ValueDate);
        await dbFixture.MarketDataDb.InsertFuturesEodDataAsync(eodDataRange);

        // act...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var marketDataFeedApi = new MarketDataFeedCommandApi(commandServiceApi);
        var response = await marketDataFeedApi.InsertFuturesTickDataAsync(contract, tickData);

        response.Should().NotBeNull();
        response.Success.Should().BeTrue(response.ErrorMessage);
        response.Value.Should().NotBe(Guid.Empty);

        await Task.WhenAll(domainEventReceived.Task, terminalEventReceived.Task).WaitAsync(TimeSpan.FromSeconds(10));

        // assert...
        futuresTickDataInsertedEvent.Should().NotBeNull();
        futuresTickDataInsertedFailEvent.Should().BeNull(futuresTickDataInsertedFailEvent?.ErrorMessage);
        futuresTickDataInsertedCompleteEvent.Should().NotBeNull();

        futuresTickDataInsertedEvent.Contract.ContractId.Should().Be(contract.ContractId);
        futuresTickDataInsertedEvent.TickData.ContractId.Should().Be(tickData.ContractId);
        futuresTickDataInsertedEvent.TickData.ValueDate.Should().Be(tickData.ValueDate);

        // verify data was inserted into database...
        var lastTickDataId = await dbFixture.MarketDataDb.GetLastFuturesTickDataIdAsync(tickData.ContractId, tickData.ValueDate);
        lastTickDataId.Should().NotBeNull();
        lastTickDataId!.ContractId.Should().Be(tickData.ContractId);
        lastTickDataId.ValueDate.Should().Be(tickData.ValueDate);

        var insertedTickData = await dbFixture.MarketDataDb.GetFuturesTickDataAsync(lastTickDataId);
        insertedTickData.Should().NotBeNull();
        insertedTickData!.ContractId.Should().Be(tickData.ContractId);
        insertedTickData.ValueDate.Should().Be(tickData.ValueDate);
        insertedTickData.Price.Should().Be(tickData.Price);
        insertedTickData.Size.Should().Be(tickData.Size);

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == FuturesTickDataInsertedEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesTickDataInsertedEvent>()!),
                _ when eventVerb == FuturesTickDataInsertedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesTickDataInsertedCompleteEvent>()!),
                _ when eventVerb == FuturesTickDataInsertedFailEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesTickDataInsertedFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is FuturesTickDataInsertedEvent inserted)
                {
                    futuresTickDataInsertedEvent = inserted;
                    domainEventReceived.TrySetResult();
                }
                if (@event is FuturesTickDataInsertedCompleteEvent insertedComplete)
                {
                    futuresTickDataInsertedCompleteEvent = insertedComplete;
                    terminalEventReceived.TrySetResult();
                }
                if (@event is FuturesTickDataInsertedFailEvent insertedFail)
                {
                    futuresTickDataInsertedFailEvent = insertedFail;
                    terminalEventReceived.TrySetResult();
                }
                return @event;
            }
        }
    }

    [Fact]
    public async Task StartFuturesTickDataStreaming_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        FuturesTickDataStreamingStartedEvent futuresTickDataStreamingStartedEvent = default!;
        FuturesTickDataStreamingStartedCompleteEvent futuresTickDataStreamingStartedCompleteEvent = default!;
        FuturesTickDataStreamingStartedFailEvent futuresTickDataStreamingStartedFailEvent = default!;
        var domainEventReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var terminalEventReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await eventListener.StartAsync(
            $"{nameof(StartFuturesTickDataStreaming_Ok)}-{Guid.NewGuid():N}",
            new()
            {
                [new ActorMailboxId(ActorType.Event, FuturesTickDataStreamingStartedEvent.Actor)] =
                [
                    FuturesTickDataStreamingStartedEvent.Verb,
                    FuturesTickDataStreamingStartedCompleteEvent.Verb,
                    FuturesTickDataStreamingStartedFailEvent.Verb
                ]
            },
            EventHandlerAsync
        );

        var contract = SampleData.FuturesContract;
        var valueDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-Random.Shared.Next(1, 10_000));

        // act...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var marketDataFeedApi = new MarketDataFeedCommandApi(commandServiceApi);
        var response = await marketDataFeedApi.StartFuturesTickDataStreamingAsync(contract, valueDate, false);

        response.Should().NotBeNull();
        response.Success.Should().BeTrue(response.ErrorMessage);
        response.Value.Should().NotBe(Guid.Empty);

        await Task.WhenAll(domainEventReceived.Task, terminalEventReceived.Task).WaitAsync(TimeSpan.FromSeconds(10));

        // assert...
        futuresTickDataStreamingStartedEvent.Should().NotBeNull();
        futuresTickDataStreamingStartedCompleteEvent.Should().BeNull();
        futuresTickDataStreamingStartedFailEvent.Should().NotBeNull();
        futuresTickDataStreamingStartedFailEvent.ErrorMessage.Should().Be("Market data API failed to start for unknown reasons.");

        futuresTickDataStreamingStartedEvent.Contract.ContractId.Should().Be(contract.ContractId);
        futuresTickDataStreamingStartedEvent.ValueDate.Should().Be(valueDate);

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == FuturesTickDataStreamingStartedEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesTickDataStreamingStartedEvent>()!),
                _ when eventVerb == FuturesTickDataStreamingStartedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesTickDataStreamingStartedCompleteEvent>()!),
                _ when eventVerb == FuturesTickDataStreamingStartedFailEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesTickDataStreamingStartedFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is FuturesTickDataStreamingStartedEvent started)
                {
                    futuresTickDataStreamingStartedEvent = started;
                    domainEventReceived.TrySetResult();
                }
                if (@event is FuturesTickDataStreamingStartedCompleteEvent startedComplete)
                {
                    futuresTickDataStreamingStartedCompleteEvent = startedComplete;
                    terminalEventReceived.TrySetResult();
                }
                if (@event is FuturesTickDataStreamingStartedFailEvent startedFail)
                {
                    futuresTickDataStreamingStartedFailEvent = startedFail;
                    terminalEventReceived.TrySetResult();
                }
                return @event;
            }
        }
    }

    [Fact]
    public async Task StopFuturesTickDataStreaming_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        FuturesTickDataStreamingStartedCompleteEvent futuresTickDataStreamingStartedCompleteEvent = default!;
        FuturesTickDataStreamingStartedFailEvent futuresTickDataStreamingStartedFailEvent = default!;
        FuturesTickDataStreamingStoppedEvent futuresTickDataStreamingStoppedEvent = default!;
        FuturesTickDataStreamingStoppedCompleteEvent futuresTickDataStreamingStoppedCompleteEvent = default!;
        FuturesTickDataStreamingStoppedFailEvent futuresTickDataStreamingStoppedFailEvent = default!;
        var startTerminalEventReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopDomainEventReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopTerminalEventReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await eventListener.StartAsync(
            $"{nameof(StopFuturesTickDataStreaming_Ok)}-{Guid.NewGuid():N}",
            new()
            {
                [new ActorMailboxId(ActorType.Event, FuturesTickDataStreamingStoppedEvent.Actor)] =
                [
                    FuturesTickDataStreamingStartedCompleteEvent.Verb,
                    FuturesTickDataStreamingStartedFailEvent.Verb,
                    FuturesTickDataStreamingStoppedEvent.Verb,
                    FuturesTickDataStreamingStoppedCompleteEvent.Verb,
                    FuturesTickDataStreamingStoppedFailEvent.Verb
                ]
            },
            EventHandlerAsync
        );

        var contract = SampleData.FuturesContract;
        var valueDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-Random.Shared.Next(1, 10_000));

        // start streaming first...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var marketDataFeedApi = new MarketDataFeedCommandApi(commandServiceApi);
        var startResponse = await marketDataFeedApi.StartFuturesTickDataStreamingAsync(contract, valueDate, false);

        startResponse.Should().NotBeNull();
        startResponse.Success.Should().BeTrue(startResponse.ErrorMessage);
        startResponse.Value.Should().NotBe(Guid.Empty);

        await startTerminalEventReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));
        futuresTickDataStreamingStartedCompleteEvent.Should().BeNull();
        futuresTickDataStreamingStartedFailEvent.Should().NotBeNull();
        futuresTickDataStreamingStartedFailEvent.ErrorMessage.Should().Be("Market data API failed to start for unknown reasons.");

        // act... stop streaming
        var response = await marketDataFeedApi.StopFuturesTickDataStreamingAsync(contract.ContractId, valueDate);

        response.Should().NotBeNull();
        response.Success.Should().BeTrue(response.ErrorMessage);
        response.Value.Should().NotBe(Guid.Empty);

        await stopDomainEventReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await stopTerminalEventReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // assert...
        futuresTickDataStreamingStoppedEvent.Should().NotBeNull();
        futuresTickDataStreamingStoppedCompleteEvent.Should().BeNull();
        futuresTickDataStreamingStoppedFailEvent.Should().NotBeNull();
        futuresTickDataStreamingStoppedFailEvent.ErrorMessage.Should().Be($"Stream ID not found for futures contract {contract.ContractId}.");

        futuresTickDataStreamingStoppedEvent.ContractId.Should().Be(contract.ContractId);

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == FuturesTickDataStreamingStartedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesTickDataStreamingStartedCompleteEvent>()!),
                _ when eventVerb == FuturesTickDataStreamingStartedFailEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesTickDataStreamingStartedFailEvent>()!),
                _ when eventVerb == FuturesTickDataStreamingStoppedEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesTickDataStreamingStoppedEvent>()!),
                _ when eventVerb == FuturesTickDataStreamingStoppedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesTickDataStreamingStoppedCompleteEvent>()!),
                _ when eventVerb == FuturesTickDataStreamingStoppedFailEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesTickDataStreamingStoppedFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is FuturesTickDataStreamingStartedCompleteEvent startedComplete)
                {
                    futuresTickDataStreamingStartedCompleteEvent = startedComplete;
                    startTerminalEventReceived.TrySetResult();
                }
                if (@event is FuturesTickDataStreamingStartedFailEvent startedFail)
                {
                    futuresTickDataStreamingStartedFailEvent = startedFail;
                    startTerminalEventReceived.TrySetResult();
                }
                if (@event is FuturesTickDataStreamingStoppedEvent stopped)
                {
                    futuresTickDataStreamingStoppedEvent = stopped;
                    stopDomainEventReceived.TrySetResult();
                }
                if (@event is FuturesTickDataStreamingStoppedCompleteEvent stoppedComplete)
                {
                    futuresTickDataStreamingStoppedCompleteEvent = stoppedComplete;
                    stopTerminalEventReceived.TrySetResult();
                }
                if (@event is FuturesTickDataStreamingStoppedFailEvent stoppedFail)
                {
                    futuresTickDataStreamingStoppedFailEvent = stoppedFail;
                    stopTerminalEventReceived.TrySetResult();
                }
                return @event;
            }
        }
    }
}
