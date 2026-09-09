using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.Storage.CommandLogBenchmark;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Shared.Storage;
namespace TomasAI.IFM.Framework.Storage.Benchmarks;

internal static class EventLogTestCutover
{
    internal static async Task StatusAsync()
    {
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Test");
        await using var connection = new TomasAI.IFM.Framework.Storage.Postgres.PostgresObjectDataRepositoryConnection()
            .As<Npgsql.NpgsqlConnection>("Host=localhost;Port=5432;Database=event-source-test-db");
        await connection.OpenAsync();
        await using var command = new Npgsql.NpgsqlCommand("SELECT pid,state,coalesce(wait_event_type,'none'),coalesce(wait_event,'none'),clock_timestamp()-query_start FROM pg_stat_activity WHERE datname=current_database() AND pid<>pg_backend_pid()", connection);
        await using var reader = await command.ExecuteReaderAsync();
        while(await reader.ReadAsync()) Console.WriteLine($"pid={reader.GetInt32(0)} {reader.GetString(1)} {reader.GetString(2)} {reader.GetString(3)} {reader.GetTimeSpan(4)}");
    }

    internal static async Task ResetAsync()
    {
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Test");
        var settings = new DbConnectionSettings().Add(EventSourceActorDbContext.EventSourceActorDbConnection,
            "Host=localhost;Port=5432;Database=event-source-test-db", "System.Data.Postgres");
        await using (var connection = new TomasAI.IFM.Framework.Storage.Postgres.PostgresObjectDataRepositoryConnection()
            .As<Npgsql.NpgsqlConnection>(settings[EventSourceActorDbContext.EventSourceActorDbConnection].ConnectionString))
        {
            connection.Notice += (_, e) => Console.WriteLine(e.Notice.MessageText);
            await connection.OpenAsync();
            await using var command = new Npgsql.NpgsqlCommand(File.ReadAllText("scripts/Reset-TestEventLogToBinary.sql"), connection)
                { CommandTimeout = 600 };
            await command.ExecuteNonQueryAsync();
        }
        await InspectAsync();
    }

    internal static async Task InspectAsync()
    {
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Test");
        var settings = new DbConnectionSettings().Add(EventSourceActorDbContext.EventSourceActorDbConnection,
            "Host=localhost;Port=5432;Database=event-source-test-db", "System.Data.Postgres");
        var store = new PostgresCommandLogBenchmarkStore(settings, NullLogger<TomasAI.IFM.Framework.Storage.DbProvider>.Instance);
        var rows = await store.Database.Use("EventLogCutover.Inspect", """
            SELECT 'database'::text, current_database()::text
            UNION ALL SELECT 'events', count(*)::text FROM event_log
            UNION ALL SELECT 'financial_history_receipt', count(*)::text FROM portfolio_financial.financial_history_receipt
            UNION ALL SELECT 'financial_operation_receipt', count(*)::text FROM portfolio_financial.financial_operation_receipt
            UNION ALL SELECT 'foreign-key', conrelid::regclass::text || ' ' || pg_get_constraintdef(oid)
              FROM pg_constraint WHERE confrelid='event_log'::regclass
            UNION ALL SELECT 'column', column_name || ' ' || data_type FROM information_schema.columns
              WHERE table_schema='public' AND table_name='event_log'
            UNION ALL SELECT 'active-client', application_name || ' ' || state FROM pg_stat_activity
              WHERE datname=current_database() AND pid<>pg_backend_pid();
            """).ExecuteQueryAsync(r => r.GetString(0) + ": " + r.GetString(1));
        foreach (var row in rows) Console.WriteLine(row);
    }
}
