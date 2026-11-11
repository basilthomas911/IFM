using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using Npgsql;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.EventSourceDb;

public sealed class AggregateCheckpointSchemaTests(EventSourceActorSnapshotRangeFixture fixture)
    : IClassFixture<EventSourceActorSnapshotRangeFixture>
{
    [Fact]
    public async Task Startup_migration_restores_checkpoint_reads_on_an_existing_event_store()
    {
        var schema = fixture.CreateSchemaDb();

        // This test owns its PostgreSQL fixture. Reproduce the pre-upgrade schema.
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var drop = new NpgsqlCommand("DROP TABLE public.event_aggregate_checkpoint;", connection);
        await drop.ExecuteNonQueryAsync();

        var stream = $"CheckpointSchemaTest.{Guid.NewGuid():N}";
        Task<AggregateCheckpointWindow> ReadAsync() =>
            fixture.ActorEventDb.ReadAggregateCheckpointWindowAsync(stream, $"{stream}.Snapshot", CancellationToken.None);

        var failure = await Assert.ThrowsAsync<PostgresException>(ReadAsync);
        Assert.Equal(PostgresErrorCodes.UndefinedTable, failure.SqlState);

        await schema.EnsureAggregateCheckpointSchemaAsync();
        await schema.EnsureAggregateCheckpointSchemaAsync();

        var window = await ReadAsync();
        Assert.Equal(0, window.Head);
        Assert.Empty(window.Rows);
    }
}
