using Npgsql;
using NpgsqlTypes;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.EventSourceDb.Schema;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.EventSourceDb;

/// <summary>Exercises the production snapshot query against real PostgreSQL,
/// including generic prepared plans and a large unrelated event population.</summary>
public sealed class SnapshotLookupPlanTests(EventSourceActorSnapshotRangeFixture fixture)
    : IClassFixture<EventSourceActorSnapshotRangeFixture>
{
    [Fact]
    public async Task Generic_snapshot_lookup_is_stream_scoped_and_preserves_alias_and_empty_results()
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        const string table = "snapshot_lookup_plan";
        var sql = EventSourceDbSql.GetMaxEventVersion.Replace("event_log", table, StringComparison.Ordinal);
        await using (var setup = new NpgsqlCommand("""
            CREATE TEMP TABLE snapshot_lookup_plan (
                eventstreamid bigint, eventnameid integer, eventversion bigint, streamversion bigint);
            INSERT INTO snapshot_lookup_plan
                SELECT 10000+i%10000,9,1000+i,1+i/10000 FROM generate_series(1,100000) i;
            INSERT INTO snapshot_lookup_plan VALUES
                (1,7,1,1),(1,7,2,2),(1,11,4,3),(1,99,5,4),(3,7,6,1);
            CREATE UNIQUE INDEX probe_event_version ON snapshot_lookup_plan(eventversion);
            CREATE INDEX probe_stream_version ON snapshot_lookup_plan(eventstreamid,streamversion);
            """ + EventSourceSchemaSql.CreateSnapshotLookupIndex.Replace("public.event_log", table, StringComparison.Ordinal)
            + "; ANALYZE snapshot_lookup_plan; SET plan_cache_mode=force_generic_plan;", connection))
            await setup.ExecuteNonQueryAsync();

        // The same numeric identity can be stored under old and new type names.
        // Neither duplicate aliases nor a later unrelated event may change max.
        foreach (var (stream, names, expected) in new (long, int[], long?)[] {
            (1,[7,11],4),(1,[7,11,11],4),(1,[7],2),(1,[77],null),
            (2,[7,11],null),(1,[],null),(3,[7,11],6)
        })
        {
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue(NpgsqlDbType.Bigint, stream);
            command.Parameters.AddWithValue(NpgsqlDbType.Array | NpgsqlDbType.Integer, names);
            await command.PrepareAsync();
            var value = await command.ExecuteScalarAsync();
            Assert.Equal(expected, value is DBNull or null ? null : (long?)Convert.ToInt64(value));
        }

        // The missing-snapshot case must not scan the global version index.
        await using (var prepare = new NpgsqlCommand("PREPARE snapshot_plan(bigint,integer[]) AS " + sql, connection))
            await prepare.ExecuteNonQueryAsync();
        await using var explain = new NpgsqlCommand(
            "EXPLAIN (ANALYZE,BUFFERS) EXECUTE snapshot_plan(2,ARRAY[7,11])", connection);
        await using var reader = await explain.ExecuteReaderAsync();
        var lines = new List<string>();
        while (await reader.ReadAsync()) lines.Add(reader.GetString(0));
        var plan = string.Join('\n', lines);
        Assert.Contains("snapshot_versions", plan);
        Assert.Contains("Index Cond:", plan);
        Assert.Contains("eventstreamid = $1", plan);
        Assert.DoesNotContain("probe_event_version", plan);
    }
}
