using Quartz;
using TomasAI.IFM.Application.ServerManager.SchedulerHost;
using TomasAI.IFM.Application.ServerManager.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
namespace TomasAI.IFM.Application.ServerManager.SchedulerHost.PortableTests;
public sealed class ScheduledTaskOccurrenceTests
{
    [Fact]
    public void Cron_execution_without_manual_keys_uses_its_stable_intended_fire_identity()
    {
        var id = new ScheduledTaskId(Guid.NewGuid()); var fire = DateTimeOffset.Parse("2026-10-07T22:00:00Z");
        var data = Data(id); var first = ScheduledTaskOccurrence.Create(data, fire); var retry = ScheduledTaskOccurrence.Create(data, fire);
        Assert.False(first.Manual); Assert.False(first.AlreadyRequested);
        Assert.Equal(fire, first.IntendedFireTimeUtc); Assert.Equal(ScheduledTaskIdentities.Occurrence(id,fire), first.RunId);
        Assert.Equal(first.RunId,retry.RunId); Assert.NotEqual(Guid.Empty, first.OperationCommandId);
    }
    [Fact]
    public void Manual_execution_preserves_requested_identity_and_correlation()
    {
        var id = new ScheduledTaskId(Guid.NewGuid()); var run = Guid.NewGuid(); var operation = Guid.NewGuid();
        var fire = DateTimeOffset.Parse("2026-10-07T22:01:00Z"); var data = Data(id);
        data["intendedFireUtc"] = fire.ToString("O"); data["operationCommandId"] = operation.ToString("D");
        data["runAlreadyRequested"] = "true"; data[ScheduledTaskExecutionService.OriginData] = ScheduledRunOrigin.Manual.ToString();
        data[ScheduledTaskExecutionService.RunIdData] = run.ToString("D");
        var result = ScheduledTaskOccurrence.Create(data,fire.AddSeconds(2));
        Assert.True(result.Manual); Assert.True(result.AlreadyRequested); Assert.Equal(run,result.RunId.Value);
        Assert.Equal(operation,result.OperationCommandId); Assert.Equal(fire,result.IntendedFireTimeUtc);
    }
    [Fact]
    public void Missing_fire_timestamp_does_not_replay_a_current_clock_occurrence()
        => Assert.Throws<InvalidOperationException>(() => ScheduledTaskOccurrence.Create(Data(new(Guid.NewGuid())), null));
    private static JobDataMap Data(ScheduledTaskId id) => new() {
        [ScheduledTaskExecutionService.ScheduleDefinitionIdData] = id.Value.ToString("D"),
        [ActorScheduleRuntime.DefinitionRevisionData] = "2", [ScheduledTaskExecutionService.TaskKeyData] = "futures-market-open" };
}
