using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Run.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Run.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Run.Queries;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Hosting;
using Npgsql;
using NSubstitute;
using Quartz;
using TomasAI.IFM.Application.ServerManager.Contracts;
using TomasAI.IFM.Application.ServerManager.SchedulerHost;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Application.ServerManager.SchedulerHost.PortableTests;
public sealed class ActorScheduledTaskRecoveryTests
{
    [SchedulerPostgresFact]
    public async Task A_stale_duplicate_manual_trigger_does_not_submit_commands_or_launch_a_process()
    {
        var fire = new DateTimeOffset(2026, 10, 7, 22, 1, 0, TimeSpan.Zero);
        var scheduleId = new ScheduledTaskId(Guid.NewGuid());
        var runId = new ScheduledTaskId(Guid.NewGuid());
        var operationId = Guid.NewGuid();
        var options = new SchedulerHostOptions { SchedulerName = $"duplicate-trigger-{Guid.NewGuid():N}" };
        options.TaskCatalog.Add(new() { TaskKey = ScheduledTaskKeys.FuturesMarketOpen });
        await using var dataSource = NpgsqlDataSource.Create(Environment.GetEnvironmentVariable("IFM_SCHEDULER_TEST_PG")!);
        await using var ownership = new SchedulerOwnershipLease(dataSource, options);
        await ownership.AcquireAsync(CancellationToken.None);
        var context = Substitute.For<IJobExecutionContext>();
        context.MergedJobDataMap.Returns(new JobDataMap
        {
            [ScheduledTaskExecutionService.ScheduleDefinitionIdData] = scheduleId.Format(),
            [ActorScheduleRuntime.DefinitionRevisionData] = "2",
            [ScheduledTaskExecutionService.TaskKeyData] = ScheduledTaskKeys.FuturesMarketOpen,
            [ScheduledTaskExecutionService.OriginData] = ScheduledRunOrigin.Manual.ToString(),
            [ScheduledTaskExecutionService.RunIdData] = runId.Format(),
            ["intendedFireUtc"] = fire.ToString("O"),
            ["operationCommandId"] = operationId.ToString("D"),
            ["runAlreadyRequested"] = "true"
        });
        var commands = Substitute.For<IScheduledTaskCommandApi>();
        var queries = Substitute.For<IScheduledTaskQueryApi>();
        queries.GetScheduledTaskRunAsync(Arg.Any<GetScheduledTaskRunQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<ScheduledTaskRun>(new()
            {
                Id = runId, ScheduleId = scheduleId, OperationCommandId = operationId,
                Manual = true, Status = ScheduledTaskRunStatus.Succeeded
            }));
        var active = new ActiveRunRegistry();
        var coordinator = new ActorScheduledTaskRunCoordinator(options, ownership, new(options, dataSource),
            new(options, NullLogger<ScheduledProcessRunner>.Instance), new(options, Substitute.For<IHttpClientFactory>()),
            active, Substitute.For<IHostApplicationLifetime>(), commands, queries, NullLogger<ActorScheduledTaskRunCoordinator>.Instance);

        await coordinator.ExecuteAsync(context);

        await queries.Received(1).GetScheduledTaskRunAsync(
            Arg.Is<GetScheduledTaskRunQuery>(query => query.EntityId == runId && query.ScheduleId == scheduleId &&
                query.IntendedFireTimeUtc == fire), Arg.Any<CancellationToken>());
        Assert.Empty(commands.ReceivedCalls());
        Assert.False(active.Contains(runId.Value));
    }

