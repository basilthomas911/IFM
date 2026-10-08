using System.Collections.Specialized;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Npgsql;
using NSubstitute;
using Quartz;
using Quartz.Impl;
using TomasAI.IFM.Application.ServerManager.SchedulerHost;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Application.ServerManager.SchedulerHost.PortableTests;
public sealed class SchedulerPostgresFactAttribute : FactAttribute
{
    public SchedulerPostgresFactAttribute() { if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("IFM_SCHEDULER_TEST_PG"))) Skip = "Requires an isolated disposable PostgreSQL database."; }
}
/// <summary>Proves actual Quartz persistence and revision readback on either supported platform.</summary>
public sealed class QuartzPersistenceTests
{
    [SchedulerPostgresFact]
    public async Task Trigger_survives_restart_missing_trigger_is_repaired_and_disable_removes_it()
    {
        using var logging = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        Quartz.Logging.LogContext.SetCurrentLogProvider(logging);
        var connection = Environment.GetEnvironmentVariable("IFM_SCHEDULER_TEST_PG")!;
        await using var data = NpgsqlDataSource.Create(connection);
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2)); var token = deadline.Token;
        await new SchedulerDatabaseMigrator(data, NullLogger<SchedulerDatabaseMigrator>.Instance).MigrateAsync(token);
        var options = new SchedulerHostOptions { ActorManaged = true, SchedulerName = "IFM-Portable-Persistence", SeedInitialSchedules = false };
        await using var ownership = new SchedulerOwnershipLease(data, options); await ownership.AcquireAsync(token);
        var factory = new StdSchedulerFactory(Properties(connection, options.SchedulerName));
        var commands = Substitute.For<IScheduledTaskCommandApi>(); var queries = Substitute.For<IScheduledTaskQueryApi>();
        var definition = new ScheduledTaskDefinition { Id = new(Guid.NewGuid()), Revision = 1, DesiredRevision = 1, Enabled = true,
            Schedule = new() { Name = "Future fixture", TaskKey = "fixture", Expression = "0 1 17 ? * MON-FRI", StartsAtUtc = DateTimeOffset.UtcNow.AddDays(10) } };
        queries.GetScheduledTaskAsync(Arg.Any<GetScheduledTaskQuery>(), Arg.Any<CancellationToken>()).Returns(_ => new ServiceOk<ScheduledTaskDefinition>(definition));
        commands.RecordScheduledTaskInstallationAsync(Arg.Any<RecordScheduledTaskInstallationCommand>(), Arg.Any<CancellationToken>()).Returns(new ServiceOk<GuidResult>(new(Guid.NewGuid())));
        var runtime = new ActorScheduleRuntime(options, ownership, factory, commands, queries, NullLogger<ActorScheduleRuntime>.Instance);
        var triggerKey = new TriggerKey(definition.Id.Format(), "ifm-schedules");
        var scheduler = await factory.GetScheduler(token);
        try
        {
            await runtime.ApplyAsync(definition, token);
            Assert.NotNull(await scheduler.GetTrigger(triggerKey, token));
            await scheduler.Shutdown(false, token);
            factory = new StdSchedulerFactory(Properties(connection, options.SchedulerName));
            scheduler = await factory.GetScheduler(token);
            Assert.NotNull(await scheduler.GetTrigger(triggerKey, token));
            runtime = new(options, ownership, factory, commands, queries, NullLogger<ActorScheduleRuntime>.Instance);
            await scheduler.UnscheduleJob(triggerKey, token);
            await runtime.ApplyAsync(definition, token);
            Assert.NotNull(await scheduler.GetTrigger(triggerKey, token));
            definition = definition with { Enabled = false, DesiredRevision = 2, Revision = 2 };
            await runtime.ApplyAsync(definition, token);
            Assert.Null(await scheduler.GetTrigger(triggerKey, token));
            Assert.False(await scheduler.CheckExists(new JobKey(definition.Id.Format(), "ifm-schedules"), token));
        }
        finally { await scheduler.Shutdown(false, CancellationToken.None); }
    }
    private static NameValueCollection Properties(string connection, string name)
    {
        var provider = "fixture" + Guid.NewGuid().ToString("N");
        return new()
    {
        ["quartz.scheduler.instanceName"] = name,
        ["quartz.jobStore.type"] = "Quartz.Impl.AdoJobStore.JobStoreTX, Quartz",
        ["quartz.jobStore.driverDelegateType"] = "Quartz.Impl.AdoJobStore.PostgreSQLDelegate, Quartz",
        ["quartz.jobStore.useProperties"] = "true",
        ["quartz.jobStore.tablePrefix"] = "ifm_quartz.qrtz_",
        ["quartz.jobStore.dataSource"] = provider,
        [$"quartz.dataSource.{provider}.provider"] = "Npgsql",
        [$"quartz.dataSource.{provider}.connectionString"] = connection,
        ["quartz.serializer.type"] = "stj",
        ["quartz.threadPool.threadCount"] = "1"
    };
    }
}
