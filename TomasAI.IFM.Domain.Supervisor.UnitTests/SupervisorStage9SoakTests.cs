using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Domain.Supervisor.Health;
using TomasAI.IFM.Domain.Supervisor.Health.Collection;
using TomasAI.IFM.Domain.Supervisor.Metrics;
using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.ReadModels;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Supervisor.UnitTests;

public sealed class SupervisorStage9SoakTests
{
    [Fact]
    [Trait("Category", "SupervisorStage9Soak")]
    public void Synthetic_pressure_recovery_and_partial_collection_remain_live_and_bounded()
    {
        var configuredMinutes = int.TryParse(
            Environment.GetEnvironmentVariable("IFM_SUPERVISOR_SOAK_MINUTES"), out var minutes)
            ? minutes
            : 0;
        var duration = configuredMinutes > 0
            ? TimeSpan.FromMinutes(configuredMinutes)
            : TimeSpan.FromSeconds(2);
        var state = new SupervisorActorMetricsState();
        var exceptionLog = new CountingExceptionLog();
        var actions = new ValidatingActions();
        var actors = Enumerable.Range(0, 128)
            .Select(index => (ISupervisorActorMetricsSource)new CyclingActorSource(index))
            .ToArray();
        using var poller = new SupervisorActorMetricsPollingService(
            new Sources(actors), state, exceptionLog, new NoOpSupervisorHealthLlmAdvisorySink(),
            actions, NullLogger<SupervisorActorMetricsPollingService>.Instance,
            interval: TimeSpan.FromMilliseconds(50));
        GC.Collect();
        var heapBefore = GC.GetTotalMemory(true);

        poller.Start();
        var deadline = DateTime.UtcNow + duration;
        while (DateTime.UtcNow < deadline)
        {
            Thread.Sleep(TimeSpan.FromMilliseconds(250));
            var status = poller.CaptureStatus();
            Assert.Equal(SupervisorPollingServiceState.Running, status.State);
            Assert.True(status.IsAlive);
        }
        Assert.True(poller.Stop(TimeSpan.FromSeconds(5)));
        GC.Collect();
        var heapGrowth = GC.GetTotalMemory(true) - heapBefore;
        var finalStatus = poller.CaptureStatus();

        Assert.True(finalStatus.CompletedCycles >= Math.Max(1, duration.TotalSeconds * 10));
        Assert.True(actions.ObservedCycles > 0);
        Assert.True(exceptionLog.ActorFailures > 0);
        Assert.True(state.Current.HasValidHealthCount);
        Assert.InRange(heapGrowth, long.MinValue, 128L * 1024 * 1024);
    }

    sealed class Sources(IReadOnlyList<ISupervisorActorMetricsSource> current)
        : ISupervisorManagedActorMetricsSource
    {
        public IReadOnlyList<ISupervisorActorMetricsSource> Current { get; } = current;
    }

    sealed class CyclingActorSource(int index) : ISupervisorActorMetricsSource
    {
        long _observations;
        public ActorMailboxId ActorId { get; } = new(
            index % 2 == 0 ? ActorType.Command : ActorType.Query,
            $"Stage9Actor{index}");

        public SupervisorActorMetrics CaptureSnapshot()
        {
            var observation = Interlocked.Increment(ref _observations);
            if (index == 0 && observation % 17 == 0)
                throw new InvalidOperationException("Injected periodic collection failure.");
            var phase = observation % 400;
            var health = phase < 200
                ? SupervisorActorHealth.Healthy
                : phase < 300
                    ? SupervisorActorHealth.Degraded
                    : phase < 350
                        ? SupervisorActorHealth.Critical
                        : SupervisorActorHealth.Healthy;
            var depth = health == SupervisorActorHealth.Healthy ? 0 : health == SupervisorActorHealth.Degraded ? 75 : 100;
            return new(ActorId, 1, true, health, 1, depth, 0, 0,
                SupervisorSnapshotQuality.Complete, []);
        }
    }

    sealed class ValidatingActions : ISupervisorHealthActionCoordinator
    {
        long _observedCycles;
        public long ObservedCycles => Interlocked.Read(ref _observedCycles);
        public void Observe(SupervisorActorMetricsSnapshot snapshot)
        {
            if (!snapshot.HasValidHealthCount)
                throw new InvalidOperationException("Invalid health-count invariant.");
            Interlocked.Increment(ref _observedCycles);
        }
    }

    sealed class CountingExceptionLog : ISupervisorExceptionLog
    {
        long _actorFailures;
        public long ActorFailures => Interlocked.Read(ref _actorFailures);
        public void ActorSnapshotFailed(ActorMailboxId actorId, Exception exception) =>
            Interlocked.Increment(ref _actorFailures);
        public void PollCycleFailed(Exception exception) { }
        public void HeartbeatLogFailed(Exception exception) { }
    }
}
