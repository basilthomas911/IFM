using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.ServiceApi;
namespace TomasAI.IFM.Application.ScheduledTask.Shared;
/// <summary>Records verified business stages in the owning actor-managed occurrence.</summary>
public sealed class ScheduledTaskBusinessReceipts(IScheduledTaskCommandApi commands, IScheduledTaskQueryApi queries)
{
    /// <summary>Waits for the parent host's persisted process-start receipt before submitting stages.</summary>
    public async Task WaitForRunningAsync(CancellationToken cancellationToken)
    {
        var runId = RequiredGuid("IFM_SCHEDULED_RUN_ID");
        var scheduleId = RequiredGuid("IFM_SCHEDULED_DEFINITION_ID");
        var environment = Environment.GetEnvironmentVariable("IFM_ENVIRONMENT") ?? throw new InvalidOperationException("Task environment is missing.");
        var host = Environment.GetEnvironmentVariable("IFM_SCHEDULED_HOST_ID") ?? throw new InvalidOperationException("Task owning host is missing.");
        var fire = DateTimeOffset.Parse(Environment.GetEnvironmentVariable("IFM_SCHEDULED_FIRE_UTC")!, System.Globalization.CultureInfo.InvariantCulture);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        while (true)
        {
            var result = await queries.GetScheduledTaskRunAsync(new() { EntityId = new(runId), ScheduleId = new(scheduleId), Environment = environment, HostId = host, IntendedFireTimeUtc = fire }, deadline.Token);
            if (result.Success && result.Value?.Status == ScheduledTaskRunStatus.Running) return;
            if (result.Value?.Status is ScheduledTaskRunStatus.Failed or ScheduledTaskRunStatus.Rejected or ScheduledTaskRunStatus.Uncertain) throw new InvalidOperationException("Task occurrence is not permitted to execute business work.");
            await Task.Delay(TimeSpan.FromMilliseconds(200), deadline.Token);
        }
    }
    /// <summary>Persists one verified stage; only PositionsFinalized from the market-close task permits date rollover.</summary>
    public async Task RecordAsync(DateOnly valueDate, string stage, string detail, CancellationToken cancellationToken)
    {
        var result = await commands.RecordScheduledTaskRunStageAsync(new()
        {
            CommandId = Guid.NewGuid(), OperationCommandId = RequiredGuid("IFM_SCHEDULED_CORRELATION_ID"), EntityId = new(RequiredGuid("IFM_SCHEDULED_RUN_ID")), Operator = "ScheduledTask",
            ValueDate = valueDate, Stage = stage, Detail = detail
        }, cancellationToken);
        if (!result.Success) throw new InvalidOperationException("Business stage was rejected: " + result.ErrorMessage);
    }
    /// <summary>Requires actor-managed identities; legacy launches cannot falsely record business completion.</summary>
    private static Guid RequiredGuid(string name) => Guid.TryParse(Environment.GetEnvironmentVariable(name), out var id) && id != Guid.Empty
        ? id : throw new InvalidOperationException("Actor-managed task identity is missing: " + name);
}
