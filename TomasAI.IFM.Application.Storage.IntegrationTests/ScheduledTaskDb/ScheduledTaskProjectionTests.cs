using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Storage.ScheduledTaskDb;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using Xunit;
namespace TomasAI.IFM.Application.Storage.IntegrationTests.ScheduledTaskDb;
public sealed class ScheduledTaskProjectionTests
{
    [Fact]
    public async Task Persisted_revision_fences_reordered_delivery_and_survives_repository_restart()
    {
        var keyspace = "ifm_sched_" + Guid.NewGuid().ToString("N");
        var logger = Substitute.For<ILogger<DbProvider>>();
        var settings = new DbConnectionSettings()
            .Add("admin", "Contact Points=localhost;Port=9042;Default Keyspace=system", "System.Data.ScyllaDb")
            .Add("TradeDbConnection", $"Contact Points=localhost;Port=9042;Default Keyspace={keyspace}", "System.Data.ScyllaDb");
        var admin = new Repository(settings["admin"], logger);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var token = deadline.Token;
        await admin.Use("ScheduledTaskTest.Create", $"CREATE KEYSPACE {keyspace} WITH replication = {{'class':'SimpleStrategy','replication_factor':1}};").ExecuteCommandAsync(token);
        try
        {
            await new ScheduledTaskSchemaDb(settings, logger).CreateAllAsync();
            var store = new ScheduledTaskReadStore(settings, logger);
            var id = new ScheduledTaskId(Guid.NewGuid());
            var definition = new ScheduledTaskDefinition { Id = id, Revision = 2, DesiredRevision = 2, Enabled = true,
                Schedule = new() { Name = "Market close", TaskKey = ScheduledTaskKeys.FuturesMarketClose, Expression = "0 1 17 ? * MON-FRI" } };
            await store.ProjectAsync(definition, token);
            await store.ProjectAsync(definition with { Revision = 1, Enabled = false, DesiredRevision = 1 }, token);
            var restarted = new ScheduledTaskReadStore(settings, logger);
            var loaded = await restarted.GetDefinitionAsync("Development", "development", id, token);
            Assert.NotNull(loaded); Assert.Equal(2, loaded.Revision); Assert.True(loaded.Enabled);
            Assert.Single(await restarted.GetDefinitionsAsync("Development", "development", 10, token));
            var catalog = new ScheduledTaskCatalog { Id = ScheduledTaskIdentities.Catalog("Development", "development"), Revision = 2, HostId = "development", Environment = "Development", Projects = [new() { TaskKey = ScheduledTaskKeys.FuturesMarketClose, Available = true }] };
            await store.ProjectAsync(catalog, token); await store.ProjectAsync(catalog with { Revision = 1, Projects = [] }, token);
            Assert.Single((await restarted.GetCatalogAsync("Development", "development", token))!.Projects);
            var fire = new DateTimeOffset(2026, 10, 7, 21, 1, 0, TimeSpan.Zero);
            var run = new ScheduledTaskRun { Id = ScheduledTaskIdentities.Occurrence(id, fire), ScheduleId = id, Revision = 3, HostId = "development", Environment = "Development", IntendedFireTimeUtc = fire, Status = ScheduledTaskRunStatus.Succeeded };
            await store.ProjectAsync(run, token); await store.ProjectAsync(run with { Revision = 2, Status = ScheduledTaskRunStatus.Running }, token);
            Assert.Equal(ScheduledTaskRunStatus.Succeeded, (await restarted.GetRunAsync("Development", "development", id, run.Id, fire, token))!.Status);
            Assert.Single(await restarted.GetRunsAsync("Development", "development", id, 10, token));
            Assert.Null(await restarted.GetCompletedEndOfDayAsync("Development", token));
            await store.ProjectAsync(run with { CompletedEndOfDayValueDate = new DateOnly(2026,10,7), Revision = 4 }, token);
            await store.ProjectAsync(run with { Id = new(Guid.NewGuid()), CompletedEndOfDayValueDate = new DateOnly(2026,10,6), Revision = 5 }, token);
            Assert.Equal(new DateOnly(2026,10,7), await new ScheduledTaskReadStore(settings, logger).GetCompletedEndOfDayAsync("Development", token));
        }
        finally { await admin.Use("ScheduledTaskTest.Drop", $"DROP KEYSPACE IF EXISTS {keyspace};").ExecuteCommandAsync(CancellationToken.None); }
    }
    private sealed class Repository(IDbConnectionSetting settings, ILogger<DbProvider> logger) : ObjectDataRepository<Repository>(settings, logger)
    { public override IObjectRepository Database => this; }
}
