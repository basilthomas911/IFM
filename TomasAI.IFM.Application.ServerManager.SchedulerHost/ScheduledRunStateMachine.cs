using TomasAI.IFM.Application.ServerManager.Contracts;

namespace TomasAI.IFM.Application.ServerManager.SchedulerHost;

public static class ScheduledRunStateMachine
{
    private static readonly IReadOnlyDictionary<ScheduledRunState, IReadOnlySet<ScheduledRunState>> Allowed =
        new Dictionary<ScheduledRunState, IReadOnlySet<ScheduledRunState>>
        {
            [ScheduledRunState.Planned] = new HashSet<ScheduledRunState>
            {
                ScheduledRunState.Starting,
                ScheduledRunState.BlockedDependency,
                ScheduledRunState.SkippedOverlap,
                ScheduledRunState.Misfired,
                ScheduledRunState.Abandoned
            },
            [ScheduledRunState.Starting] = new HashSet<ScheduledRunState>
            {
                ScheduledRunState.Running,
                ScheduledRunState.Failed,
                ScheduledRunState.Cancelling,
                ScheduledRunState.Abandoned
            },
            [ScheduledRunState.Running] = new HashSet<ScheduledRunState>
            {
                ScheduledRunState.Succeeded,
                ScheduledRunState.Failed,
                ScheduledRunState.TimedOut,
                ScheduledRunState.Cancelling,
                ScheduledRunState.Abandoned
            },
            [ScheduledRunState.Cancelling] = new HashSet<ScheduledRunState>
            {
                ScheduledRunState.Cancelled,
                ScheduledRunState.ForceTerminated,
                ScheduledRunState.Abandoned
            }
        };

    public static bool IsTerminal(ScheduledRunState state) => !Allowed.ContainsKey(state);

    public static void EnsureTransition(ScheduledRunState current, ScheduledRunState next)
    {
        if (!Allowed.TryGetValue(current, out var allowed) || !allowed.Contains(next))
        {
            throw new InvalidOperationException($"Scheduled run cannot transition from {current} to {next}.");
        }
    }
}
