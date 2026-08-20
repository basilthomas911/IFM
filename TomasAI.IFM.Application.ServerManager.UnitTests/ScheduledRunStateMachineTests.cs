using FluentAssertions;
using TomasAI.IFM.Application.ServerManager.Contracts;
using TomasAI.IFM.Application.ServerManager.SchedulerHost;

namespace TomasAI.IFM.Application.ServerManager.UnitTests;

public sealed class ScheduledRunStateMachineTests
{
    [Theory]
    [InlineData(ScheduledRunState.Planned, ScheduledRunState.Starting)]
    [InlineData(ScheduledRunState.Starting, ScheduledRunState.Running)]
    [InlineData(ScheduledRunState.Running, ScheduledRunState.Succeeded)]
    [InlineData(ScheduledRunState.Running, ScheduledRunState.TimedOut)]
    [InlineData(ScheduledRunState.Cancelling, ScheduledRunState.ForceTerminated)]
    public void Allows_legal_monotonic_transition(ScheduledRunState current, ScheduledRunState next)
    {
        var action = () => ScheduledRunStateMachine.EnsureTransition(current, next);

        action.Should().NotThrow();
    }

    [Theory]
    [InlineData(ScheduledRunState.Succeeded, ScheduledRunState.Running)]
    [InlineData(ScheduledRunState.Failed, ScheduledRunState.Starting)]
    [InlineData(ScheduledRunState.Running, ScheduledRunState.Planned)]
    [InlineData(ScheduledRunState.Running, ScheduledRunState.ForceTerminated)]
    public void Rejects_terminal_or_backward_transition(ScheduledRunState current, ScheduledRunState next)
    {
        var action = () => ScheduledRunStateMachine.EnsureTransition(current, next);

        action.Should().Throw<InvalidOperationException>();
    }
}
