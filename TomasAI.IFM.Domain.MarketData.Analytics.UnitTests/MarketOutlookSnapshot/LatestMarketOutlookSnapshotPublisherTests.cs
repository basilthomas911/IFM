using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Model.Processing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.MarketOutlookSnapshot;

public sealed class LatestMarketOutlookSnapshotPublisherTests
{
    [Fact]
    public async Task Publish_sends_realtime_immediately_without_waiting_for_persistence()
    {
        var scenario = CreateScenario();
        var snapshot = Snapshot(DateTime.UtcNow, 5_050m);

        await scenario.Publisher.PublishAsync(Update(snapshot), snapshot, CancellationToken.None);

        await scenario.Producer.Received(1)
            .SendAsync<MarketOutlookSnapshotInsertedEvent, MarketOutlookEntityId>(
                Arg.Is<ActorSubject>(subject => subject.ActorType == ActorType.Realtime),
                Arg.Is<MarketOutlookSnapshotInsertedEvent>(notification =>
                    notification.MarketOutlook.ContractId == snapshot.ContractId
                    && notification.MarketOutlook.ValueDate == snapshot.ValueDate
                    && notification.MarketOutlook.SnapshotSource ==
                        MarketOutlookSnapshotSource.DatabentoLive
                    && notification.EntityId.ContractId == snapshot.ContractId
                    && notification.EntityId.ValueDate == snapshot.ValueDate),
                Arg.Any<CancellationToken>());
        await scenario.Db.DidNotReceiveWithAnyArgs()
            .UpsertMarketOutlookSnapshotAsync(default!, default, default);
        scenario.Publisher.PendingCount.Should().Be(1);
    }

    [Fact]
    public async Task Flush_persists_only_the_newest_snapshot_for_each_contract_and_date()
    {
        var scenario = CreateScenario();
        var now = DateTime.UtcNow;
        var first = Snapshot(now, 5_050m);
        var latest = Snapshot(now.AddMilliseconds(1), 5_075m);

        await scenario.Publisher.PublishAsync(Update(first), first, CancellationToken.None);
        await scenario.Publisher.PublishAsync(Update(latest), latest, CancellationToken.None);
        await scenario.Publisher.FlushPendingAsync();

        await scenario.Db.Received(1).UpsertMarketOutlookSnapshotAsync(
            Arg.Is<MarketOutlookReadModel>(persisted =>
                persisted.UpdatedAtUtc == latest.UpdatedAtUtc
                && persisted.FuturesEodData.ClosePrice == 5_075m
                && persisted.SnapshotSource == MarketOutlookSnapshotSource.DatabentoLive),
            Arg.Any<long>(),
            Arg.Any<CancellationToken>());
        await scenario.Producer.Received(2)
            .SendAsync<MarketOutlookSnapshotInsertedEvent, MarketOutlookEntityId>(
                Arg.Any<ActorSubject>(),
                Arg.Any<MarketOutlookSnapshotInsertedEvent>(),
                Arg.Any<CancellationToken>());
        scenario.Publisher.PendingCount.Should().Be(0);
    }

    [Fact]
    public async Task Failed_persistence_is_retried_without_replaying_realtime_notifications()
    {
        var scenario = CreateScenario();
        scenario.Db.UpsertMarketOutlookSnapshotAsync(
                Arg.Any<MarketOutlookReadModel>(),
                Arg.Any<long>(),
                Arg.Any<CancellationToken>())
            .Returns(
                _ => Task.FromException(new IOException("Scylla unavailable")),
                _ => Task.CompletedTask);
        var snapshot = Snapshot(DateTime.UtcNow, 5_050m);

        await scenario.Publisher.PublishAsync(Update(snapshot), snapshot, CancellationToken.None);
        await scenario.Publisher.FlushPendingAsync();
        scenario.Publisher.PendingCount.Should().Be(1);

        await scenario.Publisher.FlushPendingAsync();

        await scenario.Db.Received(2).UpsertMarketOutlookSnapshotAsync(
            Arg.Any<MarketOutlookReadModel>(),
            Arg.Any<long>(),
            Arg.Any<CancellationToken>());
        await scenario.Producer.Received(1)
            .SendAsync<MarketOutlookSnapshotInsertedEvent, MarketOutlookEntityId>(
                Arg.Any<ActorSubject>(),
                Arg.Any<MarketOutlookSnapshotInsertedEvent>(),
                Arg.Any<CancellationToken>());
        scenario.Publisher.PendingCount.Should().Be(0);
    }

    [Fact]
    public async Task Stop_flushes_the_latest_snapshot_for_restart_hydration()
    {
        var scenario = CreateScenario();
        var snapshot = Snapshot(DateTime.UtcNow, 5_050m);
        await scenario.Publisher.PublishAsync(Update(snapshot), snapshot, CancellationToken.None);

        await scenario.Publisher.StopAsync(CancellationToken.None);

        await scenario.Db.Received(1).UpsertMarketOutlookSnapshotAsync(
            Arg.Is<MarketOutlookReadModel>(persisted =>
                persisted.ContractId == snapshot.ContractId
                && persisted.ValueDate == snapshot.ValueDate),
            Arg.Any<long>(),
            Arg.Any<CancellationToken>());
        scenario.Publisher.PendingCount.Should().Be(0);
    }

    static Scenario CreateScenario()
    {
        var db = Substitute.For<IMarketDataDbContext>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        dbFactory.MarketDataDb.Returns(db);
        var producer = Substitute.For<IActorProducer>();
        var supervisor = Substitute.For<IActorSupervisor>();
        supervisor.GetProducer(Arg.Any<ActorMailboxId>()).Returns(producer);
        var publisher = new LatestMarketOutlookSnapshotPublisher(
            dbFactory,
            supervisor,
            new(MarketOutlookSnapshotSource.DatabentoLive),
            Substitute.For<ILogger<LatestMarketOutlookSnapshotPublisher>>());
        return new(publisher, db, producer);
    }

    static EodMarketOutlookUpdate Update(MarketOutlookReadModel snapshot) => new()
    {
        UpdateId = Guid.NewGuid(),
        EntityId = new(snapshot.ContractId, snapshot.ValueDate),
        ReceivedAtUtc = DateTime.UtcNow,
        MarketDataAsOfUtc = snapshot.MarketDataAsOfUtc,
        Eod = snapshot.FuturesEodData,
        EventSource = "test"
    };

    static MarketOutlookReadModel Snapshot(DateTime updatedAtUtc, decimal closePrice)
    {
        const string contractId = "ESZ26";
        var valueDate = new DateOnly(2026, 9, 4);
        return new()
        {
            ContractId = contractId,
            ValueDate = valueDate,
            UpdatedAtUtc = updatedAtUtc,
            MarketDataAsOfUtc = updatedAtUtc,
            FuturesEodData = new(
                contractId,
                valueDate,
                "ES",
                5_000m,
                5_100m,
                4_900m,
                closePrice,
                1_000)
        };
    }

    sealed record Scenario(
        LatestMarketOutlookSnapshotPublisher Publisher,
        IMarketDataDbContext Db,
        IActorProducer Producer);
}
