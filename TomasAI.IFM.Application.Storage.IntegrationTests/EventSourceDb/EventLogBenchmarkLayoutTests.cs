using System;
using TomasAI.IFM.Application.Storage.EventSourceDb.Persistence;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.EventSourceDb;

public sealed class EventLogBenchmarkLayoutTests
{
    [Fact]
    public void Production_layout_is_unchanged()
    {
        Assert.False(EventLogSqlLayout.Current.BatchProjectionMarkers);
        const string sql = "COPY event_log FROM STDIN; SELECT nextval('public.event_log_eventversion_seq');";
        Assert.Equal(sql, EventLogSqlLayout.Current.Resolve(sql));
    }

    [Theory]
    [InlineData("Host=127.0.0.1;Database=event-source-dev-db")]
    [InlineData("Host=127.0.0.1;Database=event-source-test-db")]
    [InlineData("Host=production;Database=ifm_eventlog_bench_123456abcdef_test")]
    [InlineData("Host=127.0.0.1;Database=ifm_eventlog_bench_test")]
    [InlineData("Host=127.0.0.1;Database=ifm_eventlog_bench_123456abcdef_test;Host=remote")]
    public void Benchmark_layout_rejects_non_owned_database_or_remote_host(string connection)
    {
        Assert.Throws<ArgumentException>(() => EventLogSqlLayout.ForBenchmark(connection, true));
    }

    [Fact]
    public void Authoritative_layout_keeps_table_sequence_and_index_identifiers()
    {
        var layout = EventLogSqlLayout.ForBenchmark(
            "Host=127.0.0.1;Database=ifm_eventlog_bench_123456abcdef_test", true);
        Assert.Equal(
            "COPY event_log FROM STDIN; SELECT * FROM public.event_log; SELECT nextval('public.event_log_eventversion_seq'); -- ux_event_log_event_version",
            layout.Resolve("COPY event_log FROM STDIN; SELECT * FROM public.event_log; SELECT nextval('public.event_log_eventversion_seq'); -- ux_event_log_event_version"));
    }

    [Fact]
    public void Benchmark_layout_uses_identical_sql_to_production()
    {
        var layout = EventLogSqlLayout.ForBenchmark(
            "Host=127.0.0.1;Database=ifm_eventlog_bench_123456abcdef_test", false);
        Assert.Equal("INSERT INTO event_log", layout.Resolve("INSERT INTO event_log"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Benchmark_layout_cannot_be_reused_against_an_application_database(bool copy)
    {
        var layout = EventLogSqlLayout.ForBenchmark(
            "Host=127.0.0.1;Database=ifm_eventlog_bench_123456abcdef_test", true);
        const string application = "Host=127.0.0.1;Database=event-source-dev-db";
        if (copy)
            Assert.Throws<ArgumentException>(() => new BinaryCopyEventLogAppender(application, false, null, layout));
        else
            Assert.Throws<ArgumentException>(() => new SequentialEventLogAppender(application, false, null, layout));
    }

    [Fact]
    public void Marker_batching_is_benchmark_only_even_without_a_table_rename()
    {
        var layout = EventLogSqlLayout.ForBenchmark(
            "Host=127.0.0.1;Database=ifm_eventlog_bench_123456abcdef_test", batchProjectionMarkers: true);
        Assert.True(layout.BatchProjectionMarkers);
        Assert.Equal("COPY event_log", layout.Resolve("COPY event_log"));
        Assert.Throws<ArgumentException>(() => new BinaryCopyEventLogAppender(
            "Host=127.0.0.1;Database=event-source-dev-db", false, null, layout));
    }

    [Fact]
    public async System.Threading.Tasks.Task Empty_marker_batch_does_not_access_a_connection()
    {
        await BatchedProjectionMarkerWriter.InsertAsync(null!, null!, Array.Empty<ProjectionMarker>(),
            BatchedProjectionMarkerWriter.Sql, default);
    }
}
