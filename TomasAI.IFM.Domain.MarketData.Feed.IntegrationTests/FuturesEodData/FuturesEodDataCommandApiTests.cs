using TomasAI.IFM.Domain.MarketData.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Common;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests.FuturesEodData;

public class FuturesEodDataCommandApiTests(WebApplicationFactory<Program> factory, MarketDataFeedFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<MarketDataFeedFixture>
{
    readonly WebApplicationFactory<Program> _factory = factory;
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();
    readonly ILogger<NatsActorEventListener> _logger = Substitute.For<ILogger<NatsActorEventListener>>();

    [Fact]
    public async Task InsertFuturesEodData_PublishesTypedUpdatedNotifyEvent()
    {
        var notificationReceived = new TaskCompletionSource<FuturesEodDataUpdatedNotifyEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var notificationListener = new NatsActorEventListener(
            new NatsEventListenerOptions(),
            _logger);

        await notificationListener.StartAsync(
            "FuturesEodDataUpdatedNotifyEventIntegrationTest",
            new()
            {
                [new ActorMailboxId(
                    ActorType.Notify,
                    FuturesEodDataUpdatedNotifyEvent.Actor)] =
                [
                    FuturesEodDataUpdatedNotifyEvent.Verb
                ]
            },
            NotificationHandlerAsync);

        try
        {
            var valueDate = SampleData.ValueDate;
            var contractId = SampleData.FuturesContractId;
            var entityId = new FuturesEodDataId(contractId, valueDate);
            await dbFixture.MarketDataDb.DeleteFuturesEodDataAsync(contractId, valueDate);
            await dbFixture.DeleteRawEodObservationAsync(
                MarketSeriesIdentity.ForContract(contractId).Format(),
                contractId,
                valueDate);

            _httpClientFactory.CreateClient();
            var commandServiceApi = new CommandServiceApiClient(
                _httpClientFactory,
                _jsonSerializer,
                new CommandServiceApiOptions("http://localhost"));
            var marketDataFeedApi = new MarketDataFeedCommandApi(commandServiceApi);

            var response = await marketDataFeedApi.InsertFuturesEodDataAsync(
                valueDate,
                SampleData.UnderlyingFuturesTickData,
                SampleData.FuturesContract,
                SampleData.FuturesEodDataRange[0],
                SampleData.FuturesEodDataRange,
                new NormalCurveTableReadModel([new NormalCurveDataReadModel(0, 50.0)]),
                20,
                []);

            response.Success.Should().BeTrue(response.ErrorMessage);
            response.Value.Should().NotBe(Guid.Empty);

            var notification = await notificationReceived.Task.WaitAsync(
                TimeSpan.FromSeconds(10));

            notification.IsValid.Should().BeTrue();
            notification.Subject.Should().Be(new ActorSubject(
                ActorType.Notify,
                FuturesEodDataUpdatedNotifyEvent.Actor,
                FuturesEodDataUpdatedNotifyEvent.Verb,
                entityId.Format()));
            notification.EntityId.Should().Be(entityId);
            notification.CommandId.Should().Be(response.Value);
            notification.EventSource.Should().Be(nameof(FuturesEodDataInsertedCompleteEvent));
            notification.FuturesEodData.ContractId.Should().Be(contractId);
            notification.FuturesEodData.ValueDate.Should().Be(valueDate);
            notification.FuturesEodData.ClosePrice.Should().Be(
                SampleData.UnderlyingFuturesTickData.Price);
        }
        finally
        {
            await notificationListener.StopAsync();
        }

        ValueTask NotificationHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            if (eventVerb == FuturesEodDataUpdatedNotifyEvent.Verb)
            {
                var notification = eventMsg.AsEvent<FuturesEodDataUpdatedNotifyEvent>();
                if (notification is not null)
                    notificationReceived.TrySetResult(notification);
            }
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task InsertFuturesEodData_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        var notificationListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        FuturesEodDataInsertedEvent futuresEodDataInsertedEvent = default!;
        FuturesEodDataInsertedCompleteEvent futuresEodDataInsertedCompleteEvent = default!;
        FuturesEodDataInsertedFailEvent futuresEodDataInsertedFailEvent = default!;
        FuturesEodDataUpdatedNotifyEvent futuresEodDataNotification = default!;
        var terminalEventReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var notificationReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, FuturesEodDataInsertedEvent.Actor)] =
                [
                    FuturesEodDataInsertedEvent.Verb,
                    FuturesEodDataInsertedCompleteEvent.Verb,
                    FuturesEodDataInsertedFailEvent.Verb
                ]
            },
            EventHandlerAsync
        );
        await notificationListener.StartAsync(
            "TestFuturesEodNotificationListener",
            new()
            {
                [new ActorMailboxId(
                    ActorType.Notify,
                    FuturesEodDataUpdatedNotifyEvent.Actor)] =
                [
                    FuturesEodDataUpdatedNotifyEvent.Verb
                ]
            },
            NotificationHandlerAsync);

        var valueDate = SampleData.ValueDate;
        var contractId = SampleData.FuturesContractId;
        var futuresTickData = SampleData.UnderlyingFuturesTickData;
        var contract = SampleData.FuturesContract;
        var eodDataToday = SampleData.FuturesEodDataRange[0];
        var eodDataRange = SampleData.FuturesEodDataRange;
        var normCurveData = new NormalCurveTableReadModel([new NormalCurveDataReadModel(0, 50.0)]);
        var windowSize = 20;
        var vixEodData = Array.Empty<VixFuturesEodDataReadModel>();
        await dbFixture.MarketDataDb.DeleteFuturesEodDataAsync(contractId, valueDate);
        await dbFixture.DeleteRawEodObservationAsync(
            MarketSeriesIdentity.ForContract(contractId).Format(),
            contractId,
            valueDate);

        // act...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var marketDataFeedApi = new MarketDataFeedCommandApi(commandServiceApi);
        var response = await marketDataFeedApi.InsertFuturesEodDataAsync(
            valueDate, futuresTickData, contract, eodDataToday, eodDataRange, normCurveData, windowSize, vixEodData);

        response.Should().NotBeNull();
        response.Success.Should().BeTrue(response.ErrorMessage);
        response.Value.Should().NotBe(Guid.Empty);

        await terminalEventReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await notificationReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // assert...
        futuresEodDataInsertedEvent.Should().NotBeNull();
        futuresEodDataInsertedCompleteEvent.Should().NotBeNull();
        futuresEodDataInsertedFailEvent.Should().BeNull();
        futuresEodDataNotification.Should().NotBeNull();
        futuresEodDataNotification.Subject.ActorType.Should().Be(ActorType.Notify);
        futuresEodDataNotification.CommandId.Should().Be(response.Value);
        futuresEodDataNotification.FuturesEodData.Should().BeEquivalentTo(
            futuresEodDataInsertedCompleteEvent.FuturesEodData);

        var observationStore = _factory.Services
            .GetRequiredService<IHistoricalObservationStore>();
        var insertedEodData = await observationStore.GetRawEodAsync(
            MarketSeriesIdentity.ForContract(contractId),
            valueDate,
            CancellationToken.None);
        insertedEodData.Should().NotBeNull();
        insertedEodData!.ContractId.Should().Be(contractId);
        insertedEodData.ValueDate.Should().Be(valueDate);
        insertedEodData.Open.Should().Be(
            futuresEodDataInsertedCompleteEvent.FuturesEodData.OpenPrice);
        insertedEodData.High.Should().Be(
            futuresEodDataInsertedCompleteEvent.FuturesEodData.HighPrice);
        insertedEodData.Low.Should().Be(
            futuresEodDataInsertedCompleteEvent.FuturesEodData.LowPrice);
        insertedEodData.Close.Should().Be(
            futuresEodDataInsertedCompleteEvent.FuturesEodData.ClosePrice);
        insertedEodData.Volume.Should().Be(
            futuresEodDataInsertedCompleteEvent.FuturesEodData.Volume);
        insertedEodData.IsComplete.Should().BeTrue();
        insertedEodData.IsValid.Should().BeTrue();

        await eventListener.StopAsync();
        await notificationListener.StopAsync();

        ValueTask NotificationHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            if (eventVerb == FuturesEodDataUpdatedNotifyEvent.Verb)
            {
                futuresEodDataNotification = eventMsg.AsEvent<FuturesEodDataUpdatedNotifyEvent>()!;
                notificationReceived.TrySetResult();
            }
            return ValueTask.CompletedTask;
        }

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == FuturesEodDataInsertedEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesEodDataInsertedEvent>()!),
                _ when eventVerb == FuturesEodDataInsertedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesEodDataInsertedCompleteEvent>()!),
                _ when eventVerb == FuturesEodDataInsertedFailEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesEodDataInsertedFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is FuturesEodDataInsertedEvent inserted)
                    futuresEodDataInsertedEvent = inserted;
                if (@event is FuturesEodDataInsertedCompleteEvent insertedComplete)
                {
                    futuresEodDataInsertedCompleteEvent = insertedComplete;
                    terminalEventReceived.TrySetResult();
                }
                if (@event is FuturesEodDataInsertedFailEvent insertedFail)
                {
                    futuresEodDataInsertedFailEvent = insertedFail;
                    terminalEventReceived.TrySetResult();
                }
                return @event;
            }
        }
    }

    [Fact]
    public async Task InsertVixFuturesEodData_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        VixFuturesEodDataInsertedEvent vixFuturesEodDataInsertedEvent = default!;
        VixFuturesEodDataInsertedCompleteEvent vixFuturesEodDataInsertedCompleteEvent = default!;
        VixFuturesEodDataInsertedFailEvent vixFuturesEodDataInsertedFailEvent = default!;
        var terminalEventReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, VixFuturesEodDataInsertedEvent.Actor)] =
                [
                    VixFuturesEodDataInsertedEvent.Verb,
                    VixFuturesEodDataInsertedCompleteEvent.Verb,
                    VixFuturesEodDataInsertedFailEvent.Verb
                ]
            },
            EventHandlerAsync
        );

        var valueDate = SampleData.ValueDate;
        var vixContractId = "VXZ25";
        var vixFuturesTickData = new FuturesTickDataV2ReadModel(
            contractId: vixContractId,
            valueDate: valueDate,
            tickId: 1,
            tickTime: TimeOnly.FromDateTime(DateTime.UtcNow),
            price: 18.50m,
            size: 100);

        await dbFixture.MarketDataDb.DeleteVixFuturesEodDataAsync(vixContractId, valueDate);

        // act...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var marketDataFeedApi = new MarketDataFeedCommandApi(commandServiceApi);
        var response = await marketDataFeedApi.InsertVixFuturesEodDataAsync(vixFuturesTickData);

        response.Should().NotBeNull();
        response.Success.Should().BeTrue(response.ErrorMessage);
        response.Value.Should().NotBe(Guid.Empty);

        await terminalEventReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // assert...
        vixFuturesEodDataInsertedEvent.Should().NotBeNull();
        vixFuturesEodDataInsertedCompleteEvent.Should().NotBeNull();
        vixFuturesEodDataInsertedFailEvent.Should().BeNull();

        var insertedVixEodData = await dbFixture.MarketDataDb.GetVixFuturesEodDataAsync(vixContractId, valueDate);
        insertedVixEodData.Should().NotBeNull();
        insertedVixEodData!.ContractId.Should().Be(vixContractId);
        insertedVixEodData.ValueDate.Should().Be(valueDate);

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == VixFuturesEodDataInsertedEvent.Verb => SetEvent(eventMsg.AsEvent<VixFuturesEodDataInsertedEvent>()!),
                _ when eventVerb == VixFuturesEodDataInsertedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<VixFuturesEodDataInsertedCompleteEvent>()!),
                _ when eventVerb == VixFuturesEodDataInsertedFailEvent.Verb => SetEvent(eventMsg.AsEvent<VixFuturesEodDataInsertedFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is VixFuturesEodDataInsertedEvent inserted)
                    vixFuturesEodDataInsertedEvent = inserted;
                if (@event is VixFuturesEodDataInsertedCompleteEvent insertedComplete)
                {
                    vixFuturesEodDataInsertedCompleteEvent = insertedComplete;
                    terminalEventReceived.TrySetResult();
                }
                if (@event is VixFuturesEodDataInsertedFailEvent insertedFail)
                {
                    vixFuturesEodDataInsertedFailEvent = insertedFail;
                    terminalEventReceived.TrySetResult();
                }
                return @event;
            }
        }
    }
}
