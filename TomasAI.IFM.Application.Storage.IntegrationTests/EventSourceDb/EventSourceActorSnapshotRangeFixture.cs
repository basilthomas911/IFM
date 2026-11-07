using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.EventSourceDb.EventLog;
using TomasAI.IFM.Application.Storage.EventSourceDb.Schema;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.IntegrationTesting;
using TomasAI.IFM.Shared.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.EventSourceDb;

public sealed partial class EventSourceActorSnapshotRangeFixture : IAsyncLifetime
{
    readonly IsolatedIntegrationInfrastructure _infrastructure;
    readonly bool _ownsInfrastructure;
    IDbConnectionSettings _connectionSettings = null!;
    IBlackboardService _blackboard = null!;
    ILogger<DbProvider> _logger = null!;
    string _authenticatedConnectionString = string.Empty;

    public EventSourceActorSnapshotRangeFixture()
        : this(new IsolatedIntegrationInfrastructure("eventsource-snapshot-range"), true) { }

    internal EventSourceActorSnapshotRangeFixture(IsolatedIntegrationInfrastructure infrastructure)
        : this(infrastructure, false) { }

    EventSourceActorSnapshotRangeFixture(
        IsolatedIntegrationInfrastructure infrastructure,
        bool ownsInfrastructure)
    {
        _infrastructure = infrastructure;
        _ownsInfrastructure = ownsInfrastructure;
    }

    public async Task InitializeAsync()
    {
        if (_ownsInfrastructure)
            await _infrastructure.StartAsync();
        var baseConnection = new Npgsql.NpgsqlConnectionStringBuilder(_infrastructure.PostgresConnectionString)
        {
            Username = string.Empty,
            Password = string.Empty
        }.ConnectionString;
        _authenticatedConnectionString = new Npgsql.NpgsqlConnectionStringBuilder(_infrastructure.PostgresConnectionString)
        {
            Username = _infrastructure.PostgresUser,
            Password = _infrastructure.PostgresPassword
        }.ConnectionString;
        _connectionSettings = new DbConnectionSettings()
            .Add("EventSourceActorDbConnection", baseConnection, "System.Data.Postgres");
        var repositories = new Dictionary<Type, object>();
        var resolver = new DbContextResolver(type => repositories[type]);
        DbFactory = new DbContextFactory(resolver);
        _logger = Substitute.For<ILogger<DbProvider>>();
        await CreateSchemaDb().CreateAllAsync();

        var cache = Substitute.For<IRedisCache>();
        var cacheValues = new System.Collections.Concurrent.ConcurrentDictionary<string, string>();
        cache.TryGet(Arg.Any<string>(), out Arg.Any<string>()).Returns(call =>
        {
            var found = cacheValues.TryGetValue(call.ArgAt<string>(0), out var value);
            call[1] = value!;
            return found;
        });
        cache.When(instance => instance.Set(Arg.Any<string>(), Arg.Any<string>()))
            .Do(call => cacheValues[call.ArgAt<string>(0)] = call.ArgAt<string>(1));
        _blackboard = new BlackboardService(cache, new SystemTextJsonSerializer());

        ActorEventDb = CreateActorEventDb();
        repositories.Add(typeof(IObjectRepository<EventSourceActorDbContext>), ActorEventDb);
    }

    public async Task DisposeAsync()
    {
        if (ActorEventDb is not null)
            await ActorEventDb.DisposeAsync();
        if (_ownsInfrastructure)
            await _infrastructure.DisposeAsync();
    }

    public DbContextFactory DbFactory { get; private set; } = null!;
    public EventSourceActorDbContext ActorEventDb { get; private set; } = null!;
    public IBlackboardService Blackboard => _blackboard;
    public string ConnectionString => _authenticatedConnectionString;

    internal EventSourceSchemaDb CreateSchemaDb() => new(_connectionSettings, _logger);

    public EventSourceActorDbContext CreateActorEventDb(EventLogPersistenceOptions? options = null)
        => new(_connectionSettings, DbFactory, _blackboard, _logger, options);
}
