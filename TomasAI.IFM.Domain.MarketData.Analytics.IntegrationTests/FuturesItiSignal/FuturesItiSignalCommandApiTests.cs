using System.Diagnostics;
using FluentAssertions;
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
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;

namespace TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests.FuturesItiSignal;

public class FuturesItiSignalCommandApiTests(WebApplicationFactory<Program> factory, MarketDataAnalyticsFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<MarketDataAnalyticsFixture>
{
    readonly IActorProducer _actorProducer = factory.Services.GetRequiredService<IActorProducer>();
    readonly ILogger<NatsActorEventListener> _logger = Substitute.For<ILogger<NatsActorEventListener>>();

    [Fact]
    public async Task GenerateFuturesItiSignal_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        FuturesItiSignalGeneratedEvent futuresItiSignalGeneratedEvent = default!;
        FuturesItiSignalGeneratedCompleteEvent futuresItiSignalGeneratedCompleteEvent = default!;
        FuturesItiSignalGeneratedFailEvent futuresItiSignalGeneratedFailEvent = default!;
        var terminalEventReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

       
        var contractId = SampleData.ContractId;
        var valueDate = SampleData.ValueDate;

        var entityId = SampleData.EntityId;
        var subject = new ActorSubject(ActorType.Command, GenerateFuturesItiSignalCommand.Actor, GenerateFuturesItiSignalCommand.Verb, entityId.Format());
        var eventStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{subject.ThreadId}");
        if (eventStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(eventStreamId);
        await dbFixture.MarketDataDb.DeleteFuturesItiSignalAsync(contractId, valueDate, SampleData.TimePeriod);

        // act...

        await eventListener.StartAsync(
           "TestEventListener",
           new()
           {
               [new ActorMailboxId(ActorType.Event, FuturesItiSignalGeneratedEvent.Actor)] =
               [
                   FuturesItiSignalGeneratedEvent.Verb,
                   FuturesItiSignalGeneratedCompleteEvent.Verb,
                   FuturesItiSignalGeneratedFailEvent.Verb
               ]
           },
           EventHandlerAsync
       );

        var marketDataAnalyticsApi = new MarketDataAnalyticsCommandApi(_actorProducer);

        var sw = Stopwatch.StartNew();
        var response = await marketDataAnalyticsApi.GenerateFuturesItiSignalAsync(
            contractId, valueDate, SampleData.TimePeriod, SampleData.Timestamp, SampleData.FuturesPrice, SampleData.VixFuturesPrice);

        await terminalEventReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));

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
                {
                    futuresItiSignalGeneratedCompleteEvent = generatedComplete;
                    terminalEventReceived.TrySetResult();
                }
                if (@event is FuturesItiSignalGeneratedFailEvent generatedFail)
                {
                    futuresItiSignalGeneratedFailEvent = generatedFail;
                    terminalEventReceived.TrySetResult();
                }
                return @event;
            }
        }
    }

    [Fact]
    public async Task SetFuturesItiSignalHoldTrade_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        FuturesItiSignalGeneratedCompleteEvent futuresItiSignalGeneratedCompleteEvent = default!;
        FuturesItiSignalGeneratedFailEvent futuresItiSignalGeneratedFailEvent = default!;
        FuturesItiSignalHoldTradeSetEvent holdTradeSetEvent = default!;
        FuturesItiSignalHoldTradeSetCompleteEvent holdTradeSetCompleteEvent = default!;
        FuturesItiSignalHoldTradeSetFailEvent holdTradeSetFailEvent = default!;
        var terminalEventReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, FuturesItiSignalGeneratedEvent.Actor)] =
                [
                    FuturesItiSignalGeneratedEvent.Verb,
                    FuturesItiSignalGeneratedCompleteEvent.Verb,
                    FuturesItiSignalGeneratedFailEvent.Verb,
                    FuturesItiSignalHoldTradeSetEvent.Verb,
                    FuturesItiSignalHoldTradeSetCompleteEvent.Verb,
                    FuturesItiSignalHoldTradeSetFailEvent.Verb
                ]
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

        var marketDataAnalyticsApi = new MarketDataAnalyticsCommandApi(_actorProducer);

        var generateResponse = await marketDataAnalyticsApi.GenerateFuturesItiSignalAsync(
            contractId, valueDate, SampleData.TimePeriod, SampleData.Timestamp,
            SampleData.FuturesPrice, SampleData.VixFuturesPrice);
        await terminalEventReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));
        generateResponse.Success.Should().BeTrue();
        futuresItiSignalGeneratedCompleteEvent.Should().NotBeNull();
        futuresItiSignalGeneratedFailEvent.Should().BeNull();

        futuresItiSignalGeneratedCompleteEvent = default!;
        futuresItiSignalGeneratedFailEvent = default!;
        terminalEventReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // act...
        var itiSignalId = new FuturesItiSignalId(contractId, valueDate, SampleData.TimePeriod, SampleData.Timestamp);
        var response = await marketDataAnalyticsApi.SetFuturesItiSignalHoldTradeAsync(itiSignalId);

        await terminalEventReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
        holdTradeSetEvent.Should().NotBeNull();
        holdTradeSetCompleteEvent.Should().NotBeNull();
        holdTradeSetFailEvent.Should().BeNull();
        holdTradeSetEvent.FuturesItiSignal.Should().NotBeNull();
        holdTradeSetEvent.FuturesItiSignal!.ContractId.Should().Be(contractId);
        holdTradeSetEvent.FuturesItiSignal.ValueDate.Should().Be(valueDate);
        holdTradeSetEvent.FuturesItiSignal.TradeState.Should().Be(IntrinsicTimeTradeState.Hold);
        holdTradeSetCompleteEvent.FuturesItiSignal.Should().BeEquivalentTo(holdTradeSetEvent.FuturesItiSignal);

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == FuturesItiSignalGeneratedEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesItiSignalGeneratedEvent>()!),
                _ when eventVerb == FuturesItiSignalGeneratedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesItiSignalGeneratedCompleteEvent>()!),
                _ when eventVerb == FuturesItiSignalGeneratedFailEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesItiSignalGeneratedFailEvent>()!),
                _ when eventVerb == FuturesItiSignalHoldTradeSetEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesItiSignalHoldTradeSetEvent>()!),
                _ when eventVerb == FuturesItiSignalHoldTradeSetCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesItiSignalHoldTradeSetCompleteEvent>()!),
                _ when eventVerb == FuturesItiSignalHoldTradeSetFailEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesItiSignalHoldTradeSetFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is FuturesItiSignalGeneratedCompleteEvent generatedComplete)
                {
                    futuresItiSignalGeneratedCompleteEvent = generatedComplete;
                    terminalEventReceived.TrySetResult();
                }
                if (@event is FuturesItiSignalGeneratedFailEvent generatedFail)
                {
                    futuresItiSignalGeneratedFailEvent = generatedFail;
                    terminalEventReceived.TrySetResult();
                }
                if (@event is FuturesItiSignalHoldTradeSetEvent set)
                    holdTradeSetEvent = set;
                if (@event is FuturesItiSignalHoldTradeSetCompleteEvent setComplete)
                {
                    holdTradeSetCompleteEvent = setComplete;
                    terminalEventReceived.TrySetResult();
                }
                if (@event is FuturesItiSignalHoldTradeSetFailEvent setFail)
                {
                    holdTradeSetFailEvent = setFail;
                    terminalEventReceived.TrySetResult();
                }
                return @event;
            }
        }
    }

    [Fact]
    public async Task ClearFuturesItiSignalHoldTrade_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        FuturesItiSignalGeneratedCompleteEvent futuresItiSignalGeneratedCompleteEvent = default!;
        FuturesItiSignalGeneratedFailEvent futuresItiSignalGeneratedFailEvent = default!;
        FuturesItiSignalHoldTradeSetEvent holdTradeSetEvent = default!;
        FuturesItiSignalHoldTradeSetCompleteEvent holdTradeSetCompleteEvent = default!;
        FuturesItiSignalHoldTradeSetFailEvent holdTradeSetFailEvent = default!;
        FuturesItiSignalHoldTradeClearedEvent holdTradeClearedEvent = default!;
        FuturesItiSignalHoldTradeClearedCompleteEvent holdTradeClearedCompleteEvent = default!;
        FuturesItiSignalHoldTradeClearedFailEvent holdTradeClearedFailEvent = default!;
        var terminalEventReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, FuturesItiSignalGeneratedEvent.Actor)] =
                [
                    FuturesItiSignalGeneratedEvent.Verb,
                    FuturesItiSignalGeneratedCompleteEvent.Verb,
                    FuturesItiSignalGeneratedFailEvent.Verb,
                    FuturesItiSignalHoldTradeSetEvent.Verb,
                    FuturesItiSignalHoldTradeSetCompleteEvent.Verb,
                    FuturesItiSignalHoldTradeSetFailEvent.Verb,
                    FuturesItiSignalHoldTradeClearedEvent.Verb,
                    FuturesItiSignalHoldTradeClearedCompleteEvent.Verb,
                    FuturesItiSignalHoldTradeClearedFailEvent.Verb
                ]
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

        var marketDataAnalyticsApi = new MarketDataAnalyticsCommandApi(_actorProducer);

        var itiSignalId = new FuturesItiSignalId(contractId, valueDate, SampleData.TimePeriod, SampleData.Timestamp);

        var generateResponse = await marketDataAnalyticsApi.GenerateFuturesItiSignalAsync(
            contractId, valueDate, SampleData.TimePeriod, SampleData.Timestamp,
            SampleData.FuturesPrice, SampleData.VixFuturesPrice);
        await terminalEventReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));

        generateResponse.Success.Should().BeTrue();
        futuresItiSignalGeneratedCompleteEvent.Should().NotBeNull();
        futuresItiSignalGeneratedFailEvent.Should().BeNull();
        terminalEventReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var setResponse = await marketDataAnalyticsApi.SetFuturesItiSignalHoldTradeAsync(itiSignalId);
        await terminalEventReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));

        setResponse.Success.Should().BeTrue();
        holdTradeSetEvent.Should().NotBeNull();
        holdTradeSetCompleteEvent.Should().NotBeNull();
        holdTradeSetFailEvent.Should().BeNull();
        holdTradeSetEvent.FuturesItiSignal!.TradeState.Should().Be(IntrinsicTimeTradeState.Hold);

        terminalEventReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // act...
        var response = await marketDataAnalyticsApi.ClearFuturesItiSignalHoldTradeAsync(itiSignalId);

        await terminalEventReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
        holdTradeClearedEvent.Should().NotBeNull();
        holdTradeClearedCompleteEvent.Should().NotBeNull();
        holdTradeClearedFailEvent.Should().BeNull();
        holdTradeClearedEvent.FuturesItiSignal.Should().NotBeNull();
        holdTradeClearedEvent.FuturesItiSignal!.ContractId.Should().Be(contractId);
        holdTradeClearedEvent.FuturesItiSignal.ValueDate.Should().Be(valueDate);
        holdTradeClearedEvent.FuturesItiSignal.TradeState.Should().Be(IntrinsicTimeTradeState.Ready);
        holdTradeClearedCompleteEvent.FuturesItiSignal.Should().BeEquivalentTo(holdTradeClearedEvent.FuturesItiSignal);

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == FuturesItiSignalGeneratedEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesItiSignalGeneratedEvent>()!),
                _ when eventVerb == FuturesItiSignalGeneratedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesItiSignalGeneratedCompleteEvent>()!),
                _ when eventVerb == FuturesItiSignalGeneratedFailEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesItiSignalGeneratedFailEvent>()!),
                _ when eventVerb == FuturesItiSignalHoldTradeSetEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesItiSignalHoldTradeSetEvent>()!),
                _ when eventVerb == FuturesItiSignalHoldTradeSetCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesItiSignalHoldTradeSetCompleteEvent>()!),
                _ when eventVerb == FuturesItiSignalHoldTradeSetFailEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesItiSignalHoldTradeSetFailEvent>()!),
                _ when eventVerb == FuturesItiSignalHoldTradeClearedEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesItiSignalHoldTradeClearedEvent>()!),
                _ when eventVerb == FuturesItiSignalHoldTradeClearedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesItiSignalHoldTradeClearedCompleteEvent>()!),
                _ when eventVerb == FuturesItiSignalHoldTradeClearedFailEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesItiSignalHoldTradeClearedFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is FuturesItiSignalGeneratedCompleteEvent generatedComplete)
                {
                    futuresItiSignalGeneratedCompleteEvent = generatedComplete;
                    terminalEventReceived.TrySetResult();
                }
                if (@event is FuturesItiSignalGeneratedFailEvent generatedFail)
                {
                    futuresItiSignalGeneratedFailEvent = generatedFail;
                    terminalEventReceived.TrySetResult();
                }
                if (@event is FuturesItiSignalHoldTradeSetEvent set)
                    holdTradeSetEvent = set;
                if (@event is FuturesItiSignalHoldTradeSetCompleteEvent setComplete)
                {
                    holdTradeSetCompleteEvent = setComplete;
                    terminalEventReceived.TrySetResult();
                }
                if (@event is FuturesItiSignalHoldTradeSetFailEvent setFail)
                {
                    holdTradeSetFailEvent = setFail;
                    terminalEventReceived.TrySetResult();
                }
                if (@event is FuturesItiSignalHoldTradeClearedEvent cleared)
                    holdTradeClearedEvent = cleared;
                if (@event is FuturesItiSignalHoldTradeClearedCompleteEvent clearedComplete)
                {
                    holdTradeClearedCompleteEvent = clearedComplete;
                    terminalEventReceived.TrySetResult();
                }
                if (@event is FuturesItiSignalHoldTradeClearedFailEvent clearedFail)
                {
                    holdTradeClearedFailEvent = clearedFail;
                    terminalEventReceived.TrySetResult();
                }
                return @event;
            }
        }
    }
}
