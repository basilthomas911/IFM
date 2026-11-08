using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NSubstitute;
using Xunit;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.EventSourceDb;

/// <summary>Exercises the production repository's atomic event/projection commit and latest full-state restore in disposable PostgreSQL.</summary>
public sealed class EmaSeedBatchSnapshotTests(EventSourceActorSnapshotRangeFixture fixture) : IClassFixture<EventSourceActorSnapshotRangeFixture>
{
    [Theory]
    [InlineData(21)]
    [InlineData(48)]
    [InlineData(64)]
    public async Task Batch_commits_matching_source_and_projection_rows_and_restores_latest_snapshot_without_duplicates(int count)
    {
        await SchemaAsync();
        var command = Command(count);
        var (repository, context, projector) = Repository();
        var state = await repository.LoadStateAsync(command);
        Assert.True(command.Execute(state).Success);
        await repository.SaveStateAsync(context, state, command);
        Assert.Equal(count, await CountAsync("SELECT count(*) FROM event_log WHERE CommandId = $1", command.CommandId));
        Assert.Equal(count, await CountAsync("SELECT count(*) FROM market_data.futures_ema_signal WHERE contractId = $1", command.Observations[0].ContractId));
        var restarted = await repository.LoadStateAsync(command);
        Assert.Equal(count, restarted.CommittedStreamVersion);
        Assert.Equal(state.FuturesEmaCheckpoint, restarted.FuturesEmaCheckpoint);
        Assert.Equivalent(state.FuturesEmaSignal, restarted.FuturesEmaSignal, strict: true);
        var stream = await fixture.ActorEventDb.GetEventStreamIdAsync(command.StreamId, default);
        var versions = new List<long>();
        await fixture.ActorEventDb.MapReduceActorEventStreamFromCreationWithCheckpointsAsync<FuturesEmaSignalCommandState>(
            stream, [typeof(FuturesEmaSignalGeneratedEvent)], rows => versions.AddRange(rows.Select(row => row.StreamVersion)), default);
        Assert.Equal(new long[] { 1, count }, versions);
        Assert.True(command.Execute(restarted).Success); Assert.Empty(restarted.Events);
        await repository.SaveStateAsync(context, restarted, command);
        Assert.Equal(count, await CountAsync("SELECT count(*) FROM event_log WHERE CommandId = $1", command.CommandId));
        await projector.Received(1).DomainEventsProjectionAsync(Arg.Any<DomainEventCollection>());
    }

    [Fact]
    public async Task Mid_batch_projection_failure_rolls_back_all_events_and_projections_then_a_restart_recovers_once()
    {
        await SchemaAsync();
        var command = Command(48);
        var contract = command.Observations[0].ContractId;
        var constraint = "perf_ema_guard_" + Guid.NewGuid().ToString("N");
        await ExecuteSqlAsync($"ALTER TABLE market_data.futures_ema_signal ADD CONSTRAINT {constraint} CHECK (contractId <> '{contract}' OR sourceSequence <> 25)");
        var (repository, context, _) = Repository();
        var state = await repository.LoadStateAsync(command);
        Assert.True(command.Execute(state).Success);
        await Assert.ThrowsAsync<PostgresException>(() => repository.SaveStateAsync(context, state, command).AsTask());
        Assert.Equal(0, await CountAsync("SELECT count(*) FROM event_log WHERE CommandId = $1", command.CommandId));
        Assert.Equal(0, await CountAsync("SELECT count(*) FROM market_data.futures_ema_signal WHERE contractId = $1", contract));
        await ExecuteSqlAsync($"ALTER TABLE market_data.futures_ema_signal DROP CONSTRAINT {constraint}");
        var recovered = await repository.LoadStateAsync(command);
        Assert.Null(recovered.FuturesEmaCheckpoint);
        Assert.True(command.Execute(recovered).Success);
        await repository.SaveStateAsync(context, recovered, command);
        Assert.Equal(48, await CountAsync("SELECT count(*) FROM event_log WHERE CommandId = $1", command.CommandId));
        Assert.Equal(48, await CountAsync("SELECT count(*) FROM market_data.futures_ema_signal WHERE contractId = $1", contract));
        Assert.Equal(48, (await repository.LoadStateAsync(command)).FuturesEmaCheckpoint!.Count);
    }

