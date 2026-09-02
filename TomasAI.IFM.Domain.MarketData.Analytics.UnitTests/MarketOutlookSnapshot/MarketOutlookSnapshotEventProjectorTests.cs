using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command.EventProjector;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.MarketOutlookSnapshot;

public sealed class MarketOutlookSnapshotEventProjectorTests
{
    [Fact]
    public async Task Apply_UpsertsBeforePublishingTheSameSnapshotAsRealtime()
    {
        var calls = new List<string>();
        var db = Substitute.For<IMarketDataDbContext>();
        db.UpsertMarketOutlookSnapshotAsync(
                Arg.Any<MarketOutlookReadModel>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                calls.Add("upsert");
                return Task.CompletedTask;
            });
        var actorContext = Substitute.For<ICommandActorContext>();
        actorContext.SendAsync<MarketOutlookSnapshotInsertedEvent, MarketOutlookEntityId>(
                Arg.Any<MarketOutlookSnapshotInsertedEvent>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                calls.Add("publish");
                return ValueTask.CompletedTask;
            });
        var inserted = InsertedEvent();

        var result = await MarketOutlookSnapshotEventProjector.ApplyAndPublishAsync(
            inserted, Execution(), db, actorContext);

        result.Outcome.Should().Be(EventProjectionApplyOutcome.Applied);
        calls.Should().Equal("upsert", "publish");
        await actorContext.Received(1)
            .SendAsync<MarketOutlookSnapshotInsertedEvent, MarketOutlookEntityId>(
                Arg.Is<MarketOutlookSnapshotInsertedEvent>(published =>
                    published.Subject.ActorType == ActorType.Realtime
                    && published.MarketOutlook == inserted.MarketOutlook
                    && published.CommandId == inserted.CommandId),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Apply_WhenUpsertFails_DoesNotPublishRealtime()
    {
        var db = Substitute.For<IMarketDataDbContext>();
        db.UpsertMarketOutlookSnapshotAsync(
                Arg.Any<MarketOutlookReadModel>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new IOException("Scylla unavailable"));
        var actorContext = Substitute.For<ICommandActorContext>();

        var apply = async () => await MarketOutlookSnapshotEventProjector.ApplyAndPublishAsync(
            InsertedEvent(), Execution(), db, actorContext);

        await apply.Should().ThrowAsync<IOException>()
            .WithMessage("Scylla unavailable");
        await actorContext.DidNotReceiveWithAnyArgs()
            .SendAsync<MarketOutlookSnapshotInsertedEvent, MarketOutlookEntityId>(default!, default);
    }

    static ProjectionExecutionContext Execution()
    {
        const long eventId = 42;
        const string projector = nameof(MarketOutlookSnapshotEventProjector);
        return new(
            projector,
            eventId,
            eventStreamId: 11,
            new EventProjectorEffectIdentity(
                projector, eventId, EventProjectorEffectKind.TargetProjection),
            Guid.NewGuid(),
            EventProjectionIdempotencyStrategy.NaturalKeyMutation,
            CancellationToken.None,
            streamVersion: 3);
    }

    static MarketOutlookSnapshotInsertedEvent InsertedEvent()
    {
        var snapshot = new MarketOutlookReadModel
        {
            ContractId = "ESZ26",
            ValueDate = new DateOnly(2026, 9, 2),
            UpdatedAtUtc = DateTime.UtcNow,
            MarketDataAsOfUtc = DateTime.UtcNow,
            FuturesEodData = new(
                "ESZ26", new DateOnly(2026, 9, 2), "ES",
                5_000m, 5_100m, 4_900m, 5_050m, 1000)
        };
        var entityId = new MarketOutlookEntityId(snapshot.ContractId, snapshot.ValueDate);
        return new()
        {
            Subject = new(
                ActorType.Event,
                MarketOutlookSnapshotInsertedEvent.Actor,
                MarketOutlookSnapshotInsertedEvent.Verb,
                entityId.Format()),
            CommandId = Guid.NewGuid(),
            EntityId = entityId,
            MarketOutlook = snapshot
        };
    }
}
