using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.HistoricalDataLoader;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.EventSourceDb;

public sealed class HistoricalAnalyticsAttemptSnapshotTests(EventSourceActorSnapshotRangeFixture fixture)
    : IClassFixture<EventSourceActorSnapshotRangeFixture>
{
    [Fact]
    public async Task PostgreSql_restores_request_snapshot_and_reconciled_completion_after_failure()
    {
        var id = new FuturesAnalyticsHistoricalDataLoaderEntityId(Guid.NewGuid());
        var command = new LoadFuturesAnalyticsHistoricalDataCommand
        {
            CommandId = Guid.NewGuid(),
            EntityId = id,
            Subject = new(ActorType.Command, LoadFuturesAnalyticsHistoricalDataCommand.Actor,
                LoadFuturesAnalyticsHistoricalDataCommand.Verb, id.Format())
        };
        var request = new FuturesAnalyticsHistoricalDataLoaderRequestedEvent
        {
            EntityId = id,
            FuturesAnalyticsHistoricalDataLoaderParameters = new() { RequestedBy = "snapshot-qualification" }
        };
        var source = fixture.ActorEventDb;
        await source.SaveEventsAsync(command.StreamId, Guid.NewGuid(),
            new DomainEventCollection([request,
                new FuturesAnalyticsHistoricalDataLoaderFailedEvent { EntityId = id, ErrorMessage = "interrupted" },
                new FuturesAnalyticsHistoricalDataLoaderCompletedEvent { EntityId = id, RequestSha256 = "qualified" }]),
            0, CancellationToken.None);

        var factory = Substitute.For<IEventSourceActorStateFactory>();
        factory.CreateState<FuturesAnalyticsHistoricalDataLoaderCommandState>()
            .Returns(_ => new FuturesAnalyticsHistoricalDataLoaderCommandState());
        var repository = new FuturesAnalyticsHistoricalDataLoaderStateRepository(
            factory, source, Substitute.For<IActorService>(),
            Substitute.For<IEventProjector<FuturesAnalyticsHistoricalDataLoaderCommandActor>>(),
            NullLogger<FuturesAnalyticsHistoricalDataLoaderStateRepository>.Instance);
        var restored = await repository.LoadStateAsync(command, CancellationToken.None);

        Assert.True(restored.IsRequested);
        Assert.True(restored.IsCompleted);
        Assert.False(restored.IsFailed);
        Assert.Equal("snapshot-qualification", restored.FuturesAnalyticsHistoricalDataLoaderParameters?.RequestedBy);
        Assert.Equal(3, restored.CommittedStreamVersion);
        Assert.Empty(restored.Events);
    }
}