    [Theory]
    [InlineData(ScheduledTaskRunStatus.Succeeded, true, false)]
    [InlineData(ScheduledTaskRunStatus.Failed, true, false)]
    [InlineData(ScheduledTaskRunStatus.Rejected, true, false)]
    [InlineData(ScheduledTaskRunStatus.Running, false, true)]
    [InlineData(ScheduledTaskRunStatus.Admitted, false, true)]
    [InlineData(ScheduledTaskRunStatus.Requested, false, true)]
    [InlineData(ScheduledTaskRunStatus.Uncertain, false, false)]
    public async Task Restart_never_relaunches_business_work(ScheduledTaskRunStatus status, bool release, bool uncertain)
    {
        var commands = Substitute.For<IScheduledTaskCommandApi>();
        var queries = Substitute.For<IScheduledTaskQueryApi>();
        var runId = Guid.NewGuid();
        var definition = new ScheduledTaskDefinition { Id = new(Guid.NewGuid()), ActiveRunId = runId, LastAdmittedFireUtc = DateTimeOffset.UtcNow };
        queries.GetScheduledTaskRunAsync(Arg.Any<GetScheduledTaskRunQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<ScheduledTaskRun>(new() { Id = new(runId), ScheduleId = definition.Id, Status = status }));
        commands.RecordScheduledTaskRunCompletionAsync(Arg.Any<RecordScheduledTaskRunCompletionCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<GuidResult>(new(Guid.NewGuid())));
        commands.RecordScheduledTaskRunUncertainAsync(Arg.Any<RecordScheduledTaskRunUncertainCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<GuidResult>(new(Guid.NewGuid())));
        var recovery = new ActorScheduledTaskRecovery(commands, queries, new(), NullLogger<ActorScheduledTaskRecovery>.Instance);
        await recovery.ReconcileAsync(definition, CancellationToken.None);
        await commands.Received(release ? 1 : 0).RecordScheduledTaskRunCompletionAsync(Arg.Any<RecordScheduledTaskRunCompletionCommand>(), Arg.Any<CancellationToken>());
        await commands.Received(uncertain ? 1 : 0).RecordScheduledTaskRunUncertainAsync(Arg.Any<RecordScheduledTaskRunUncertainCommand>(), Arg.Any<CancellationToken>());
        await commands.DidNotReceive().RequestScheduledTaskRunAsync(Arg.Any<RequestScheduledTaskRunCommand>(), Arg.Any<CancellationToken>());
    }
    [Fact]
    public async Task Current_process_owned_run_is_not_interrupted_by_reconciliation()
    {
        var active = new ActiveRunRegistry(); var id = Guid.NewGuid();
        using var registration = active.Register(id);
        var commands = Substitute.For<IScheduledTaskCommandApi>(); var queries = Substitute.For<IScheduledTaskQueryApi>();
        await new ActorScheduledTaskRecovery(commands, queries, active, NullLogger<ActorScheduledTaskRecovery>.Instance)
            .ReconcileAsync(new() { ActiveRunId = id }, CancellationToken.None);
        Assert.Empty(queries.ReceivedCalls()); Assert.Empty(commands.ReceivedCalls());
    }
    [Fact]
    public void Quartz_readback_detects_removed_trigger_and_timezone_or_cron_drift()
    {
        var definition = new ScheduledTaskDefinition { Id = new(Guid.NewGuid()), Schedule = new() { Timing = ScheduledTaskTiming.Cron, Expression = "0 1 17 ? * MON-FRI", TimeZoneId = "America/New_York" } };
        var job = new JobKey("job"); var trigger = new TriggerKey("trigger");
        var desired = ActorScheduleRuntime.BuildTrigger(definition, job, trigger);
        Assert.True(ActorScheduleRuntime.TriggerMatches(desired, desired));
        Assert.False(ActorScheduleRuntime.TriggerMatches(null, desired));
        Assert.False(ActorScheduleRuntime.TriggerMatches(ActorScheduleRuntime.BuildTrigger(definition with { Schedule = definition.Schedule with { Expression = "0 2 17 ? * MON-FRI" } }, job, trigger), desired));
        Assert.False(ActorScheduleRuntime.TriggerMatches(ActorScheduleRuntime.BuildTrigger(definition with { Schedule = definition.Schedule with { TimeZoneId = "UTC" } }, job, trigger), desired));
    }
}
