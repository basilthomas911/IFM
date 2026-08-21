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
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests.FuturesItiSignal;

[Trait("Category", "Integration")]
[Collection(ItiPipelineIntegrationCollection.Name)]
public sealed class FuturesItiSignalDailyPipelineIntegrationTests(
    WebApplicationFactory<Program> factory,
    MarketDataAnalyticsFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>,
      IClassFixture<MarketDataAnalyticsFixture>
{
    readonly IActorProducer _actorProducer =
        factory.Services.GetRequiredService<IActorProducer>();

    [Fact]
    public async Task DailyCommand_ProjectsOnlyDailySignalWithoutLongerPeriodFanout()
    {
        var contractId = SampleData.ContractId;
        var valueDate = SampleData.ValueDate;
        TimeFrameType[] expectedPeriods =
        [
            TimeFrameType.Daily,
            TimeFrameType.Weekly,
            TimeFrameType.Monthly
        ];

        foreach (var period in expectedPeriods)
        {
            var entityId = new FuturesItiSignalEntityId(contractId, valueDate, period);
            var subject = new ActorSubject(
                ActorType.Command,
                GenerateFuturesItiSignalCommand.Actor,
                GenerateFuturesItiSignalCommand.Verb,
                entityId.Format());
            var streamId = await dbFixture.ActorEventSourceDb
                .GetEventStreamIdAsync($"{subject.ThreadId}");
            if (streamId > 0)
                await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(streamId);
            await dbFixture.MarketDataDb.DeleteFuturesItiSignalAsync(
                contractId,
                valueDate,
                period);
        }

        var generated = new ConcurrentDictionary<TimeFrameType, FuturesItiSignalGeneratedEvent>();
        var completed = new ConcurrentDictionary<TimeFrameType, FuturesItiSignalGeneratedCompleteEvent>();
        var notifications = new ConcurrentDictionary<TimeFrameType, FuturesItiSignalUpdatedNotifyEvent>();
        var pipelineCompleted = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var listener = new NatsActorEventListener(
            new NatsEventListenerOptions(),
            Substitute.For<ILogger<NatsActorEventListener>>());

        try
        {
            await listener.StartAsync(
                $"iti-daily-pipeline-{Guid.NewGuid():N}",
                new Dictionary<ActorMailboxId, List<string>>
                {
                    [new ActorMailboxId(ActorType.Event, FuturesItiSignalGeneratedEvent.Actor)] =
                    [
                        FuturesItiSignalGeneratedEvent.Verb,
                        FuturesItiSignalGeneratedCompleteEvent.Verb,
                        FuturesItiSignalGeneratedFailEvent.Verb
                    ],
                    [new ActorMailboxId(ActorType.Notify, FuturesItiSignalUpdatedNotifyEvent.Actor)] =
                    [FuturesItiSignalUpdatedNotifyEvent.Verb]
                },
                HandleEventAsync);

            var api = new MarketDataAnalyticsCommandApi(_actorProducer);
            var response = await api.GenerateFuturesItiSignalAsync(
                contractId,
                valueDate,
                TimeFrameType.Daily,
                SampleData.Timestamp,
                SampleData.FuturesPrice,
                SampleData.VixFuturesPrice);

            response.Success.Should().BeTrue();
            await pipelineCompleted.Task.WaitAsync(TimeSpan.FromSeconds(20));
            await Task.Delay(500);

            generated.Keys.Should().Equal(TimeFrameType.Daily);
            completed.Keys.Should().Equal(TimeFrameType.Daily);
            notifications.Keys.Should().Equal(TimeFrameType.Daily);
            generated[TimeFrameType.Daily].VixFuturesPrice.Should().Be(SampleData.VixFuturesPrice);
            completed[TimeFrameType.Daily].VixFuturesPrice.Should().Be(SampleData.VixFuturesPrice);
            notifications[TimeFrameType.Daily].FuturesItiSignal.Should()
                .BeEquivalentTo(completed[TimeFrameType.Daily].FuturesItiSignal);
            notifications[TimeFrameType.Daily].SourceEventId.Should()
                .Be(completed[TimeFrameType.Daily].Id);
            generated[TimeFrameType.Daily].FuturesItiSignal!.TradingDays.Should().Be(1);

            foreach (var period in expectedPeriods)
            {
                var entityId = new FuturesItiSignalEntityId(contractId, valueDate, period);
                var signals = await dbFixture.MarketDataDb.GetFuturesItiSignalsAsync(entityId);
                if (period == TimeFrameType.Daily)
                    signals.Should().ContainSingle();
                else
                    signals.Should().BeEmpty("longer periods are independent realtime evaluators");
            }
        }
        finally
        {
            await listener.StopAsync();
        }

        ValueTask HandleEventAsync(string eventVerb, NatsMsg<byte[]> message)
        {
            if (eventVerb == FuturesItiSignalGeneratedEvent.Verb)
            {
                var @event = message.AsEvent<FuturesItiSignalGeneratedEvent>()!;
                if (Matches(@event.EntityId))
                    generated[@event.EntityId.TimePeriod] = @event;
            }
            else if (eventVerb == FuturesItiSignalGeneratedCompleteEvent.Verb)
            {
                var @event = message.AsEvent<FuturesItiSignalGeneratedCompleteEvent>()!;
                if (Matches(@event.EntityId))
                {
                    completed[@event.EntityId.TimePeriod] = @event;
                    TryComplete();
                }
            }
            else if (eventVerb == FuturesItiSignalUpdatedNotifyEvent.Verb)
            {
                var @event = message.AsEvent<FuturesItiSignalUpdatedNotifyEvent>()!;
                if (Matches(@event.EntityId))
                {
                    notifications[@event.EntityId.TimePeriod] = @event;
                    TryComplete();
                }
            }
            else if (eventVerb == FuturesItiSignalGeneratedFailEvent.Verb)
            {
                var @event = message.AsEvent<FuturesItiSignalGeneratedFailEvent>()!;
                if (Matches(@event.EntityId))
                    pipelineCompleted.TrySetException(new InvalidOperationException(@event.ErrorMessage));
            }

            return ValueTask.CompletedTask;

            void TryComplete()
            {
                if (completed.ContainsKey(TimeFrameType.Daily)
                    && notifications.ContainsKey(TimeFrameType.Daily))
                {
                    pipelineCompleted.TrySetResult(true);
                }
            }
        }

        bool Matches(FuturesItiSignalEntityId entityId) =>
            StringComparer.Ordinal.Equals(entityId.ContractId, contractId)
            && entityId.ValueDate == valueDate;
    }
}
