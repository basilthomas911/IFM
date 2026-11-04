using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Application.Storage.ReferenceDb.Schema;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command.State;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.State;
using TomasAI.IFM.Domain.Reference.LookupType.Command.Actor;
using TomasAI.IFM.Domain.Reference.LookupType.Command.State;
using TomasAI.IFM.Domain.Reference.Shared.Events;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.EconomicCalendar.Command.Actor;
using TomasAI.IFM.Domain.MarketData.EconomicCalendar.Command.State;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.YieldCurveRate.Command.Actor;
using TomasAI.IFM.Domain.MarketData.YieldCurveRate.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using Xunit;
using Xunit.Abstractions;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.EventSourceDb;

public sealed class PeriodicMarketDataSnapshotTests(EventSourceActorSnapshotRangeFixture fixture, ITestOutputHelper output)
    : IClassFixture<EventSourceActorSnapshotRangeFixture>
{
    private static readonly DateOnly FirstDate = new(2026, 1, 1);

    [Fact]
    public async Task Yield_curve_saves_a_full_snapshot_at_the_boundary()
    {
        var context = Substitute.For<IYieldCurveRateCommandContext>();
        context.DbEventSource.Returns(fixture.ActorEventDb);
        var factory = Factory<YieldCurveRateCommandState>();
        var actors = Substitute.For<IActorService>();
        var databases = DatabaseFactory();
        context.StateFactory.Returns(factory);
        context.ActorService.Returns(actors);
        context.Logger.Returns(NullLogger<YieldCurveRateCommandActor>.Instance);
        context.DbFactory.Returns(databases);
        var repository = new YieldCurveRateStateRepository(context);
        await Verify<YieldCurveRateCommandState, YieldCurveRateStateSnapshottedEvent>(
            repository, context, (i, id) => new YieldCurveRateAddedEvent
            {
                Id = Guid.NewGuid(), CommandId = id, EntityId = new(2026),
                Subject = new(ActorType.Event, "YieldCurveRateEvent", "Added", "2026"),
                YieldCurveRate = new() { ValueDate = FirstDate.AddDays(i) }
            },
            (full, snapshot, command) => Assert.Equal(full.CaptureSnapshot(command).ValueDates, snapshot.CaptureSnapshot(command).ValueDates));
        await context.Received(1).SendAsync<YieldCurveRateAddedEvent, YieldCurveRateEntityId>(Arg.Any<YieldCurveRateAddedEvent>());
    }

    [Fact]
    public async Task Economic_calendar_saves_a_full_snapshot_at_the_boundary()
    {
        var context = Substitute.For<IEconomicCalendarCommandContext>();
        context.DbEventSource.Returns(fixture.ActorEventDb);
        var factory = Factory<EconomicCalendarCommandState>();
        var actors = Substitute.For<IActorService>();
        var databases = DatabaseFactory();
        context.StateFactory.Returns(factory);
        context.ActorService.Returns(actors);
        context.Logger.Returns(NullLogger<EconomicCalendarCommandActor>.Instance);
        context.DbFactory.Returns(databases);
        var repository = new EconomicCalendarStateRepository(context);
        await Verify<EconomicCalendarCommandState, EconomicCalendarStateSnapshottedEvent>(
            repository, context, (i, id) =>
            {
                var entry = new EconomicCalendarReadModel
                {
                    EventDate = FirstDate.AddDays(i).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                    CountryCode = "US", EventName = "test"
                };
                return new EconomicCalendarAddedEvent
                {
                    Id = Guid.NewGuid(), CommandId = id, EntityId = entry.Id, EconomicCalendar = entry,
                    Subject = new(ActorType.Event, "EconomicCalendarEvent", "Added", entry.Id.Format())
                };
            },
            (full, snapshot, command) =>
                Assert.Equivalent(full.CaptureSnapshot(command).Entries, snapshot.CaptureSnapshot(command).Entries));
        await context.Received(1).SendAsync<EconomicCalendarAddedEvent, EconomicCalendarId>(Arg.Any<EconomicCalendarAddedEvent>());
    }

    [Fact]
    public async Task Futures_contract_catalog_preserves_membership_at_the_snapshot_boundary()
    {
        var context = Substitute.For<IFuturesContractCommandContext>();
        var factory = Factory<FuturesContractCommandState>();
        var actors = Substitute.For<IActorService>();
        var projector = Substitute.For<IEventProjector<FuturesContractCommandActor>>();
        var databases = SecuritiesFactory();
        context.DbEventSource.Returns(fixture.ActorEventDb);
        context.StateFactory.Returns(factory);
        context.ActorService.Returns(actors);
        context.EventProjector.Returns(projector);
        context.Logger.Returns(NullLogger<FuturesContractCommandActor>.Instance);
        context.DbFactory.Returns(databases);
        var repository = new FuturesContractStateRepository(context, Transactions());
        await Verify<FuturesContractCommandState, FuturesContractStateSnapshottedEvent>(
            repository, context, (i, commandId) =>
            {
                var row = new FuturesContractV3ReadModel { ContractId = $"ES-{i}", Symbol = "ES", LastTradeDate = FirstDate };
                return i == 127
                    ? new FuturesContractRemovedEvent { Id = Guid.NewGuid(), CommandId = commandId,
                        EntityId = row.Id, ContractId = new("ES-1", "ES", FirstDate) }
                    : new FuturesContractAddedEvent { Id = Guid.NewGuid(), CommandId = commandId, EntityId = row.Id, Contract = row };
            },
            (full, snapshot, command) => Assert.Equal(full.CaptureSnapshot(command).ContractIds, snapshot.CaptureSnapshot(command).ContractIds));
        await projector.Received(1).DomainEventsProjectionAsync(Arg.Is<DomainEventCollection>(
            facts => facts.Count == 1 && facts[0] is FuturesContractRemovedEvent));
    }

    [Fact]
    public async Task Futures_option_catalog_preserves_membership_at_the_snapshot_boundary()
    {
        var context = Substitute.For<IFuturesOptionContractCommandContext>();
        var factory = Factory<FuturesOptionContractCommandState>();
        var actors = Substitute.For<IActorService>();
        var projector = Substitute.For<IEventProjector<FuturesOptionContractCommandActor>>();
        var databases = SecuritiesFactory();
        context.DbEventSource.Returns(fixture.ActorEventDb);
        context.StateFactory.Returns(factory);
        context.ActorService.Returns(actors);
        context.EventProjector.Returns(projector);
        context.Logger.Returns(NullLogger<FuturesOptionContractCommandActor>.Instance);
        context.DbFactory.Returns(databases);
        var repository = new FuturesOptionContractStateRepository(context, Transactions());
        await Verify<FuturesOptionContractCommandState, FuturesOptionContractStateSnapshottedEvent>(
            repository, context, (i, commandId) =>
            {
                var id = new FuturesOptionContractEntityId($"ES-option-{i}", 2026);
                return i == 127
                    ? new FuturesOptionContractRemovedEvent { Id = Guid.NewGuid(), CommandId = commandId,
                        EntityId = id, ContractId = "ES-option-1" }
                    : new FuturesOptionContractAddedEvent { Id = Guid.NewGuid(), CommandId = commandId, EntityId = id,
                        Contract = new() { ContractId = $"ES-option-{i}", ContractMonth = FirstDate } };
            },
            (full, snapshot, command) => Assert.Equal(full.CaptureSnapshot(command).ContractIds, snapshot.CaptureSnapshot(command).ContractIds));
        await projector.Received(1).DomainEventsProjectionAsync(Arg.Is<DomainEventCollection>(
            facts => facts.Count == 1 && facts[0] is FuturesOptionContractRemovedEvent));
    }

    [Fact]
    public async Task Lookup_catalog_preserves_accepted_entries_and_original_keys()
    {
        var context = Substitute.For<ILookupTypeCommandContext>();
        var factory = Factory<LookupTypeCommandState>();
        var actors = Substitute.For<IActorService>();
        var projector = Substitute.For<IEventProjector<LookupTypeCommandActor>>();
        var databases = Substitute.For<IDbContextFactory>();
        var reference = Substitute.For<IReferenceDbContext>();
        reference.ProviderName.Returns("System.Data.Postgres");
        reference.ConnectionString.Returns(fixture.ActorEventDb.ConnectionString);
        databases.ReferenceDb.Returns(reference);
        context.DbEventSource.Returns(fixture.ActorEventDb);
        context.StateFactory.Returns(factory);
        context.ActorService.Returns(actors);
        context.EventProjector.Returns(projector);
        context.Logger.Returns(NullLogger<LookupTypeCommandActor>.Instance);
        context.DbFactory.Returns(databases);
        var transactions = Transactions();
        await transactions.ExecuteOnceAsync((db, token) => db.ExecuteAsync(ReferenceSchemaSql.Create, [], token));
        var repository = new LookupTypeStateRepository(context, transactions);
        var name = "lookup-snapshot-" + Guid.NewGuid().ToString("N");
        await Verify<LookupTypeCommandState, LookupTypeStateSnapshottedEvent>(
            repository, context, (i, commandId) =>
            {
                var entry = new LookupTypeReadModel(name, $"code-{i}", i, "value", DateTime.UnixEpoch, "test");
                var removed = new LookupTypeReadModel(name, "code-1", 1, "value", DateTime.UnixEpoch, "test").Id;
                return i == 127
                    ? new LookupTypeRemovedEvent { Id = Guid.NewGuid(), CommandId = commandId,
                        EntityId = removed, LookupTypeId = removed }
                    : new LookupTypeAddedEvent { Id = Guid.NewGuid(), CommandId = commandId, EntityId = entry.Id, LookupType = entry };
            },
            (full, snapshot, command) => Assert.Equivalent(full.CaptureSnapshot(command).Entries, snapshot.CaptureSnapshot(command).Entries));
        await projector.Received(1).DomainEventsProjectionAsync(Arg.Is<DomainEventCollection>(
            facts => facts.Count == 1 && facts[0] is LookupTypeRemovedEvent));
    }

    private IPostgresEventTransaction Transactions() =>
        new PostgresEventTransaction(new DbConnectionSettings()
            .Add(EventSourceActorDbContext.EventSourceActorDbConnection, fixture.ActorEventDb.ConnectionString, "System.Data.Postgres"));

    private IDbContextFactory SecuritiesFactory()
    {
        var factory = Substitute.For<IDbContextFactory>();
        var securities = Substitute.For<ISecuritiesDbContext>();
        securities.ProviderName.Returns("System.Data.Postgres");
        securities.ConnectionString.Returns(fixture.ActorEventDb.ConnectionString);
        factory.SecuritiesDb.Returns(securities);
        return factory;
    }

    private async Task Verify<TState, TSnapshot>(IEventSourceActorStateRepository<TState> repository,
        ICommandActorContext context, Func<int, Guid, IEvent> source, Action<TState, TState, ICommand> assertState)
        where TState : BaseEventSourceActorState<TState>, new()
        where TSnapshot : IEvent
    {
        var command = Substitute.For<ICommand>();
        var subject = new ActorSubject(ActorType.Command, "PeriodicMarketDataSnapshot", "Write", Guid.NewGuid().ToString("N"));
        command.Subject.Returns(subject);
        command.StreamId.Returns(subject.StreamId);
        var commandId = Guid.NewGuid();
        command.CommandId.Returns(commandId);
        command.EventSource.Returns("PeriodicMarketDataSnapshot");
        var database = fixture.ActorEventDb;
        await database.SaveEventsAsync(command.StreamId, Guid.NewGuid(),
            new DomainEventCollection(Enumerable.Range(0, 127).Select(i => source(i, Guid.NewGuid()))), 0, CancellationToken.None);
        var state = await repository.LoadStateAsync(command);
        Assert.Equal(127, state.CommittedStreamVersion);
        Assert.True(state.Update(source(127, commandId), command));
        await repository.SaveStateAsync(context, state, command);

        var stream = await database.GetEventStreamIdAsync(command.StreamId, CancellationToken.None);
        var versions = new List<long>();
        await database.MapReduceActorEventStreamFromCreationWithCheckpointsAsync<TState>(
            stream, [typeof(TSnapshot)], rows => versions.AddRange(rows.Select(row => row.StreamVersion)));
        Assert.Equal(new long[] { 1, 129 }, versions);

        // A further mutation must replay on top of the snapshot.
        await database.SaveEventsAsync(command.StreamId, Guid.NewGuid(), new DomainEventCollection([source(128, Guid.NewGuid())]),
            129, CancellationToken.None);
        var full = new List<double>();
        var checkpoint = new List<double>();
        for (var i = 0; i < 20; i++)
        {
            TState baseline;
            TState restored;
            if (i % 2 == 0)
            {
                baseline = await ReplayFull();
                restored = await Restore();
            }
            else
            {
                restored = await Restore();
                baseline = await ReplayFull();
            }

            Assert.Equal(130, restored.CommittedStreamVersion);
            Assert.Empty(restored.Events);
            assertState(baseline, restored, command);
            assertState(ReferenceState(), restored, command);
        }

        full.Sort();
        checkpoint.Sort();
        output.WriteLine($"{typeof(TState).Name}: 130 -> 3 rows, " +
            $"median {(full[9] + full[10]) / 2:F2} -> {(checkpoint[9] + checkpoint[10]) / 2:F2} ms, " +
            $"p95 {full[18]:F2} -> {checkpoint[18]:F2} ms.");

        TState ReferenceState()
        {
            var reference = new TState { Id = subject.ThreadId };
            reference.ReplayEvents(Enumerable.Range(0, 129).Select(i => source(i, Guid.NewGuid())));
            return reference;
        }

        async Task<TState> ReplayFull()
        {
            var start = Stopwatch.GetTimestamp();
            var id = await database.GetEventStreamIdAsync(command.StreamId, CancellationToken.None);
            var baseline = new TState { Id = subject.ThreadId };
            await database.MapReduceActorEventStreamFromCreationAsync<TState>(id, baseline.ReplayEvents);
            full.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            return baseline;
        }

        async Task<TState> Restore()
        {
            var start = Stopwatch.GetTimestamp();
            var restored = await repository.LoadStateAsync(command);
            checkpoint.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            return restored;
        }
    }

    private static IEventSourceActorStateFactory Factory<TState>() where TState : BaseEventSourceActorState<TState>, new()
    {
        var factory = Substitute.For<IEventSourceActorStateFactory>();
        factory.CreateState<TState>().Returns(_ => new TState());
        return factory;
    }

    private static IDbContextFactory DatabaseFactory()
    {
        var factory = Substitute.For<IDbContextFactory>();
        var marketData = Substitute.For<IMarketDataDbContext>();
        factory.MarketDataDb.Returns(marketData);
        return factory;
    }
}
