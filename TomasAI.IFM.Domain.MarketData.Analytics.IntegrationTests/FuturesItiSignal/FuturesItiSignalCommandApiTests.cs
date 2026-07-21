using System.Diagnostics;
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
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Shared.MarketDataAnalytics.Commands;

namespace TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests.FuturesItiSignal;

public class FuturesItiSignalCommandApiTests(WebApplicationFactory<Program> factory, MarketDataAnalyticsFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<MarketDataAnalyticsFixture>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();
    readonly ILogger<NatsActorEventListener> _logger = Substitute.For<ILogger<NatsActorEventListener>>();

    [Fact]
    public async Task GenerateFuturesItiSignal_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        FuturesItiSignalGeneratedEvent futuresItiSignalGeneratedEvent = default!;
        FuturesItiSignalGeneratedCompleteEvent futuresItiSignalGeneratedCompleteEvent = default!;
        FuturesItiSignalGeneratedFailEvent futuresItiSignalGeneratedFailEvent = default!;

       
        var contractId = SampleData.ContractId;
        var valueDate = SampleData.ValueDate;

        var entityId = SampleData.EntityId;
        var subject = new ActorSubject(ActorType.Command, GenerateFuturesItiSignalCommand.Actor, GenerateFuturesItiSignalCommand.Verb, entityId.Format());
        var eventStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{subject.ThreadId}");
        if (eventStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(eventStreamId);
        await dbFixture.MarketDataDb.DeleteFuturesItiSignalAsync(contractId, valueDate, SampleData.TimePeriod);

        // act...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));

        await eventListener.StartAsync(
           "TestEventListener",
           new()
           {
               [new ActorMailboxId(ActorType.Event, FuturesItiSignalGeneratedEvent.Actor)] = [FuturesItiSignalGeneratedEvent.Verb]
           },
           EventHandlerAsync
       );

        var marketDataAnalyticsApi = new MarketDataAnalyticsCommandApi(commandServiceApi);

        var sw = Stopwatch.StartNew();
        var response = await marketDataAnalyticsApi.GenerateFuturesItiSignalAsync(
            contractId, valueDate, SampleData.TimePeriod, SampleData.Timestamp, SampleData.FuturesPrice, SampleData.VixFuturesPrice);

        await Task.Delay(1);

        sw.Stop();
        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
        futuresItiSignalGeneratedEvent.Should().NotBeNull();
        if (futuresItiSignalGeneratedCompleteEvent is null)
        {
            futuresItiSignalGeneratedFailEvent.Should().NotBeNull();
            Assert.Fail(futuresItiSignalGeneratedFailEvent.ErrorMessage);
        }
        else
        {
            futuresItiSignalGeneratedCompleteEvent.Should().NotBeNull();
            futuresItiSignalGeneratedEvent.FuturesItiSignal.Should().NotBeNull();
            futuresItiSignalGeneratedEvent.FuturesItiSignal!.ContractId.Should().Be(contractId);
            futuresItiSignalGeneratedEvent.FuturesItiSignal.ValueDate.Should().Be(valueDate);

            var signals = await dbFixture.MarketDataDb.GetFuturesItiSignalsAsync(entityId);
            signals.Should().NotBeEmpty();
            signals.First().ContractId.Should().Be(contractId);
            signals.First().ValueDate.Should().Be(valueDate);
            Assert.True(true, $"Elapsed time: {sw.ElapsedMilliseconds} ms");
        }

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == FuturesItiSignalGeneratedEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesItiSignalGeneratedEvent>()!),
                _ when eventVerb == FuturesItiSignalGeneratedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesItiSignalGeneratedCompleteEvent>()!),
                _ when eventVerb == FuturesItiSignalGeneratedFailEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesItiSignalGeneratedFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is FuturesItiSignalGeneratedEvent generated)
                    futuresItiSignalGeneratedEvent = generated;
                if (@event is FuturesItiSignalGeneratedCompleteEvent generatedComplete)
                    futuresItiSignalGeneratedCompleteEvent = generatedComplete;
                if (@event is FuturesItiSignalGeneratedFailEvent generatedFail)
                    futuresItiSignalGeneratedFailEvent = generatedFail;
                return @event;
            }
        }
    }

    [Fact]
    public async Task SetFuturesItiSignalHoldTrade_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        FuturesItiSignalGeneratedEvent futuresItiSignalGeneratedEvent = default!;
        FuturesItiSignalGeneratedCompleteEvent futuresItiSignalGeneratedCompleteEvent = default!;
        FuturesItiSignalGeneratedFailEvent futuresItiSignalGeneratedFailEvent = default!;

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, FuturesItiSignalGeneratedEvent.Actor)] = [FuturesItiSignalGeneratedEvent.Verb]
            },
            EventHandlerAsync
        );

        var contractId = SampleData.ContractId;
        var valueDate = SampleData.ValueDate;

        var entityId = SampleData.EntityId;
        var generateSubject = new ActorSubject(ActorType.Command, GenerateFuturesItiSignalCommand.Actor, GenerateFuturesItiSignalCommand.Verb, entityId.Format());
        var generateStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{generateSubject.ThreadId}");
        if (generateStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(generateStreamId);

        var setSubject = new ActorSubject(ActorType.Command, SetFuturesItiSignalHoldTradeCommand.Actor, SetFuturesItiSignalHoldTradeCommand.Verb, entityId.Format());
        var setStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{setSubject.ThreadId}");
        if (setStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(setStreamId);

        await dbFixture.MarketDataDb.DeleteFuturesItiSignalAsync(contractId, valueDate, SampleData.TimePeriod);

        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var marketDataAnalyticsApi = new MarketDataAnalyticsCommandApi(commandServiceApi);

        await marketDataAnalyticsApi.GenerateFuturesItiSignalAsync(
            contractId, valueDate, SampleData.TimePeriod, SampleData.Timestamp,
            SampleData.FuturesPrice, 0);
        await Task.Delay(10);

        futuresItiSignalGeneratedEvent = default!;
        futuresItiSignalGeneratedCompleteEvent = default!;
        futuresItiSignalGeneratedFailEvent = default!;

        // act...
        var itiSignalId = new FuturesItiSignalId(contractId, valueDate, SampleData.TimePeriod, SampleData.Timestamp);
        var response = await marketDataAnalyticsApi.SetFuturesItiSignalHoldTradeAsync(itiSignalId);

        await Task.Delay(10);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
        futuresItiSignalGeneratedEvent.Should().NotBeNull();
        futuresItiSignalGeneratedCompleteEvent.Should().NotBeNull();
        futuresItiSignalGeneratedFailEvent.Should().BeNull();

        futuresItiSignalGeneratedEvent.FuturesItiSignal.Should().NotBeNull();
        futuresItiSignalGeneratedEvent.FuturesItiSignal!.ContractId.Should().Be(contractId);
        futuresItiSignalGeneratedEvent.FuturesItiSignal.ValueDate.Should().Be(valueDate);
        futuresItiSignalGeneratedEvent.FuturesItiSignal.TradeState.Should().Be(IntrinsicTimeTradeState.Hold);

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == FuturesItiSignalGeneratedEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesItiSignalGeneratedEvent>()!),
                _ when eventVerb == FuturesItiSignalGeneratedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesItiSignalGeneratedCompleteEvent>()!),
                _ when eventVerb == FuturesItiSignalGeneratedFailEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesItiSignalGeneratedFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is FuturesItiSignalGeneratedEvent generated)
                    futuresItiSignalGeneratedEvent = generated;
                if (@event is FuturesItiSignalGeneratedCompleteEvent generatedComplete)
                    futuresItiSignalGeneratedCompleteEvent = generatedComplete;
                if (@event is FuturesItiSignalGeneratedFailEvent generatedFail)
                    futuresItiSignalGeneratedFailEvent = generatedFail;
                return @event;
            }
        }
    }

    [Fact]
    public async Task ClearFuturesItiSignalHoldTrade_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        FuturesItiSignalGeneratedEvent futuresItiSignalGeneratedEvent = default!;
        FuturesItiSignalGeneratedCompleteEvent futuresItiSignalGeneratedCompleteEvent = default!;
        FuturesItiSignalGeneratedFailEvent futuresItiSignalGeneratedFailEvent = default!;

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, FuturesItiSignalGeneratedEvent.Actor)] = [FuturesItiSignalGeneratedEvent.Verb]
            },
            EventHandlerAsync
        );

        var contractId = SampleData.ContractId;
        var valueDate = SampleData.ValueDate;

        var entityId = SampleData.EntityId;
        var generateSubject = new ActorSubject(ActorType.Command, GenerateFuturesItiSignalCommand.Actor, GenerateFuturesItiSignalCommand.Verb, entityId.Format());
        var generateStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{generateSubject.ThreadId}");
        if (generateStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(generateStreamId);

        var setSubject = new ActorSubject(ActorType.Command, SetFuturesItiSignalHoldTradeCommand.Actor, SetFuturesItiSignalHoldTradeCommand.Verb, entityId.Format());
        var setStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{setSubject.ThreadId}");
        if (setStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(setStreamId);

        var clearSubject = new ActorSubject(ActorType.Command, ClearFuturesItiSignalHoldTradeCommand.Actor, ClearFuturesItiSignalHoldTradeCommand.Verb, entityId.Format());
        var clearStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{clearSubject.ThreadId}");
        if (clearStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(clearStreamId);

        await dbFixture.MarketDataDb.DeleteFuturesItiSignalAsync(contractId, valueDate, SampleData.TimePeriod);

        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var marketDataAnalyticsApi = new MarketDataAnalyticsCommandApi(commandServiceApi);

        var itiSignalId = new FuturesItiSignalId(contractId, valueDate, SampleData.TimePeriod, SampleData.Timestamp);

        await marketDataAnalyticsApi.GenerateFuturesItiSignalAsync(
            contractId, valueDate, SampleData.TimePeriod, SampleData.Timestamp,
            SampleData.FuturesPrice, 0);
        await Task.Delay(10);

        await marketDataAnalyticsApi.SetFuturesItiSignalHoldTradeAsync(itiSignalId);
        await Task.Delay(10);

        futuresItiSignalGeneratedEvent = default!;
        futuresItiSignalGeneratedCompleteEvent = default!;
        futuresItiSignalGeneratedFailEvent = default!;

        // act...
        var response = await marketDataAnalyticsApi.ClearFuturesItiSignalHoldTradeAsync(itiSignalId);

        await Task.Delay(10);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
        futuresItiSignalGeneratedEvent.Should().NotBeNull();
        futuresItiSignalGeneratedCompleteEvent.Should().NotBeNull();
        futuresItiSignalGeneratedFailEvent.Should().BeNull();

        futuresItiSignalGeneratedEvent.FuturesItiSignal.Should().NotBeNull();
        futuresItiSignalGeneratedEvent.FuturesItiSignal!.ContractId.Should().Be(contractId);
        futuresItiSignalGeneratedEvent.FuturesItiSignal.ValueDate.Should().Be(valueDate);
        futuresItiSignalGeneratedEvent.FuturesItiSignal.TradeState.Should().Be(IntrinsicTimeTradeState.Ready);

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == FuturesItiSignalGeneratedEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesItiSignalGeneratedEvent>()!),
                _ when eventVerb == FuturesItiSignalGeneratedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesItiSignalGeneratedCompleteEvent>()!),
                _ when eventVerb == FuturesItiSignalGeneratedFailEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesItiSignalGeneratedFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is FuturesItiSignalGeneratedEvent generated)
                    futuresItiSignalGeneratedEvent = generated;
                if (@event is FuturesItiSignalGeneratedCompleteEvent generatedComplete)
                    futuresItiSignalGeneratedCompleteEvent = generatedComplete;
                if (@event is FuturesItiSignalGeneratedFailEvent generatedFail)
                    futuresItiSignalGeneratedFailEvent = generatedFail;
                return @event;
            }
        }
    }
}
