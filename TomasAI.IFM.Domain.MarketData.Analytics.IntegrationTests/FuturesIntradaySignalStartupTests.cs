using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Event.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Event.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Event.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Event.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests;

public class FuturesIntradaySignalStartupTests(
    WebApplicationFactory<Program> factory,
    MarketDataAnalyticsFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<MarketDataAnalyticsFixture>
{
    readonly IActorProducer _actorProducer = factory.Services.GetRequiredService<IActorProducer>();
    readonly ILogger<NatsActorEventListener> _logger = Substitute.For<ILogger<NatsActorEventListener>>();

    [Fact]
    public async Task AllConfiguredIntradaySignalActors_EmitStartedEvents()
    {
        var contractId = $"ESAUT{Guid.NewGuid():N}"[..13];
        var valueDate = new DateOnly(2026, 8, 14);
        var profile = FuturesIntradaySignalActivationProfile.Create(contractId, valueDate);
        var expected = profile.SelectMany(activation => new[]
        {
            $"RSI:{activation.Rsi.Format()}",
            $"ATR:{activation.Atr.Format()}",
            $"ADX:{activation.Adx.Format()}",
            $"MACD:{activation.Macd.Format()}"
        }).ToHashSet(StringComparer.Ordinal);
        var observed = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
        var allStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var listener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        var api = new MarketDataAnalyticsCommandApi(_actorProducer);

        await DeleteEventStreamsAsync(profile);
        await listener.StartAsync(
            $"IntradaySignalStartup-{Guid.NewGuid():N}",
            new()
            {
                [new ActorMailboxId(ActorType.Event, FuturesRsiSignalStartedEvent.Actor)] =
                    [FuturesRsiSignalStartedEvent.Verb],
                [new ActorMailboxId(ActorType.Event, FuturesAtrSignalStartedEvent.Actor)] =
                    [FuturesAtrSignalStartedEvent.Verb],
                [new ActorMailboxId(ActorType.Event, FuturesAdxSignalStartedEvent.Actor)] =
                    [FuturesAdxSignalStartedEvent.Verb],
                [new ActorMailboxId(ActorType.Event, FuturesMacdSignalStartedEvent.Actor)] =
                    [FuturesMacdSignalStartedEvent.Verb]
            },
            HandleStartedEventAsync);

        try
        {
            var responses = await Task.WhenAll(profile.SelectMany(activation => new[]
            {
                api.StartFuturesRsiSignalAsync(activation.Rsi),
                api.StartFuturesAtrSignalAsync(activation.Atr),
                api.StartFuturesAdxSignalAsync(activation.Adx),
                api.StartFuturesMacdSignalAsync(activation.Macd)
            }));

            responses.Should().HaveCount(24);
            responses.Should().OnlyContain(response => response.Success);
            await allStarted.Task.WaitAsync(TimeSpan.FromSeconds(30));
            observed.Keys.Should().BeEquivalentTo(expected);
        }
        finally
        {
            await Task.WhenAll(profile.SelectMany(activation => new[]
            {
                api.StopFuturesRsiSignalAsync(activation.Rsi),
                api.StopFuturesAtrSignalAsync(activation.Atr),
                api.StopFuturesAdxSignalAsync(activation.Adx),
                api.StopFuturesMacdSignalAsync(activation.Macd)
            }));
            await listener.StopAsync();
            await FuturesRsiSignalTimer.StopAllAsync();
            await FuturesAtrSignalTimer.StopAllAsync();
            await FuturesAdxSignalTimer.StopAllAsync();
            await FuturesMacdSignalTimer.StopAllAsync();
            await DeleteEventStreamsAsync(profile);
        }

        ValueTask HandleStartedEventAsync(string _, NatsMsg<byte[]> message)
        {
            var subject = message.Subject.ToSubject();
            var key = subject.Name switch
            {
                FuturesRsiSignalStartedEvent.Actor =>
                    $"RSI:{message.AsEvent<FuturesRsiSignalStartedEvent>()!.EntityId.Format()}",
                FuturesAtrSignalStartedEvent.Actor =>
                    $"ATR:{message.AsEvent<FuturesAtrSignalStartedEvent>()!.EntityId.Format()}",
                FuturesAdxSignalStartedEvent.Actor =>
                    $"ADX:{message.AsEvent<FuturesAdxSignalStartedEvent>()!.EntityId.Format()}",
                FuturesMacdSignalStartedEvent.Actor =>
                    $"MACD:{message.AsEvent<FuturesMacdSignalStartedEvent>()!.EntityId.Format()}",
                _ => string.Empty
            };

            if (expected.Contains(key) && observed.TryAdd(key, 0) && observed.Count == expected.Count)
                allStarted.TrySetResult();
            return ValueTask.CompletedTask;
        }
    }

    async Task DeleteEventStreamsAsync(IReadOnlyList<FuturesIntradaySignalActivation> profile)
    {
        foreach (var activation in profile)
        {
            await DeleteEventStreamAsync(StartFuturesRsiSignalCommand.Actor, activation.Rsi.Format());
            await DeleteEventStreamAsync(StartFuturesAtrSignalCommand.Actor, activation.Atr.Format());
            await DeleteEventStreamAsync(StartFuturesAdxSignalCommand.Actor, activation.Adx.Format());
            await DeleteEventStreamAsync(StartFuturesMacdSignalCommand.Actor, activation.Macd.Format());
        }
    }

    async Task DeleteEventStreamAsync(string actor, string entityId)
    {
        var subject = new ActorSubject(ActorType.Command, actor, "Start", entityId);
        var eventStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{subject.ThreadId}");
        if (eventStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(eventStreamId);
    }
}
