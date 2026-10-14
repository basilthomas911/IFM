using TomasAI.IFM.Application.Storage.ScheduledTaskDb.Schema;
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
[Collection("PostgreSQL plan atomicity")]
[Trait("Category", "SelfContained")]
public sealed class ScheduledTaskProjectionTests(TomasAI.IFM.Application.Storage.IntegrationTests.TradePlanDb.PostgresTradePlanFixture fixture)
{
    [Fact]
    public async Task Persisted_revision_fences_reordered_delivery_and_survives_repository_restart()
    {
        var logger = Substitute.For<ILogger<DbProvider>>();
        var settings = new DbConnectionSettings().Add(ScheduledTaskDbContext.ConnectionName, fixture.Connection, "System.Data.Postgres");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var token = deadline.Token;
        await new ScheduledTaskSchemaDb(settings, logger).CreateAllAsync();
        var environment = "test-" + Guid.NewGuid();
            var store = new ScheduledTaskDbContext(settings, logger);
            var id = new ScheduledTaskId(Guid.NewGuid());
            var definition = new ScheduledTaskDefinition { Id = id, Revision = 2, DesiredRevision = 2, Enabled = true,
                Schedule = new() { Environment = environment, Name = "Market close", TaskKey = ScheduledTaskKeys.FuturesMarketClose, Expression = "0 1 17 ? * MON-FRI" } };
            await store.ProjectAsync(definition, token);
            await store.ProjectAsync(definition with { Revision = 1, Enabled = false, DesiredRevision = 1 }, token);
            var restarted = new ScheduledTaskDbContext(settings, logger);
            var loaded = await restarted.GetDefinitionAsync(environment, "development", id, token);
            Assert.NotNull(loaded); Assert.Equal(2, loaded.Revision); Assert.True(loaded.Enabled);
            Assert.Single(await restarted.GetDefinitionsAsync(environment, "development", 10, token));
            var catalog = new ScheduledTaskCatalog { Id = ScheduledTaskIdentities.Catalog(environment, "development"), Revision = 2, HostId = "development", Environment = environment, Projects = [new() { TaskKey = ScheduledTaskKeys.FuturesMarketClose, Available = true }] };
            await store.ProjectAsync(catalog, token); await store.ProjectAsync(catalog with { Revision = 1, Projects = [] }, token);
            Assert.Single((await restarted.GetCatalogAsync(environment, "development", token))!.Projects);
            var fire = new DateTimeOffset(2026, 10, 7, 21, 1, 0, TimeSpan.Zero);
            var run = new ScheduledTaskRun { Id = ScheduledTaskIdentities.Occurrence(id, fire), ScheduleId = id, Revision = 3, HostId = "development", Environment = environment, IntendedFireTimeUtc = fire, Status = ScheduledTaskRunStatus.Succeeded };
            await store.ProjectAsync(run, token); await store.ProjectAsync(run with { Revision = 2, Status = ScheduledTaskRunStatus.Running }, token);
            Assert.Equal(ScheduledTaskRunStatus.Succeeded, (await restarted.GetRunAsync(environment, "development", id, run.Id, fire, token))!.Status);
            Assert.Single(await restarted.GetRunsAsync(environment, "development", id, 10, token));
            Assert.Null(await restarted.GetCompletedEndOfDayAsync(environment, token));
            await store.ProjectAsync(run with { CompletedEndOfDayValueDate = new DateOnly(2026,10,7), Revision = 4 }, token);
            await store.ProjectAsync(run with { Id = new(Guid.NewGuid()), CompletedEndOfDayValueDate = new DateOnly(2026,10,6), Revision = 5 }, token);
            Assert.Equal(new DateOnly(2026,10,7), await new ScheduledTaskDbContext(settings, logger).GetCompletedEndOfDayAsync(environment, token));
    }
}
