using TomasAI.IFM.Domain.MarketData.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.EconomicCalendar.Command.Actor;
using TomasAI.IFM.Domain.MarketData.EconomicCalendar.Command;
using TomasAI.IFM.Domain.MarketData.EconomicCalendar.Command.State;
using TomasAI.IFM.Domain.MarketData.YieldCurveRate.Command.Actor;
using TomasAI.IFM.Domain.MarketData.YieldCurveRate.Command;
using TomasAI.IFM.Domain.MarketData.YieldCurveRate.Command.State;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.EventSourceDb;

public sealed class HistoricalMarketDataImportOrderingTests(EventSourceActorSnapshotRangeFixture fixture)
    : IClassFixture<EventSourceActorSnapshotRangeFixture>
{
    [Fact]
    public async Task Given_the_same_committed_remove_and_import_facts_legacy_projection_order_can_change_the_final_calendar()
    {
        var connection = new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
        {
            Username = string.Empty,
            Password = string.Empty
        }.ConnectionString;
        var settings = new DbConnectionSettings()
            .Add("MarketDataDbConnection", connection, "System.Data.Postgres")
            .Add(EventSourceActorDbContext.EventSourceActorDbConnection, connection, "System.Data.Postgres");
        var logger = NullLogger<DbProvider>.Instance;
        await new MarketDataSchemaDb(settings, logger).CreateAllAsync();
        var repositories = new Dictionary<Type, object>();
        var factory = new DbContextFactory(new DbContextResolver(type => repositories[type]));
        var db = new MarketDataDbContext(settings, factory, Substitute.For<ISequenceIdGenerator>(), logger);
        repositories.Add(typeof(IObjectRepository<MarketDataDbContext>), db);

        var at = new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);
        var row = new EconomicCalendarReadModel(at, "US", "Historical ordering probe", null, "100", "90", at, "test");
        var removal = new EconomicCalendarRemovedEvent
        {
            Id = Guid.NewGuid(),
            CommandId = Guid.NewGuid(),
            EntityId = row.Id,
            Subject = new(ActorType.Event, EconomicCalendarRemovedEvent.Actor, EconomicCalendarRemovedEvent.Verb, "ordering-probe"),
            RemovedOn = at,
            RemovedBy = "test"
        };
        var accepted = new EconomicCalendarsImportedCompleteEvent
        {
            Id = Guid.NewGuid(),
            CommandId = Guid.NewGuid(),
            EconomicCalendars = [row],
            ReceivedOn = at,
            DuplicatePolicy = ImportDuplicatePolicy.Overwrite
        };
        var transactions = new PostgresEventTransaction(settings);

        await db.InsertEconomicCalendarAsync(row);
        await fixture.ActorEventDb.SaveEventsAsync("historical-ordering-command", removal.CommandId,
            new DomainEventCollection([removal]), 0, CancellationToken.None);
        await db.DeleteEconomicCalendarAsync(removal.EntityId);

        // The accepted fact and import write commit together. The earlier removal
        // has no write-order receipt, so its projection could also run afterward.
        await transactions.ExecuteOnceAsync(async (transaction, token) =>
        {
            await transaction.AppendAsync("historical-ordering-import", accepted.CommandId, accepted, 0, token);
            await transaction.ImportEconomicCalendarsAsync(accepted.EconomicCalendars, accepted.DuplicatePolicy, accepted.CommandId, token);
            return true;
        });
        Assert.NotNull(await db.GetEconomicCalendarAsync(row.Id));

        // Reproduce the other legal projection schedule without changing the source history.
        await db.DeleteEconomicCalendarAsync(removal.EntityId);
        Assert.Null(await db.GetEconomicCalendarAsync(row.Id));
    }

    [Fact]
    public async Task Given_legacy_facts_when_PostgreSQL_is_baselined_then_live_rows_and_deleted_keys_restore_from_snapshots()
    {
        var connection = new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
        {
            Username = string.Empty,
            Password = string.Empty
        }.ConnectionString;
        var settings = new DbConnectionSettings()
            .Add("MarketDataDbConnection", connection, "System.Data.Postgres")
            .Add(EventSourceActorDbContext.EventSourceActorDbConnection, connection, "System.Data.Postgres");
        await new MarketDataSchemaDb(settings, NullLogger<DbProvider>.Instance).CreateAllAsync();
        var transactions = new PostgresEventTransaction(settings);
        var migration = new MarketDataReconciliationBaseline(transactions);
        Assert.Throws<InvalidOperationException>(() => { _ = migration.CaptureAsync(writersStopped: false); });

        var at = new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);
        var live = new EconomicCalendarReadModel(at, "US", "Verified current row", null, "100", "90", at, "test");
        var deleted = live with { EventName = "Deleted from PostgreSQL" };
        var curve = new YieldCurveRateReadModel { ValueDate = new DateOnly(2026, 10, 7) };

        AddEconomicCalendarCommand CalendarCommand(EconomicCalendarReadModel row) => new(row)
        {
            CommandId = Guid.NewGuid(),
            Subject = new(ActorType.Command, AddEconomicCalendarCommand.Actor, AddEconomicCalendarCommand.Verb, row.Id.Format())
        };

        var deletedCommand = CalendarCommand(deleted);
        await fixture.ActorEventDb.SaveEventsAsync(deletedCommand.StreamId, deletedCommand.CommandId,
            new DomainEventCollection([new EconomicCalendarAddedEvent
            {
                Id = Guid.NewGuid(), CommandId = deletedCommand.CommandId, EntityId = deleted.Id, EconomicCalendar = deleted,
                Subject = new(ActorType.Event, EconomicCalendarAddedEvent.Actor, EconomicCalendarAddedEvent.Verb, deleted.Id.Format())
            }]), 0, CancellationToken.None);

        await transactions.ExecuteOnceAsync(async (transaction, token) =>
        {
            await transaction.ImportEconomicCalendarsAsync([live], ImportDuplicatePolicy.Overwrite, Guid.NewGuid(), token);
            await transaction.ImportYieldCurvesAsync([curve], ImportDuplicatePolicy.Overwrite, Guid.NewGuid(), at, token);
            return true;
        });

        Assert.True(await migration.CaptureAsync(writersStopped: true));
        Assert.False(await migration.CaptureAsync(writersStopped: true));

        var factory = Substitute.For<IEventSourceActorStateFactory>();
        factory.CreateState<EconomicCalendarCommandState>().Returns(_ => new EconomicCalendarCommandState());
        factory.CreateState<YieldCurveRateCommandState>().Returns(_ => new YieldCurveRateCommandState());
        var calendarContext = Substitute.For<IEconomicCalendarCommandContext>();
        calendarContext.DbEventSource.Returns(fixture.ActorEventDb);
        calendarContext.StateFactory.Returns(factory);
        calendarContext.ActorService.Returns(Substitute.For<IActorService>());
        calendarContext.Logger.Returns(NullLogger<EconomicCalendarCommandActor>.Instance);
        var calendarRepository = new EconomicCalendarStateRepository(calendarContext);

        var restoredLive = await calendarRepository.LoadStateAsync(CalendarCommand(live));
        Assert.True(restoredLive.EconomicCalendarExists(live.Id));
        Assert.Equal(1, restoredLive.CommittedStreamVersion);
        var restoredDeleted = await calendarRepository.LoadStateAsync(deletedCommand);
        Assert.False(restoredDeleted.EconomicCalendarExists(deleted.Id));
        Assert.Equal(2, restoredDeleted.CommittedStreamVersion);

        var liveCommand = CalendarCommand(live);
        var edited = live with { Actual = "101" };
        await fixture.ActorEventDb.SaveEventsAsync(liveCommand.StreamId, liveCommand.CommandId,
            new DomainEventCollection([new EconomicCalendarChangedEvent
            {
                Id = Guid.NewGuid(), CommandId = liveCommand.CommandId, EntityId = live.Id, EconomicCalendar = edited,
                Subject = new(ActorType.Event, EconomicCalendarChangedEvent.Actor, EconomicCalendarChangedEvent.Verb, live.Id.Format())
            }]), restoredLive.CommittedStreamVersion, CancellationToken.None);
        var calendarWithTail = await calendarRepository.LoadStateAsync(liveCommand);
        Assert.Equal(2, calendarWithTail.CommittedStreamVersion);
        Assert.Equal(edited, calendarWithTail.CaptureSnapshot(liveCommand).Entries![live.Id]);

        var yieldContext = Substitute.For<IYieldCurveRateCommandContext>();
        yieldContext.DbEventSource.Returns(fixture.ActorEventDb);
        yieldContext.StateFactory.Returns(factory);
        yieldContext.ActorService.Returns(Substitute.For<IActorService>());
        yieldContext.Logger.Returns(NullLogger<YieldCurveRateCommandActor>.Instance);
        var yieldRepository = new YieldCurveRateStateRepository(yieldContext);
        var curveCommand = new AddYieldCurveRateCommand(curve, false)
        {
            CommandId = Guid.NewGuid(),
            Subject = new(ActorType.Command, AddYieldCurveRateCommand.Actor, AddYieldCurveRateCommand.Verb, "2026")
        };
        var restoredCurve = await yieldRepository.LoadStateAsync(curveCommand);
        Assert.True(restoredCurve.YieldCurveRateExists(curve.ValueDate, false));
        Assert.Equal(1, restoredCurve.CommittedStreamVersion);

        var imported = live with { EventName = "New owner-routed row" };
        var importId = Guid.NewGuid();
        var apply = new ApplyEconomicCalendarsImportCommand
        {
            CommandId = importId,
            EntityId = imported.Id,
            Subject = new(ActorType.Command, AddEconomicCalendarCommand.Actor,
                ApplyEconomicCalendarsImportCommand.Verb, imported.Id.Format()),
            Accepted = new EconomicCalendarsImportedCompleteEvent
            {
                Id = importId, CommandId = Guid.NewGuid(), EntityId = imported.Id,
                EconomicCalendars = [imported], DuplicatePolicy = ImportDuplicatePolicy.Overwrite, ReceivedOn = at
            }
        };
        Assert.True((await apply.ExecuteAsync(transactions)).Success);
        Assert.True((await apply.ExecuteAsync(transactions)).Success);
        var importedState = await calendarRepository.LoadStateAsync(CalendarCommand(imported));
        Assert.True(importedState.EconomicCalendarExists(imported.Id));
        Assert.Equal(1, importedState.CommittedStreamVersion);

        var pendingId = Guid.NewGuid();
        await fixture.ActorEventDb.SaveEventsAsync(apply.StreamId, pendingId,
            new DomainEventCollection([new EconomicCalendarRemovedEvent
            {
                Id = Guid.NewGuid(), CommandId = pendingId, EntityId = imported.Id,
                Subject = new(ActorType.Event, EconomicCalendarRemovedEvent.Actor, EconomicCalendarRemovedEvent.Verb, imported.Id.Format()),
                RemovedOn = at, RemovedBy = "test", UseDurableProjection = true
            }]), 1, CancellationToken.None);
        var laterId = Guid.NewGuid();
        var later = apply with
        {
            CommandId = laterId,
            Accepted = apply.Accepted with { Id = laterId, CommandId = Guid.NewGuid() }
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => later.ExecuteAsync(transactions).AsTask());
        Assert.False(await fixture.ActorEventDb.HasEventForCommandAsync(laterId));

        var importedCurve = curve with { ValueDate = curve.ValueDate.AddDays(1) };
        var curveImportId = Guid.NewGuid();
        var applyCurve = new ApplyYieldCurveRatesImportCommand
        {
            CommandId = curveImportId, EntityId = new(2026),
            Subject = new(ActorType.Command, AddYieldCurveRateCommand.Actor, ApplyYieldCurveRatesImportCommand.Verb, "2026"),
            Accepted = new YieldCurveRatesImportedCompleteEvent
            {
                Id = curveImportId, CommandId = Guid.NewGuid(), EntityId = new(2026),
                YieldCurveRates = [importedCurve], DuplicatePolicy = ImportDuplicatePolicy.Overwrite, ReceivedOn = at
            }
        };
        Assert.True((await applyCurve.ExecuteAsync(transactions)).Success);
        var importedCurveState = await yieldRepository.LoadStateAsync(curveCommand);
        Assert.True(importedCurveState.YieldCurveRateExists(importedCurve.ValueDate, false));
        Assert.Equal(2, importedCurveState.CommittedStreamVersion);
    }
}
