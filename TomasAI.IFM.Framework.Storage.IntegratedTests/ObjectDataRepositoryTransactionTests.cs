using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NSubstitute;
using Xunit;
using FluentAssertions;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using Microsoft.Extensions.Logging;

namespace TomasAI.IFM.Framework.Storage.IntegratedTests;

public class EventDatabaseFixture : IDisposable
{
    public EventDatabaseFixture()
    {
        var dbConn = new DbConnectionSettings()
             .Add("EventSourceDbConnection", "Host=localhost;Port=5432;Username=postgres;Password=monkey35907;Database=event-source-test-db", "System.Data.Postgres");
        var diContainer = new Dictionary<Type, EventSourceDbContext>();
        var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
        DbFactory = new DbContextFactory(dbResolver);
        var dbCache = new DbCache();
        var logger = Substitute.For<ILogger<DbProvider>>();
        logger.When(_ => { }).Do(_ => { });
        var blackboardService = Substitute.For<IBlackboardService>();
        blackboardService.When(_ => { }).Do(_ => { });

        diContainer.Add(typeof(IObjectRepository<EventSourceDbContext>), new EventSourceDbContext(dbConn, DbFactory, blackboardService, logger));
        Db = DbFactory.EventSourceDb as EventSourceDbContext;
    }

    public EventSourceDbContext Db { get; }

    public IDbContextFactory DbFactory { get; }

    public void Dispose()
    {
    }
}

public class ObjectDataRepositoryTransactionTests(EventDatabaseFixture fixture) : IClassFixture<EventDatabaseFixture>
{
    readonly EventDatabaseFixture _fixture = fixture;

  
    [Fact]
    [Trait("UnitTest","Create transaction and commit data successfully")]
    public async Task ObjectDataRepository_CommitOk()
    {
        var db = _fixture.Db;
        await db.Use("delete from event_log").ExecuteCommandAsync();
        var rowCount = await db.Use($"select count(*) from event_log").ExecuteScalarAsync(MapToInt);
        rowCount.Should().Be(0);
        var tx = db.BeginTransaction();
        tx.Should().NotBeNull();
        await InsertEventLogAsync(1, 2, Guid.NewGuid());
        tx.Commit();
        rowCount = await db.Use($"select count(*) from event_log").ExecuteScalarAsync(MapToInt);
        rowCount.Should().Be(1);
        await db.Use("delete from event_log").ExecuteCommandAsync();
        return;

        async Task InsertEventLogAsync(long eventSourceId, long eventSourceVersion, Guid commandId)
        {
            var sql = $"exec spInsertEventLog @eventSourceId = {eventSourceId}, @eventSourceVersion = {eventSourceVersion},  @eventTypeId = 99, @eventData = 'The Rain In Spain', @eventDate = '{DateTime.Now:yyyy-MM-dd}', @commandId = '{commandId}'";
            await db.Use(sql).ExecuteCommandAsync();
        }
    }

    [Fact]
    [Trait("UnitTest", "Create transaction and rollback data successfully")]
    public async Task ObjectDataRepository_RollbackOk()
    {
        var db = _fixture.Db;
        await db.Use("delete from event_log").ExecuteCommandAsync();
        await InsertEventLogAsync(1, 2, Guid.NewGuid());
        var rowCount = await db.Use($"select count(*) from event_log").ExecuteScalarAsync(MapToInt);
        rowCount.Should().Be(1);
        var tx = db.BeginTransaction();
        tx.Should().NotBeNull();
        await InsertEventLogAsync(2, 3, Guid.NewGuid());
        tx.Rollback();
        rowCount = await db.Use($"select count(*) from event_log").ExecuteScalarAsync(MapToInt);
        rowCount.Should().Be(1);
        await db.Use("delete from event_log").ExecuteCommandAsync();
        return;

        async Task InsertEventLogAsync(long eventSourceId, long eventSourceVersion, Guid commandId)
        {
            var sql = $"exec spInsertEventLog @eventSourceId = {eventSourceId}, @eventSourceVersion = {eventSourceVersion},  @eventTypeId = 99, @eventData = 'The Rain In Spain', @eventDate = '{DateTime.Now:yyyy-MM-dd}', @commandId = '{commandId}'";
            await db.Use(sql).ExecuteCommandAsync();
        }
    }

    static int MapToInt(IObjectDataRecord record)
        => record.GetInt(0);
}
