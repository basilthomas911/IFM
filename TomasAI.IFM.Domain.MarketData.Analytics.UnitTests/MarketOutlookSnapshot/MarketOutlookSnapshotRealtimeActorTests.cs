using Microsoft.Extensions.Logging;
using NSubstitute;
using FluentAssertions;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.MarketOutlookSnapshot;

public sealed class MarketOutlookSnapshotRealtimeActorTests
{
    sealed class TestableMarketOutlookSnapshotRealtimeActor(
        IRealtimeActorContext<MarketOutlookSnapshotRealtimeActor> context)
        : MarketOutlookSnapshotRealtimeActor(context)
    {
        internal ValueTask InvokeReceiveAsync(
            IEventActorContext<MarketOutlookSnapshotRealtimeActor> context,
            IEvent @event) => ReceiveAsync(context, @event);
    }

    [Fact]
    public async Task ComponentChange_ForwardsOneCommandWithoutPublishingFrontendNotification()
    {
        var context = Context();
        var actor = new TestableMarketOutlookSnapshotRealtimeActor(context);
        var entityId = new MarketOutlookEntityId("ESU26", new DateOnly(2026, 8, 21));
        var changed = new MarketOutlookComponentChangedRealtimeEvent
        {
            Subject = RealtimeSubject(MarketOutlookComponentChangedRealtimeEvent.Verb, entityId),
            Id = Guid.NewGuid(),
            CommandId = Guid.NewGuid(),
            EntityId = entityId,
            EventId = 23,
            ReceivedOn = DateTime.UtcNow,
            EventSource = "test",
            FuturesRsiSignal = SampleData.AtrRsiSignals[0] with
            {
                ContractId = entityId.ContractId,
                ValueDate = entityId.ValueDate,
                TimePeriod = TimeFrameType.FifteenSeconds,
                PeriodLength = FuturesIntradaySignalActivationProfile.RsiPeriodLength,
                IsWarm = true
            }
        };

        await actor.InvokeReceiveAsync(context, changed);

        await context.Received(1).RequestAsync<ObserveMarketOutlookComponentCommand, MarketOutlookEntityId>(
            Arg.Is<ObserveMarketOutlookComponentCommand>(command =>
                command.CommandId == changed.Id
                && command.SourceEventId == changed.Id
                && command.SourceEventSequence == changed.EventId
                && command.FuturesRsiSignal == changed.FuturesRsiSignal));
        await context.DidNotReceiveWithAnyArgs()
            .SendAsync<MarketOutlookUpdatedNotifyEvent, MarketOutlookEntityId>(default!);
    }

    [Fact]
    public async Task RejectedComponentCommand_IsLoggedWithoutThrowingOrPublishingNotification()
    {
        var context = Context();
        context.RequestAsync<ObserveMarketOutlookComponentCommand, MarketOutlookEntityId>(
                Arg.Any<ObserveMarketOutlookComponentCommand>())
            .Returns(ValueTask.FromResult<ServiceResult<GuidResult>>(
                new ServiceFailed<GuidResult>(ObserveMarketOutlookComponentCommand.ErrorId, "rejected")));
        var actor = new TestableMarketOutlookSnapshotRealtimeActor(context);
        var entityId = new MarketOutlookEntityId("ESU26", new DateOnly(2026, 8, 21));
        var changed = new MarketOutlookComponentChangedRealtimeEvent
        {
            Subject = RealtimeSubject(MarketOutlookComponentChangedRealtimeEvent.Verb, entityId),
            Id = Guid.NewGuid(),
            CommandId = Guid.NewGuid(),
            EntityId = entityId,
            EventId = 25,
            ReceivedOn = DateTime.UtcNow,
            EventSource = "rejected-test",
            FuturesRsiSignal = SampleData.AtrRsiSignals[0] with
            {
                ContractId = entityId.ContractId,
                ValueDate = entityId.ValueDate,
                TimePeriod = TimeFrameType.FifteenSeconds,
                PeriodLength = FuturesIntradaySignalActivationProfile.RsiPeriodLength,
                IsWarm = true
            }
        };

        Func<Task> receive = () => actor.InvokeReceiveAsync(context, changed).AsTask();

        await receive.Should().NotThrowAsync();
        await context.DidNotReceiveWithAnyArgs()
            .SendAsync<MarketOutlookUpdatedNotifyEvent, MarketOutlookEntityId>(default!);
    }

