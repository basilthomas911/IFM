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
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests.FuturesTdiSignal;

public class FuturesTdiSignalCommandApiTests(WebApplicationFactory<Program> factory, MarketDataAnalyticsFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<MarketDataAnalyticsFixture>
{
    readonly IActorProducer _actorProducer = factory.Services.GetRequiredService<IActorProducer>();
    readonly ILogger<NatsActorEventListener> _logger = Substitute.For<ILogger<NatsActorEventListener>>();

    [Fact]
    public async Task GenerateFuturesTdiSignal_Ok()
    {
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        var contractId = $"ESTD{Guid.NewGuid():N}"[..18];
        var valueDate = new DateOnly(2099, 12, 31);
        var timestamp = new TimeOnly(10, 0, 0);
        var futuresTdiSignalId = new FuturesTdiSignalId(contractId, valueDate, timestamp);
        var entityId = new FuturesTdiSignalEntityId(
            contractId,
            valueDate,
            TimeFrameType.OneMinute,
            FuturesTdiConfiguration.StandardConfigurationId);
        var generatedCompletion = new TaskCompletionSource<FuturesTdiSignalGeneratedEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var projectionCompletion = new TaskCompletionSource<FuturesTdiSignalGeneratedCompleteEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        await eventListener.StartAsync(
            $"futures-tdi-command-{Guid.NewGuid():N}",
            new()
            {
                [new ActorMailboxId(ActorType.Event, FuturesTdiSignalGeneratedEvent.Actor)] =
                [
                    FuturesTdiSignalGeneratedEvent.Verb,
                    FuturesTdiSignalGeneratedCompleteEvent.Verb,
                    FuturesTdiSignalGeneratedFailEvent.Verb
                ]
            },
            EventHandlerAsync);

        try
        {
            var subject = new ActorSubject(
                ActorType.Command,
                GenerateFuturesTdiSignalCommand.Actor,
                GenerateFuturesTdiSignalCommand.Verb,
                entityId.Format());
            var eventStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{subject.ThreadId}");
            if (eventStreamId > 0)
                await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(eventStreamId);

            var analyticsApi = new MarketDataAnalyticsCommandApi(_actorProducer);
            var response = await analyticsApi.GenerateFuturesTdiSignalAsync(
                futuresTdiSignalId,
                CreateRsiSignals(contractId, valueDate));

            response.Should().NotBeNull();
            response.Success.Should().BeTrue(response.ErrorMessage);
            response.Value.Should().NotBe(Guid.Empty);

            var futuresTdiSignalGeneratedEvent = await generatedCompletion.Task.WaitAsync(
                TimeSpan.FromSeconds(10));
            var futuresTdiSignalGeneratedCompleteEvent = await projectionCompletion.Task.WaitAsync(
                TimeSpan.FromSeconds(10));

            futuresTdiSignalGeneratedEvent.CommandId.Should().Be(response.Value);
            futuresTdiSignalGeneratedCompleteEvent.CommandId.Should().Be(response.Value);
            futuresTdiSignalGeneratedEvent.FuturesTdiSignal.Should().NotBeNull();
            futuresTdiSignalGeneratedEvent.FuturesTdiSignal.ContractId.Should().Be(contractId);
            futuresTdiSignalGeneratedEvent.FuturesTdiSignal.ValueDate.Should().Be(valueDate);
            futuresTdiSignalGeneratedEvent.FuturesTdiSignal.TimePeriod.Should().Be(TimeFrameType.OneMinute);
            futuresTdiSignalGeneratedEvent.FuturesTdiSignal.Timestamp.Should().Be(timestamp);

            var lastSignal = await dbFixture.MarketDataDb.GetLastFuturesTdiSignalAsync(contractId, valueDate);
            lastSignal.Should().NotBeNull();
            lastSignal!.ContractId.Should().Be(contractId);
            lastSignal.ValueDate.Should().Be(valueDate);
            lastSignal.Timestamp.Should().Be(timestamp);
        }
        finally
        {
            await eventListener.StopAsync();
        }

        ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            switch (eventVerb)
            {
                case FuturesTdiSignalGeneratedEvent.Verb:
                    var generated = eventMsg.AsEvent<FuturesTdiSignalGeneratedEvent>();
                    if (generated?.EntityId == entityId)
                        generatedCompletion.TrySetResult(generated);
                    break;
                case FuturesTdiSignalGeneratedCompleteEvent.Verb:
                    var completed = eventMsg.AsEvent<FuturesTdiSignalGeneratedCompleteEvent>();
                    if (completed?.EntityId == entityId)
                        projectionCompletion.TrySetResult(completed);
                    break;
                case FuturesTdiSignalGeneratedFailEvent.Verb:
                    var failed = eventMsg.AsEvent<FuturesTdiSignalGeneratedFailEvent>();
                    if (failed?.EntityId == entityId)
                    {
                        var exception = new InvalidOperationException(failed.ErrorMessage);
                        generatedCompletion.TrySetException(exception);
                        projectionCompletion.TrySetException(exception);
                    }
                    break;
            }
            return ValueTask.CompletedTask;
        }
    }

    static FuturesRsiSignalReadModel[] CreateRsiSignals(string contractId, DateOnly valueDate)
        => Enumerable.Range(0, FuturesTdiConfiguration.Standard.RequiredRsiSamples)
            .Select(index => new FuturesRsiSignalReadModel(
                contractId,
                valueDate,
                TimeFrameType.OneMinute,
                FuturesTdiConfiguration.Standard.RsiPeriod,
                new TimeOnly(9, 27).AddMinutes(index),
                5500m + index,
                1m,
                1m,
                0m,
                1m,
                0.5m,
                2d,
                40d + index,
                0d,
                1d,
                index + 1,
                valueDate.ToDateTime(new TimeOnly(9, 27)).AddMinutes(index)))
            .ToArray();
}
