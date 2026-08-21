using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketEvaluationSnapshot.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.MarketEvaluationSnapshot;

public sealed class MarketOutlookRealtimeActorTests
{
    sealed class TestableMarketOutlookRealtimeActor(
        IActorSupervisor supervisor,
        IDbContextFactory dbFactory,
        ILogger<MarketOutlookRealtimeActor> logger)
        : MarketOutlookRealtimeActor(supervisor, dbFactory, logger)
    {
        internal ValueTask InvokeReceiveAsync(IEventActorContext context, IEvent @event)
            => ReceiveAsync(context, @event);
    }

    [Fact]
    public async Task ComponentChange_IsRetainedWithoutPublishingAFrontendNotification()
    {
        var dbFactory = Substitute.For<IDbContextFactory>();
        var actor = CreateActor(dbFactory);
        var context = Substitute.For<IEventActorContext>();
        var entityId = new MarketOutlookEntityId("ESU26", new DateOnly(2026, 8, 21));
        var changed = new MarketOutlookComponentChangedRealtimeEvent
        {
            Subject = new ActorSubject(
                ActorType.Realtime,
                MarketOutlookComponentChangedRealtimeEvent.Actor,
                MarketOutlookComponentChangedRealtimeEvent.Verb,
                entityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = Guid.NewGuid(),
            EntityId = entityId
        };

        await actor.InvokeReceiveAsync(context, changed);

        _ = dbFactory.DidNotReceive().MarketDataDb;
        await context.DidNotReceiveWithAnyArgs()
            .SendAsync<MarketOutlookUpdatedNotifyEvent, MarketOutlookEntityId>(default!);
    }

    [Fact]
    public async Task NonEsEod_DoesNotCreateOrPublishAMarketOutlookSnapshot()
    {
        var dbFactory = Substitute.For<IDbContextFactory>();
        var actor = CreateActor(dbFactory);
        var context = Substitute.For<IEventActorContext>();
        var valueDate = new DateOnly(2026, 8, 21);
        var entityId = new MarketOutlookEntityId("NQU26", valueDate);
        var eod = new MarketOutlookEodUpdatedRealtimeEvent
        {
            Subject = new ActorSubject(
                ActorType.Realtime,
                MarketOutlookEodUpdatedRealtimeEvent.Actor,
                MarketOutlookEodUpdatedRealtimeEvent.Verb,
                entityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = Guid.NewGuid(),
            EntityId = entityId,
            FuturesEodData = new FuturesEodDataV2ReadModel(
                entityId.ContractId,
                valueDate,
                "NQ",
                20_000m,
                20_100m,
                19_900m,
                20_050m,
                100_000)
        };

        await actor.InvokeReceiveAsync(context, eod);

        _ = dbFactory.DidNotReceive().MarketDataDb;
        await context.DidNotReceiveWithAnyArgs()
            .SendAsync<MarketOutlookUpdatedNotifyEvent, MarketOutlookEntityId>(default!);
    }

    [Fact]
    public async Task EsEod_PersistsAndPublishesExactlyOneCompositeSnapshot()
    {
        var db = Substitute.For<IMarketDataDbContext>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        dbFactory.MarketDataDb.Returns(db);
        var actor = CreateActor(dbFactory);
        var context = Substitute.For<IEventActorContext>();
        context.RequestAsync<FuturesEodDataV2ReadModel, GetLastFuturesEodDataQuery>(
                Arg.Any<GetLastFuturesEodDataQuery>())
            .Returns(ValueTask.FromResult<ServiceResult<FuturesEodDataV2ReadModel>>(
                new ServiceOk<FuturesEodDataV2ReadModel>(null!)));
        context.RequestAsync<FuturesContractV2ReadModel[], GetCurrentlyTradedFuturesContractsQuery>(
                Arg.Any<GetCurrentlyTradedFuturesContractsQuery>())
            .Returns(ValueTask.FromResult<ServiceResult<FuturesContractV2ReadModel[]>>(
                new ServiceOk<FuturesContractV2ReadModel[]>([])));
        var valueDate = new DateOnly(2026, 8, 21);
        var entityId = new MarketOutlookEntityId("ESU26", valueDate);
        var source = new MarketOutlookEodUpdatedRealtimeEvent
        {
            Subject = new ActorSubject(
                ActorType.Realtime,
                MarketOutlookEodUpdatedRealtimeEvent.Actor,
                MarketOutlookEodUpdatedRealtimeEvent.Verb,
                entityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = Guid.NewGuid(),
            EntityId = entityId,
            FuturesEodData = new FuturesEodDataV2ReadModel(
                entityId.ContractId,
                valueDate,
                "ES",
                6_400m,
                6_450m,
                6_350m,
                6_425m,
                100_000)
        };

        await actor.InvokeReceiveAsync(context, source);

        await db.Received(1).UpsertMarketOutlookSnapshotAsync(
            Arg.Is<MarketOutlookSnapshotReadModel>(snapshot =>
                snapshot.ContractId == entityId.ContractId
                && snapshot.ValueDate == entityId.ValueDate
                && snapshot.Revision == 1
                && snapshot.FuturesEodData == source.FuturesEodData
                && snapshot.FuturesTradeSignal == null
                && snapshot.MissingInputs.Contains("RSI")));
        await context.Received(1)
            .SendAsync<MarketOutlookUpdatedNotifyEvent, MarketOutlookEntityId>(
                Arg.Is<MarketOutlookUpdatedNotifyEvent>(notification =>
                    notification.EntityId == entityId
                    && notification.CommandId == source.CommandId
                    && notification.MarketOutlook.Revision == 1));
    }

    static TestableMarketOutlookRealtimeActor CreateActor(IDbContextFactory dbFactory)
        => new(
            Substitute.For<IActorSupervisor>(),
            dbFactory,
            Substitute.For<ILogger<MarketOutlookRealtimeActor>>());
}
