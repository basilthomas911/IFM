using System.Text.Json;
using Npgsql;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.EventSourceDb;

/// <summary>Checks bounded creation/checkpoint seeks against dense PostgreSQL source history.</summary>
public sealed class CreationSnapshotQueryPlanTests(EventSourceActorSnapshotRangeFixture fixture)
    : IClassFixture<EventSourceActorSnapshotRangeFixture>
{
    [Fact]
    public async Task Explicit_fast_paths_seek_creation_and_latest_without_materializing_the_dense_history()
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using (var setup = new NpgsqlCommand("""
            CREATE TEMP TABLE creation_plan_events (
                EventStreamId bigint, EventNameId integer, EventVersion bigint, EventPayload bytea,
                CommandId uuid, EventTimestamp timestamptz, StreamVersion bigint,
                PRIMARY KEY(EventStreamId,StreamVersion));
            CREATE INDEX creation_plan_checkpoint ON creation_plan_events(EventStreamId,EventNameId,EventVersion DESC) INCLUDE(StreamVersion);
            CREATE TEMP TABLE creation_plan_streams(EventStreamId bigint PRIMARY KEY,CurrentVersion bigint);
            CREATE TEMP TABLE creation_plan_names(EventNameId integer PRIMARY KEY,EventName text,EventTypeName text);
            INSERT INTO creation_plan_streams VALUES(1,100000),(2,1);
            INSERT INTO creation_plan_names VALUES(7,'Fact','Fact');
            INSERT INTO creation_plan_events
                SELECT 1,7,i,decode('00','hex'),'00000000-0000-0000-0000-000000000001','2026-11-03',i
                FROM generate_series(1,100000) i;
            INSERT INTO creation_plan_events VALUES(2,7,100001,decode('00','hex'),'00000000-0000-0000-0000-000000000001','2026-11-03',1);
            ANALYZE creation_plan_events; ANALYZE creation_plan_streams; ANALYZE creation_plan_names;
            SET plan_cache_mode=force_generic_plan;
            """, connection))
            await setup.ExecuteNonQueryAsync();
        foreach (var (name, source, parameters, arguments) in new[]
        {
            ("checkpoint", EventSourceDbSql.GetEventLogFromCreationWithCheckpoint, "bigint,integer[]", "1,ARRAY[7]"),
            ("metadata", EventSourceDbSql.GetStatelessEventLogFromCreationSnapshot, "bigint", "1")
        })
        {
            var sql = source.Replace("event_log", "creation_plan_events", StringComparison.Ordinal)
                .Replace("event_stream_id", "creation_plan_streams", StringComparison.Ordinal)
                .Replace("event_name_id", "creation_plan_names", StringComparison.Ordinal);
            await using (var prepare = new NpgsqlCommand($"PREPARE creation_{name}({parameters}) AS {sql}", connection))
                await prepare.ExecuteNonQueryAsync();
            await using (var query = new NpgsqlCommand($"EXECUTE creation_{name}({arguments})", connection))
            await using (var reader = await query.ExecuteReaderAsync())
            {
                var versions = new List<long>();
                while (await reader.ReadAsync()) versions.Add(reader.GetInt64(7));
                Assert.Equal(new long[] { 1, 100000 }, versions);
            }
            await using var explain = new NpgsqlCommand($"EXPLAIN (ANALYZE,BUFFERS,FORMAT JSON) EXECUTE creation_{name}({arguments})", connection);
            using var document = JsonDocument.Parse((string)(await explain.ExecuteScalarAsync())!);
            var scans = Nodes(document.RootElement[0].GetProperty("Plan"))
                .Where(node => node.TryGetProperty("Relation Name", out var table) && table.GetString() == "creation_plan_events").ToArray();
            Assert.NotEmpty(scans);
            Assert.All(scans, scan => Assert.True(scan.GetProperty("Actual Rows").GetDouble() <= 1,
                $"{name} scanned dense source history instead of seeking the creation/checkpoint endpoints."));
        }
    }

    static IEnumerable<JsonElement> Nodes(JsonElement node)
    {
        yield return node;
        if (node.TryGetProperty("Plans", out var children))
            foreach (var child in children.EnumerateArray())
                foreach (var descendant in Nodes(child)) yield return descendant;
    }
}
