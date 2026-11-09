using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NSubstitute;
using Xunit;
using FluentAssertions;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using Microsoft.Extensions.Logging;

namespace TomasAI.IFM.Framework.Storage.IntegratedTests;

public class EventDatabaseFixture : IAsyncLifetime
{
    readonly TomasAI.IFM.IntegrationTesting.IsolatedIntegrationInfrastructure infrastructure = new("redisfree-transactions", includeCql: false);

    public async Task InitializeAsync()
    {
        await infrastructure.StartAsync();
        var dbConn = new DbConnectionSettings()
             .Add("EventSourceActorDbConnection", infrastructure.PostgresConnectionString, "System.Data.Postgres");
        var diContainer = new Dictionary<Type, EventSourceActorDbContext>();
        var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
        DbFactory = new DbContextFactory(dbResolver);
        var dbCache = new DbCache();
        var logger = Substitute.For<ILogger<DbProvider>>();
        logger.When(_ => { }).Do(_ => { });


        await new TomasAI.IFM.Application.Storage.EventSourceDb.Schema.EventSourceSchemaDb(dbConn, logger).CreateAllAsync();
        diContainer.Add(typeof(IObjectRepository<EventSourceActorDbContext>), new EventSourceActorDbContext(dbConn, DbFactory, logger));
        Db = DbFactory.ActorEventSourceDb as EventSourceActorDbContext;
    }

    public EventSourceActorDbContext Db { get; private set; } = null!;

    public IDbContextFactory DbFactory { get; private set; } = null!;

    public async Task DisposeAsync()
    {
        if (Db is not null) await Db.DisposeAsync();
        await infrastructure.DisposeAsync();
    }
}

public class ObjectDataRepositoryTransactionTests(EventDatabaseFixture fixture) : IClassFixture<EventDatabaseFixture>
{
    readonly EventDatabaseFixture _fixture = fixture;

    [Fact]
    [Trait("IntegrationTest", "Create transaction and commit data successfully")]
    public async Task ObjectDataRepository_CommitOk()
    {
        var db = _fixture.Db;
        var commandId = Guid.NewGuid();

        try
        {
            (await CountCommandAsync(commandId)).Should().Be(0);

            var tx = db.BeginTransaction();
            tx.Should().NotBeNull();
            var transactionCompleted = false;
            try
            {
                await InsertCommandAsync(commandId);
                tx!.Commit();
                transactionCompleted = true;
            }
            finally
            {
                if (!transactionCompleted)
                    tx?.Rollback();
            }

            (await CountCommandAsync(commandId)).Should().Be(1);
        }
        finally
        {
            await DeleteCommandAsync(commandId);
        }
    }

    [Fact]
    [Trait("IntegrationTest", "Create transaction and rollback data successfully")]
    public async Task ObjectDataRepository_RollbackOk()
    {
        var db = _fixture.Db;
        var commandId = Guid.NewGuid();

        try
        {
            (await CountCommandAsync(commandId)).Should().Be(0);

            var tx = db.BeginTransaction();
            tx.Should().NotBeNull();
            await InsertCommandAsync(commandId);
            tx!.Rollback();

            (await CountCommandAsync(commandId)).Should().Be(0);
        }
        finally
        {
            await DeleteCommandAsync(commandId);
        }
    }

    async Task InsertCommandAsync(Guid commandId)
        => await _fixture.Db.UseTest($"""
            insert into command_log (
                commandid, streamid, actorname, commandname,
                commandtimestamp, commandstatus, commanddata
            ) values (
                '{commandId}', 'storage-integrated-tests', 'StorageTests', 'TestTransaction',
                '{DateTime.UtcNow:o}', 'Processing', 'test-data'
            )
            """).ExecuteCommandAsync();

    async Task DeleteCommandAsync(Guid commandId)
        => await _fixture.Db.UseTest($"delete from command_log where commandid = '{commandId}'").ExecuteCommandAsync();

    Task<int> CountCommandAsync(Guid commandId)
        => _fixture.Db.UseTest($"select count(*)::integer from command_log where commandid = '{commandId}'")
            .ExecuteScalarAsync(MapToInt);

    static int MapToInt(IObjectDataRecord record)
        => record.GetInt(0);
}
