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
        FuturesItiSignalGeneratedEvent futuresItiSignalGeneratedEvent = default!;
        FuturesItiSignalGeneratedCompleteEvent futuresItiSignalGeneratedCompleteEvent = default!;
        FuturesItiSignalGeneratedFailEvent futuresItiSignalGeneratedFailEvent = default!;
        var terminalEventReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

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

        futuresItiSignalGeneratedEvent = default!;
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
    public async Task ClearFuturesItiSignalHoldTrade_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        FuturesItiSignalGeneratedEvent futuresItiSignalGeneratedEvent = default!;
        FuturesItiSignalGeneratedCompleteEvent futuresItiSignalGeneratedCompleteEvent = default!;
        FuturesItiSignalGeneratedFailEvent futuresItiSignalGeneratedFailEvent = default!;
        var generatedEvents = new System.Collections.Concurrent.ConcurrentDictionary<Guid, FuturesItiSignalGeneratedEvent>();
        var terminalEventReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

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
        futuresItiSignalGeneratedEvent = await WaitForGeneratedAsync(setResponse.Value);

        setResponse.Success.Should().BeTrue();
        futuresItiSignalGeneratedEvent.FuturesItiSignal!.TradeState.Should().Be(IntrinsicTimeTradeState.Hold);

        futuresItiSignalGeneratedEvent = default!;
        futuresItiSignalGeneratedCompleteEvent = default!;
        futuresItiSignalGeneratedFailEvent = default!;
        terminalEventReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // act...
        var response = await marketDataAnalyticsApi.ClearFuturesItiSignalHoldTradeAsync(itiSignalId);

        await terminalEventReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));
        futuresItiSignalGeneratedEvent = await WaitForGeneratedAsync(response.Value);

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

        async Task<FuturesItiSignalGeneratedEvent> WaitForGeneratedAsync(Guid commandId)
        {
            var timeoutAt = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < timeoutAt)
            {
                if (generatedEvents.TryGetValue(commandId, out var generated))
                    return generated;
                await Task.Delay(20);
            }
            throw new TimeoutException($"No FuturesItiSignalGeneratedEvent arrived for command {commandId}.");
        }

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
                {
                    futuresItiSignalGeneratedEvent = generated;
                    generatedEvents[generated.CommandId] = generated;
                }
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
}
