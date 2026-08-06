using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.EventSourceDb.Schema;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using Xunit;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.FrameworkStorage.Postgres;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PostgresStorageProviderCollection : ICollectionFixture<PostgresStorageProviderFixture>
{
    public const string Name = "Framework.Storage PostgreSQL integration";
}

public sealed class PostgresStorageProviderFixture : IAsyncLifetime
{
    const string ConnectionVariable = "IFM_POSTGRES_EVENTSOURCE_TEST_CONNECTION";
    const string ProviderName = "System.Data.Postgres";

    readonly ILogger<DbProvider> _logger = Substitute.For<ILogger<DbProvider>>();

    public PostgresTestRepository Repository { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Set {ConnectionVariable} to a credential-free PostgreSQL connection string whose database is dedicated to integration tests.");
        }

        var settings = new DbConnectionSettings()
            .Add(EventSourceActorDbContext.EventSourceActorDbConnection, connectionString, ProviderName);

        await new EventSourceSchemaDb(settings, _logger).CreateAllAsync();
        Repository = new PostgresTestRepository(settings[EventSourceActorDbContext.EventSourceActorDbConnection], _logger);

        await CleanupAllScopesAsync();
    }

    public async Task DisposeAsync()
    {
        if (Repository is null)
            return;

        await CleanupAllScopesAsync();
    }

    internal async Task RunIsolatedAsync(PostgresEventSourceTestScope scope, Func<PostgresTestRepository, Task> test)
    {
        await CleanupAndVerifyAsync(scope);
        try
        {
            await test(Repository);
        }
        finally
        {
            await CleanupAndVerifyAsync(scope);
        }
    }

    async Task CleanupAllScopesAsync()
    {
        for (var slot = 1; slot <= PostgresEventSourceTestData.SlotCount; slot++)
            await CleanupAndVerifyAsync(PostgresEventSourceTestData.Scope(slot));
    }

    async Task CleanupAndVerifyAsync(PostgresEventSourceTestScope scope)
    {
        await Repository.Use("DELETE FROM event_projector_state WHERE eventid = $1 OR eventid = $2;")
            .SetParameters(new EventVersions(scope.EventVersion, scope.SecondEventVersion))
            .ExecuteCommandAsync();

        await Repository.Use("""
                DELETE FROM event_log
                WHERE eventstreamid = $1 OR eventstreamid = $2 OR eventversion = $3 OR eventversion = $4;
                """)
            .SetParameters(new EventLogKeys(
                scope.EventStreamId,
                scope.SecondEventStreamId,
                scope.EventVersion,
                scope.SecondEventVersion))
            .ExecuteCommandAsync();

        await Repository.Use("DELETE FROM command_log WHERE commandid = $1;")
            .SetParameters(new CommandKey(scope.CommandId))
            .ExecuteCommandAsync();

        await Repository.Use("DELETE FROM event_name_id WHERE eventnameid = $1;")
            .SetParameters(new EventNameKey(scope.EventNameId))
            .ExecuteCommandAsync();

        await Repository.Use("""
                DELETE FROM event_stream_id
                WHERE eventstreamid = $1 OR eventstreamid = $2 OR eventstream = $3 OR eventstream = $4;
                """)
            .SetParameters(new EventStreamKeys(
                scope.EventStreamId,
                scope.SecondEventStreamId,
                scope.EventStream,
                scope.SecondEventStream))
            .ExecuteCommandAsync();

        await EnsureEmptyAsync(
            "SELECT count(*) FROM event_projector_state WHERE eventid = $1 OR eventid = $2;",
            new EventVersions(scope.EventVersion, scope.SecondEventVersion),
            "event_projector_state");
        await EnsureEmptyAsync(
            "SELECT count(*) FROM event_log WHERE eventstreamid = $1 OR eventstreamid = $2 OR eventversion = $3 OR eventversion = $4;",
            new EventLogKeys(scope.EventStreamId, scope.SecondEventStreamId, scope.EventVersion, scope.SecondEventVersion),
            "event_log");
        await EnsureEmptyAsync(
            "SELECT count(*) FROM command_log WHERE commandid = $1;",
            new CommandKey(scope.CommandId),
            "command_log");
        await EnsureEmptyAsync(
            "SELECT count(*) FROM event_name_id WHERE eventnameid = $1;",
            new EventNameKey(scope.EventNameId),
            "event_name_id");
        await EnsureEmptyAsync(
            "SELECT count(*) FROM event_stream_id WHERE eventstreamid = $1 OR eventstreamid = $2 OR eventstream = $3 OR eventstream = $4;",
            new EventStreamKeys(scope.EventStreamId, scope.SecondEventStreamId, scope.EventStream, scope.SecondEventStream),
            "event_stream_id");
    }

    async Task EnsureEmptyAsync<TParam>(string sql, TParam parameters, string table)
        where TParam : struct, IBindValue
    {
        var count = await Repository.Use(sql)
            .SetParameters(parameters)
            .ExecuteScalarAsync(static row => row.GetLong(0));

        if (count != 0)
            throw new InvalidOperationException($"PostgreSQL integration cleanup left {count} row(s) in {table}.");
    }

    readonly record struct EventVersions(long eventVersion, long secondEventVersion) : IBindValue
    {
        public object Bind() => Values(Bigint(eventVersion), Bigint(secondEventVersion));
    }

    readonly record struct EventLogKeys(
        int eventStreamId,
        int secondEventStreamId,
        long eventVersion,
        long secondEventVersion) : IBindValue
    {
        public object Bind() => Values(Integer(eventStreamId), Integer(secondEventStreamId), Bigint(eventVersion), Bigint(secondEventVersion));
    }

    readonly record struct CommandKey(Guid commandId) : IBindValue
    {
        public object Bind() => Values(Uuid(commandId));
    }

    readonly record struct EventNameKey(int eventNameId) : IBindValue
    {
        public object Bind() => Values(Integer(eventNameId));
    }

    readonly record struct EventStreamKeys(
        int eventStreamId,
        int secondEventStreamId,
        string eventStream,
        string secondEventStream) : IBindValue
    {
        public object Bind() => Values(Integer(eventStreamId), Integer(secondEventStreamId), Text(eventStream), Text(secondEventStream));
    }
}

public sealed class PostgresTestRepository(
    IDbConnectionSetting connectionSetting,
    ILogger<DbProvider> logger)
    : ObjectDataRepository<PostgresTestRepository>(connectionSetting, logger)
{
    public override IObjectRepository Database => this;
}