    [Fact]
    public async Task UnsupportedItiComponent_IsIgnoredWithoutRequestOrException()
    {
        var context = Context();
        var actor = new TestableMarketOutlookSnapshotRealtimeActor(context);
        var entityId = new MarketOutlookEntityId("ESU26", new DateOnly(2026, 8, 21));
        var changed = new MarketOutlookComponentChangedRealtimeEvent
        {
            Subject = RealtimeSubject(MarketOutlookComponentChangedRealtimeEvent.Verb, entityId),
            Id = Guid.NewGuid(),
            CommandId = Guid.NewGuid(),
            EntityId = entityId,
            EventId = 24,
            ReceivedOn = DateTime.UtcNow,
            EventSource = "ineligible-iti-test",
            FuturesItiSignal = SampleData.StartOfDayEvent.FuturesItiSignal! with
            {
                ContractId = entityId.ContractId,
                ValueDate = entityId.ValueDate,
                TimePeriod = TimeFrameType.Daily,
                IntrinsicTimeMode = IntrinsicTimeModeType.PredictedIntervalChanged
            }
        };
        Func<Task> receive = () => actor.InvokeReceiveAsync(context, changed).AsTask();

        await receive.Should().NotThrowAsync();

        await context.DidNotReceiveWithAnyArgs()
            .RequestAsync<ObserveMarketOutlookComponentCommand, MarketOutlookEntityId>(default!);
    }

    [Fact]
    public async Task UnsupportedItiCompletion_DoesNotPublishRealtimeComponentEvent()
    {
        var context = Context();
        var valueDate = new DateOnly(2026, 8, 21);
        var entityId = new FuturesItiSignalEntityId("ESU26", valueDate, TimeFrameType.Daily);
        var source = SampleData.CreateItiSignalGeneratedCompleteEvent() with
        {
            EntityId = entityId,
            VixFuturesPrice = 0,
            FuturesItiSignal = SampleData.StartOfDayEvent.FuturesItiSignal! with
            {
                ContractId = entityId.ContractId,
                ValueDate = entityId.ValueDate,
                TimePeriod = TimeFrameType.Daily,
                IntrinsicTimeMode = IntrinsicTimeModeType.PredictedIntervalChanged
            }
        };

        await context.PublishMarketOutlookComponentAsync(source);

        await context.DidNotReceiveWithAnyArgs()
            .SendAsync<MarketOutlookComponentChangedRealtimeEvent, MarketOutlookEntityId>(default!);
    }

    [Fact]
    public async Task UnsupportedItiCompletion_StillPublishesValidVixSibling()
    {
        var context = Context();
        var valueDate = new DateOnly(2026, 8, 21);
        var entityId = new FuturesItiSignalEntityId("ESU26", valueDate, TimeFrameType.Daily);
        var source = SampleData.CreateItiSignalGeneratedCompleteEvent() with
        {
            EntityId = entityId,
            VixFuturesPrice = 22.25,
            FuturesItiSignal = SampleData.StartOfDayEvent.FuturesItiSignal! with
            {
                ContractId = entityId.ContractId,
                ValueDate = entityId.ValueDate,
                TimePeriod = TimeFrameType.Daily,
                IntrinsicTimeMode = IntrinsicTimeModeType.PredictedIntervalChanged
            }
        };

        await context.PublishMarketOutlookComponentAsync(source);

        await context.Received(1)
            .SendAsync<MarketOutlookComponentChangedRealtimeEvent, MarketOutlookEntityId>(
                Arg.Is<MarketOutlookComponentChangedRealtimeEvent>(changed =>
                    changed.FuturesItiSignal == null
                    && changed.VixFuturesPrice == 22.25m));
    }

    [Fact]
    public async Task EligibleItiCompletion_PublishesRealtimeComponentEvent()
    {
        var context = Context();
        var valueDate = new DateOnly(2026, 8, 21);
        var entityId = new FuturesItiSignalEntityId("ESU26", valueDate, TimeFrameType.Daily);
        var source = SampleData.CreateItiSignalGeneratedCompleteEvent() with
        {
            EntityId = entityId,
            FuturesItiSignal = SampleData.StartOfDayEvent.FuturesItiSignal! with
            {
                ContractId = entityId.ContractId,
                ValueDate = entityId.ValueDate,
                TimePeriod = TimeFrameType.Daily,
                IntrinsicTimeMode = IntrinsicTimeModeType.TrendDirectionChanged
            }
        };

        await context.PublishMarketOutlookComponentAsync(source);

        await context.Received(1)
            .SendAsync<MarketOutlookComponentChangedRealtimeEvent, MarketOutlookEntityId>(
                Arg.Is<MarketOutlookComponentChangedRealtimeEvent>(changed =>
                    changed.EntityId.ContractId == entityId.ContractId
                    && changed.EntityId.ValueDate == entityId.ValueDate
                    && changed.FuturesItiSignal == source.FuturesItiSignal));
    }

