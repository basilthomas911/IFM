using System;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using TomasAI.IFM.Application.Storage.ScheduledTaskDb;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Shared.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.FrameworkStorage.Postgres;

[Collection(PostgresStorageProviderCollection.Name)]
public sealed class PostgresDirectBindingTests(PostgresStorageProviderFixture fixture)
{
    [Fact]
    public async Task Direct_scheduled_task_and_copy_paths_round_trip_neutral_values_in_one_transaction()
    {
        await using var connection = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(fixture.Repository.ConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var create = new NpgsqlCommand("""
            CREATE TEMP TABLE neutral_binding_test (id bigint, payload bytea, label text,
                wall_clock timestamp, absolute_time timestamptz, tags text[], documents bytea[]);
            """, connection, transaction))
            await create.ExecuteNonQueryAsync();
        var wallClock = new DateTime(2026, 10, 30, 12, 34, 56, DateTimeKind.Unspecified);
        var absolute = DateTime.SpecifyKind(wallClock, DateTimeKind.Utc);
        object?[] values = [1L, new byte[] { 1, 2 }, BindValues.Text(null), BindValues.Timestamp(wallClock),
            absolute, Array.Empty<string>(), new byte[][] { [3, 4] }];
        await using (var insert = new NpgsqlCommand("INSERT INTO neutral_binding_test VALUES ($1,$2,$3,$4,$5,$6,$7)", connection, transaction))
        {
            insert.BindScheduledTaskParameters(values);
            Assert.Equal(1, await insert.ExecuteNonQueryAsync());
        }
        await using (var importer = await connection.BeginBinaryImportAsync("COPY neutral_binding_test FROM STDIN (FORMAT BINARY)"))
        {
            await PostgresValueBinding.WriteRowAsync(importer, values, CancellationToken.None);
            await importer.CompleteAsync();
        }
        await using var query = new NpgsqlCommand("""
            SELECT count(*) FROM neutral_binding_test WHERE id=@id AND payload=decode('0102','hex')
                AND label IS NULL AND wall_clock=@wall AND absolute_time=@absolute
                AND cardinality(tags)=0 AND documents=ARRAY[decode('0304','hex')];
            """, connection, transaction);
        PostgresValueBinding.AddNamedParameter(query, "id", 1L);
        PostgresValueBinding.AddNamedParameter(query, "wall", BindValues.Timestamp(wallClock));
        PostgresValueBinding.AddNamedParameter(query, "absolute", absolute);
        Assert.Equal(2L, await query.ExecuteScalarAsync());
        await transaction.RollbackAsync();
    }
}