    (FuturesEmaSignalStateRepository Repository, ICommandActorContext Context, IEventProjector<FuturesEmaSignalCommandActor> Projector) Repository()
    {
        var factory = Substitute.For<IEventSourceActorStateFactory>();
        factory.CreateState<FuturesEmaSignalCommandState>().Returns(_ => new FuturesEmaSignalCommandState());
        var projector = Substitute.For<IEventProjector<FuturesEmaSignalCommandActor>>();
        var repository = new FuturesEmaSignalStateRepository(factory, fixture.ActorEventDb, Substitute.For<IActorService>(), projector,
            NullLogger<FuturesEmaSignalStateRepository>.Instance);
        var databases = Substitute.For<IDbContextFactory>();
        var market = Substitute.For<IMarketDataDbContext>();
        market.ProviderName.Returns("System.Data.Postgres"); market.ConnectionString.Returns(fixture.ActorEventDb.ConnectionString);
        databases.MarketDataDb.Returns(market);
        var context = Substitute.For<ICommandActorContext>(); var container = Substitute.For<IContainerInstance>();
        context.Container.Returns(container); container.Resolve<IDbContextFactory>().Returns(databases);
        container.Resolve<IPostgresEventTransaction>().Returns(new PostgresEventTransaction(new DbConnectionSettings()
            .Add(EventSourceActorDbContext.EventSourceActorDbConnection, fixture.ActorEventDb.ConnectionString, "System.Data.Postgres")));
        return (repository, context, projector);
    }

    Task SchemaAsync() => ExecuteSqlAsync(MarketDataSchemaSql.Create);
    async Task ExecuteSqlAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString); await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection); await command.ExecuteNonQueryAsync();
    }
    async Task<long> CountAsync(string sql, object value)
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString); await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection); command.Parameters.Add(new NpgsqlParameter { Value = value });
        return (long)(await command.ExecuteScalarAsync())!;
    }
    static SeedFuturesEmaSignalsCommand Command(int count)
    {
        var contract = "ES-perf-" + Guid.NewGuid().ToString("N");
        var series = MarketSeriesIdentity.ForContract(contract);
        var id = new FuturesTradeSessionBarEntityId(series, TimeFrameType.OneMinute);
        var start = new DateTimeOffset(2026, 11, 6, 15, 0, 0, TimeSpan.Zero);
        return new() { CommandId = Guid.NewGuid(), EntityId = id,
            Subject = new(ActorType.Command, SeedFuturesEmaSignalsCommand.Actor, SeedFuturesEmaSignalsCommand.Verb, id.Format()),
            Observations = Enumerable.Range(1, count).Select(index => new FuturesTradeSessionBarReadModel
            {
                MarketSeriesIdentity = series, ContractId = contract, ValueDate = new(2026, 11, 6), TimeFrame = TimeFrameType.OneMinute,
                ObservationId = FuturesTradeSessionBarId.Create(series, TimeFrameType.OneMinute, start.AddMinutes(index), index),
                IntervalStartUtc = start.AddMinutes(index - 1), IntervalEndUtc = start.AddMinutes(index),
                Open = 5000, High = 5002, Low = 4998, Close = 5000 + index / 100m, Volume = 1, TradeCount = 1, PriceVolumeSum = 5000 + index / 100m,
                FirstSourceSequence = index, LastSourceSequence = index, FirstMarketEventUtc = start.AddMinutes(index - 1).AddSeconds(1),
                LastMarketEventUtc = start.AddMinutes(index).AddMilliseconds(-1), CalculatedAtUtc = start.AddMinutes(index),
                IsComplete = true, IsValid = true, SchemaVersion = 1, CalculationVersion = "postgres-seed-test-v1", CalculationMethod = MarketSignalCalculationMethod.NormalizedHistoricalAggregate
            }).ToArray() };
    }
}
