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
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests.MarketOutlookSnapshot;

/// <summary>Verifies the command-owned Market Outlook state and projection pipeline.</summary>
[Trait("Category", "Integration")]
public sealed class MarketOutlookSnapshotCommandPipelineIntegrationTests(
    WebApplicationFactory<Program> factory,
    MarketDataAnalyticsFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<MarketDataAnalyticsFixture>
{
    readonly IActorProducer _producer = factory.Services.GetRequiredService<IActorProducer>();

    /// <summary>Commits a component and EOD checkpoint, projects both models, and publishes one UI notification.</summary>
    [Fact]
    public async Task ObserveThenPublish_ProjectsWorkingStateSnapshotAndNotification()
    {
        var contractId = $"ESMO{Guid.NewGuid():N}"[..18];
        var valueDate = new DateOnly(2099, 12, 30);
        var entityId = new MarketOutlookEntityId(contractId, valueDate);
        var observedAt = DateTime.UtcNow;
        var observeId = Guid.NewGuid();
        var publishId = Guid.NewGuid();
        var terminal = new TaskCompletionSource<MarketOutlookUpdatedNotifyEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var listener = new NatsActorEventListener(
            new NatsEventListenerOptions(),
            Substitute.For<ILogger<NatsActorEventListener>>());
        await listener.StartAsync(
            $"market-outlook-{Guid.NewGuid():N}",
            new()
            {
                [new ActorMailboxId(ActorType.Notify, MarketOutlookUpdatedNotifyEvent.Actor)] =
                [
                    MarketOutlookUpdatedNotifyEvent.Verb
                ]
            },
            OnNotificationAsync);

        try
        {
            var rsi = SampleData.CreateRsiSignalsForAtr()[0] with
            {
                ContractId = contractId,
                ValueDate = valueDate,
                PeriodLength = FuturesIntradaySignalActivationProfile.RsiPeriodLength
            };
            var observe = new ObserveMarketOutlookComponentCommand(
                entityId,
                observeId,
                1,
                observedAt,
                "integration-rsi",
                futuresRsiSignal: rsi)
            {
                CommandId = observeId,
                Subject = CommandSubject(ObserveMarketOutlookComponentCommand.Verb, entityId)
            };
            var observeResult = await _producer.RequestAsync<
                ObserveMarketOutlookComponentCommand,
                MarketOutlookEntityId,
                GuidResult>(observe.Subject, observe, entityId);
            observeResult.Success.Should().BeTrue(observeResult.ErrorMessage);

            var eod = SampleData.FuturesEodData with
            {
                ContractId = contractId,
                ValueDate = valueDate,
                Symbol = "ES"
            };
            var publish = new PublishMarketOutlookSnapshotCommand(
                entityId,
                publishId,
                2,
                observedAt.AddMinutes(1),
                eod,
                futuresRsiSignal: rsi)
            {
                CommandId = publishId,
                Subject = CommandSubject(PublishMarketOutlookSnapshotCommand.Verb, entityId)
            };
            var publishResult = await _producer.RequestAsync<
                PublishMarketOutlookSnapshotCommand,
                MarketOutlookEntityId,
                GuidResult>(publish.Subject, publish, entityId);
            publishResult.Success.Should().BeTrue(publishResult.ErrorMessage);

            var notification = await terminal.Task.WaitAsync(TimeSpan.FromSeconds(20));
            notification.CommandId.Should().Be(publishId);
            notification.EntityId.Should().Be(entityId);
            notification.MarketOutlook.Revision.Should().Be(1);
            notification.MarketOutlook.MissingInputs.Should().Contain("TDI");

            var workingState = await dbFixture.MarketDataDb.GetMarketOutlookWorkingStateAsync(
                contractId,
                valueDate);
            var snapshot = await dbFixture.MarketDataDb.GetMarketOutlookSnapshotAsync(
                contractId,
                valueDate);
            workingState.Should().NotBeNull();
            workingState!.Revision.Should().Be(2);
            workingState.Status.Should().Be(MarketOutlookStateStatus.Published);
            workingState.FuturesRsiSignal.Should().BeEquivalentTo(rsi);
            snapshot.Should().BeEquivalentTo(
                notification.MarketOutlook,
                options => options.Excluding(value => value.UpdatedOn));
            snapshot!.UpdatedOn.Should().BeCloseTo(
                notification.MarketOutlook.UpdatedOn,
                TimeSpan.FromMilliseconds(1));

            var queryApi = new MarketDataAnalyticsQueryApi(_producer);
            var queryResult = await queryApi.GetMarketOutlookSnapshotAsync(contractId, valueDate);
            queryResult.Success.Should().BeTrue(queryResult.ErrorMessage);
            queryResult.Value.Should().BeEquivalentTo(
                snapshot,
                options => options.Excluding(value => value.UpdatedOn));
            queryResult.Value!.UpdatedOn.Should().BeCloseTo(
                snapshot.UpdatedOn,
                TimeSpan.FromMilliseconds(1));

            var streamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync(
                CommandSubject(ObserveMarketOutlookComponentCommand.Verb, entityId).ThreadId.ToString());
            streamId.Should().BeGreaterThan(0);
        }
        finally
        {
            await listener.StopAsync();
        }

        ValueTask OnNotificationAsync(string verb, NatsMsg<byte[]> message)
        {
            if (verb == MarketOutlookUpdatedNotifyEvent.Verb)
            {
                var notification = message.AsEvent<MarketOutlookUpdatedNotifyEvent>();
                if (notification is not null
                    && notification.EntityId == entityId
                    && notification.CommandId == publishId)
                    terminal.TrySetResult(notification);
            }
            return ValueTask.CompletedTask;
        }
    }

    static ActorSubject CommandSubject(string verb, MarketOutlookEntityId entityId)
        => new(
            ActorType.Command,
            ObserveMarketOutlookComponentCommand.Actor,
            verb,
            entityId.Format());
}
