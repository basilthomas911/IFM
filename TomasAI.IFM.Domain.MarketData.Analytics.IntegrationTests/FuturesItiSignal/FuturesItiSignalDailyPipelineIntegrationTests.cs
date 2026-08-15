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
public sealed class FuturesItiSignalDailyPipelineIntegrationTests(
    WebApplicationFactory<Program> factory,
    MarketDataAnalyticsFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>,
      IClassFixture<MarketDataAnalyticsFixture>
{
    readonly IActorProducer _actorProducer =
        factory.Services.GetRequiredService<IActorProducer>();

    [Fact]
    public async Task DailyCommand_ProjectsAndDurablyDerivesWeeklyAndMonthlySignals()
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
                    ]
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

            generated.Keys.Should().BeEquivalentTo(expectedPeriods);
            completed.Keys.Should().BeEquivalentTo(expectedPeriods);
            foreach (var period in expectedPeriods)
            {
                generated[period].VixFuturesPrice.Should().Be(SampleData.VixFuturesPrice);
                completed[period].VixFuturesPrice.Should().Be(SampleData.VixFuturesPrice);
                generated[period].FuturesItiSignal!.TradingDays.Should().Be(period switch
                {
                    TimeFrameType.Daily => 1,
                    TimeFrameType.Weekly => 5,
                    TimeFrameType.Monthly => 20,
                    _ => throw new ArgumentOutOfRangeException(nameof(period))
                });

                var entityId = new FuturesItiSignalEntityId(contractId, valueDate, period);
                var signals = await dbFixture.MarketDataDb.GetFuturesItiSignalsAsync(entityId);
                signals.Should().ContainSingle();
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
                    if (completed.Count == expectedPeriods.Length)
                        pipelineCompleted.TrySetResult(true);
                }
            }
            else if (eventVerb == FuturesItiSignalGeneratedFailEvent.Verb)
            {
                var @event = message.AsEvent<FuturesItiSignalGeneratedFailEvent>()!;
                if (Matches(@event.EntityId))
                    pipelineCompleted.TrySetException(new InvalidOperationException(@event.ErrorMessage));
            }

            return ValueTask.CompletedTask;
        }

        bool Matches(FuturesItiSignalEntityId entityId) =>
            StringComparer.Ordinal.Equals(entityId.ContractId, contractId)
            && entityId.ValueDate == valueDate;
    }
}