    [Fact]
    public async Task NonEsEod_DoesNotSendPublicationCommand()
    {
        var context = Context();
        var actor = new TestableMarketOutlookSnapshotRealtimeActor(context);
        var valueDate = new DateOnly(2026, 8, 21);
        var entityId = new MarketOutlookEntityId("NQU26", valueDate);
        var eod = new MarketOutlookEodUpdatedRealtimeEvent
        {
            Subject = RealtimeSubject(MarketOutlookEodUpdatedRealtimeEvent.Verb, entityId),
            Id = Guid.NewGuid(),
            CommandId = Guid.NewGuid(),
            EntityId = entityId,
            ReceivedOn = DateTime.UtcNow,
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

        await context.DidNotReceiveWithAnyArgs()
            .RequestAsync<PublishMarketOutlookSnapshotCommand, MarketOutlookEntityId>(default!);
    }

    [Fact]
    public async Task EsEod_ReconcilesInputsAndForwardsPublicationCommand()
    {
        var db = Substitute.For<IMarketDataDbContext>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        dbFactory.MarketDataDb.Returns(db);
        var context = Context(dbFactory);
        context.RequestAsync<FuturesEodDataV2ReadModel, GetLastFuturesEodDataQuery>(
                Arg.Any<GetLastFuturesEodDataQuery>())
            .Returns(ValueTask.FromResult<ServiceResult<FuturesEodDataV2ReadModel>>(
                new ServiceOk<FuturesEodDataV2ReadModel>(null!)));
        context.RequestAsync<FuturesContractV2ReadModel[], GetCurrentlyTradedFuturesContractsQuery>(
                Arg.Any<GetCurrentlyTradedFuturesContractsQuery>())
            .Returns(ValueTask.FromResult<ServiceResult<FuturesContractV2ReadModel[]>>(
                new ServiceOk<FuturesContractV2ReadModel[]>([])));
        var actor = new TestableMarketOutlookSnapshotRealtimeActor(context);
        var valueDate = new DateOnly(2026, 8, 21);
        var entityId = new MarketOutlookEntityId("ESU26", valueDate);
        var source = new MarketOutlookEodUpdatedRealtimeEvent
        {
            Subject = RealtimeSubject(MarketOutlookEodUpdatedRealtimeEvent.Verb, entityId),
            Id = Guid.NewGuid(),
            CommandId = Guid.NewGuid(),
            EntityId = entityId,
            EventId = 41,
            ReceivedOn = DateTime.UtcNow,
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

        await context.Received(1).RequestAsync<PublishMarketOutlookSnapshotCommand, MarketOutlookEntityId>(
            Arg.Is<PublishMarketOutlookSnapshotCommand>(command =>
                command.CommandId == source.Id
                && command.SourceEventId == source.Id
                && command.SourceEventSequence == source.EventId
                && command.FuturesEodData == source.FuturesEodData));
        await db.DidNotReceiveWithAnyArgs().UpsertMarketOutlookSnapshotAsync(default!);
    }

    [Fact]
    public async Task SnapshotProjectionComplete_PublishesExactlyOneFrontendNotification()
    {
        var context = Context();
        var actor = new TestableMarketOutlookSnapshotRealtimeActor(context);
        var entityId = new MarketOutlookEntityId("ESU26", new DateOnly(2026, 8, 21));
        var snapshot = new MarketOutlookSnapshotReadModel
        {
            ContractId = entityId.ContractId,
            ValueDate = entityId.ValueDate,
            Revision = 3,
            UpdatedOn = DateTime.UtcNow,
            FuturesEodData = SampleData.EodData with
            {
                ContractId = entityId.ContractId,
                ValueDate = entityId.ValueDate
            }
        };
        var completed = new MarketOutlookSnapshotPublishedCompleteEvent
        {
            Subject = RealtimeSubject(MarketOutlookSnapshotPublishedCompleteEvent.Verb, entityId),
            Id = Guid.NewGuid(),
            CommandId = Guid.NewGuid(),
            EntityId = entityId,
            MarketOutlook = snapshot
        };

        await actor.InvokeReceiveAsync(context, completed);

        await context.Received(1).SendAsync<MarketOutlookUpdatedNotifyEvent, MarketOutlookEntityId>(
            Arg.Is<MarketOutlookUpdatedNotifyEvent>(notification =>
                notification.EntityId == entityId
                && notification.CommandId == completed.CommandId
                && notification.MarketOutlook == snapshot));
    }

    [Fact]
    public async Task ComponentProjectionComplete_PublishesReprojectedFrontendSnapshot()
    {
        var context = Context();
        var actor = new TestableMarketOutlookSnapshotRealtimeActor(context);
        var entityId = new MarketOutlookEntityId("ESU26", new DateOnly(2026, 8, 21));
        var snapshot = new MarketOutlookSnapshotReadModel
        {
            ContractId = entityId.ContractId,
            ValueDate = entityId.ValueDate,
            Revision = 4,
            UpdatedOn = DateTime.UtcNow,
            FuturesEodData = SampleData.EodData with
            {
                ContractId = entityId.ContractId,
                ValueDate = entityId.ValueDate
            }
        };
        var completed = new MarketOutlookComponentObservedCompleteEvent
        {
            Subject = RealtimeSubject(MarketOutlookComponentObservedCompleteEvent.Verb, entityId),
            Id = Guid.NewGuid(),
            CommandId = Guid.NewGuid(),
            EntityId = entityId,
            WorkingState = new MarketOutlookWorkingStateReadModel
            {
                EntityId = entityId,
                Revision = 7,
                PublishedSnapshot = snapshot,
                Status = MarketOutlookStateStatus.Published
            }
        };

        await actor.InvokeReceiveAsync(context, completed);

        await context.Received(1).SendAsync<MarketOutlookUpdatedNotifyEvent, MarketOutlookEntityId>(
            Arg.Is<MarketOutlookUpdatedNotifyEvent>(notification =>
                notification.EntityId == entityId
                && notification.CommandId == completed.CommandId
                && notification.MarketOutlook == snapshot));
    }

    [Fact]
    public async Task LiveTradePreview_AcceptsOrderedNewTrades_FencesDuplicatesAndGaps_ThenRecoversOnNewEpoch()
    {
        const string contractId = "ESZ00";
        var valueDate = new DateOnly(2026, 8, 31);
        MarketOutlookDailyPreviewCalculatorTests.SeedBaseline(contractId);
        var database = Substitute.For<IMarketDataDbContext>();
        database.GetMarketOutlookSnapshotAsync(contractId, valueDate, Arg.Any<CancellationToken>())
            .Returns((MarketOutlookSnapshotReadModel?)null);
        var dbFactory = Substitute.For<IDbContextFactory>();
        dbFactory.MarketDataDb.Returns(database);
        var context = Context(dbFactory);
        var actor = new TestableMarketOutlookSnapshotRealtimeActor(context);
        var first = MarketOutlookDailyPreviewCalculatorTests.Trade(contractId, 7_100m, 1);

        await actor.InvokeReceiveAsync(context, first);
        await actor.InvokeReceiveAsync(context, first);
        await actor.InvokeReceiveAsync(context,
            MarketOutlookDailyPreviewCalculatorTests.Trade(contractId, 7_102m, 3));
        await actor.InvokeReceiveAsync(context,
            MarketOutlookDailyPreviewCalculatorTests.Trade(contractId, 7_103m, 4));

        var newEpoch = MarketOutlookDailyPreviewCalculatorTests.Trade(contractId, 7_104m, 1);
        newEpoch = newEpoch with
        {
            Price = newEpoch.Price with
            {
                Trade = newEpoch.Price.Trade!.Value with { StreamEpochId = Guid.NewGuid() }
            }
        };
        await actor.InvokeReceiveAsync(context, newEpoch);

        await context.Received(2).SendAsync<MarketOutlookUpdatedNotifyEvent, MarketOutlookEntityId>(
            Arg.Is<MarketOutlookUpdatedNotifyEvent>(notification =>
                notification.MarketOutlook.FuturesEmaSignal!.IsProvisional
                && notification.MarketOutlook.FuturesBbSignal!.IsProvisional));
        await database.Received(1).GetMarketOutlookSnapshotAsync(
            contractId, valueDate, Arg.Any<CancellationToken>());
    }

    static IRealtimeActorContext<MarketOutlookSnapshotRealtimeActor> Context(
        IDbContextFactory? dbFactory = null)
    {
        var context = Substitute.For<
            IRealtimeActorContext<MarketOutlookSnapshotRealtimeActor>,
            IMarketOutlookSnapshotRealtimeContext>();
        var typed = (IMarketOutlookSnapshotRealtimeContext)context;
        typed.DbFactory.Returns(dbFactory ?? Substitute.For<IDbContextFactory>());
        typed.Logger.Returns(Substitute.For<ILogger<MarketOutlookSnapshotRealtimeActor>>());
        context.RequestAsync<ObserveMarketOutlookComponentCommand, MarketOutlookEntityId>(
                Arg.Any<ObserveMarketOutlookComponentCommand>())
            .Returns(ValueTask.FromResult<ServiceResult<GuidResult>>(
                new ServiceOk<GuidResult>(new GuidResult(Guid.NewGuid()))));
        context.RequestAsync<PublishMarketOutlookSnapshotCommand, MarketOutlookEntityId>(
                Arg.Any<PublishMarketOutlookSnapshotCommand>())
            .Returns(ValueTask.FromResult<ServiceResult<GuidResult>>(
                new ServiceOk<GuidResult>(new GuidResult(Guid.NewGuid()))));
        return context;
    }

    static ActorSubject RealtimeSubject(string verb, MarketOutlookEntityId entityId)
        => new(ActorType.Realtime, MarketOutlookSnapshotRealtimeActor.ActorName, verb, entityId.Format());
}
