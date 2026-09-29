using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Domain.Supervisor.Health;
using TomasAI.IFM.Domain.Supervisor.Health.Collection;
using TomasAI.IFM.Domain.Supervisor.Metrics;
using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.ReadModels;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Supervisor.UnitTests;

public sealed class SupervisorActorMetricsPollingServiceTests
{
    [Fact]
    public void Collects_all_sources_and_stores_valid_snapshot_on_dedicated_thread()
    {
        var state = new SupervisorActorMetricsState();
        var source = new Source([
            new ActorSource(new(ActorType.Query, "Healthy"), SupervisorActorHealth.Healthy),
            new ActorSource(new(ActorType.Event, "Degraded"), SupervisorActorHealth.Degraded)
        ]);
        using var poller = Create(source, state, new ExceptionLog(), TimeSpan.FromMilliseconds(10));

        poller.Start();
        Assert.True(SpinWait.SpinUntil(() => state.Current.Revision > 0, TimeSpan.FromSeconds(2)));
        Assert.True(poller.Stop(TimeSpan.FromSeconds(2)));

        Assert.Equal(2, state.Current.ExpectedActors);
        Assert.Equal(2, state.Current.CollectedActors);
        Assert.Equal(1, state.Current.HealthyActors);
        Assert.Equal(1, state.Current.DegradedActors);
        Assert.True(state.Current.HasValidHealthCount);
        Assert.Equal(SupervisorPollingServiceState.Stopped, poller.State);
    }

    [Fact]
    public void Contains_one_actor_failure_and_continues_collecting()
    {
        var state = new SupervisorActorMetricsState();
        var exceptions = new ExceptionLog();
        var source = new Source([
            new ActorSource(new(ActorType.Query, "Broken"), SupervisorActorHealth.Unknown, true),
            new ActorSource(new(ActorType.Event, "Healthy"), SupervisorActorHealth.Healthy)
        ]);
        using var poller = Create(source, state, exceptions, TimeSpan.FromMilliseconds(10));

        poller.Start();
        Assert.True(SpinWait.SpinUntil(() => state.Current.Revision > 0, TimeSpan.FromSeconds(2)));
        Assert.True(poller.Stop(TimeSpan.FromSeconds(2)));

        Assert.Equal(2, state.Current.ExpectedActors);
        Assert.Equal(1, state.Current.CollectedActors);
        Assert.Equal(1, state.Current.FailedActors);
        Assert.Equal(1, state.Current.UnknownActors);
        Assert.True(exceptions.ActorFailures >= 1);
        Assert.True(state.Current.HasValidHealthCount);
    }

    [Fact]
    public void Recoverable_source_state_logger_fallback_advisory_and_action_failures_never_stop_polling()
    {
        using var poller = new SupervisorActorMetricsPollingService(
            new ThrowingSource(), new ThrowingState(), new ThrowingExceptionLog(),
            new ThrowingAdvisory(), new ThrowingActions(), new ThrowingLogger(),
            interval: TimeSpan.FromMilliseconds(5));

        poller.Start();
        Assert.True(SpinWait.SpinUntil(
            () => poller.CaptureStatus().CompletedCycles >= 5,
            TimeSpan.FromSeconds(2)));

        var status = poller.CaptureStatus();
        Assert.Equal(SupervisorPollingServiceState.Running, status.State);
        Assert.True(status.IsAlive);
        Assert.True(status.CompletedCycles >= 5);
        Assert.NotNull(status.LastHeartbeatUtc);
        Assert.True(poller.Stop(TimeSpan.FromSeconds(2)));
    }

    static SupervisorActorMetricsPollingService Create(
        ISupervisorManagedActorMetricsSource source,
        ISupervisorActorMetricsState state,
        ISupervisorExceptionLog exceptions,
        TimeSpan interval) => new(
            source, state, exceptions, new NoOpSupervisorHealthLlmAdvisorySink(),
            new NoActions(),
            NullLogger<SupervisorActorMetricsPollingService>.Instance, interval: interval);

    sealed class NoActions : ISupervisorHealthActionCoordinator
    {
        public void Observe(SupervisorActorMetricsSnapshot snapshot) { }
    }

    sealed class Source(IReadOnlyList<ISupervisorActorMetricsSource> actors)
        : ISupervisorManagedActorMetricsSource
    {
        public IReadOnlyList<ISupervisorActorMetricsSource> Current { get; } = actors;
    }

    sealed class ActorSource(ActorMailboxId actorId, SupervisorActorHealth health, bool fail = false)
        : ISupervisorActorMetricsSource
    {
        public ActorMailboxId ActorId { get; } = actorId;

        public SupervisorActorMetrics CaptureSnapshot()
        {
            if (fail) throw new InvalidOperationException("injected");
            Assert.Equal(SupervisorActorMetricsPollingService.PollingThreadName, Thread.CurrentThread.Name);
            return new(ActorId, 1, true, health, 1, 0, 0, 0,
                SupervisorSnapshotQuality.Complete, []);
        }
    }

    sealed class ExceptionLog : ISupervisorExceptionLog
    {
        public int ActorFailures;
        public void ActorSnapshotFailed(ActorMailboxId actorId, Exception exception) =>
            Interlocked.Increment(ref ActorFailures);
        public void PollCycleFailed(Exception exception) { }
        public void HeartbeatLogFailed(Exception exception) { }
    }

    sealed class ThrowingSource : ISupervisorManagedActorMetricsSource
    {
        public IReadOnlyList<ISupervisorActorMetricsSource> Current =>
            throw new InvalidOperationException("injected source failure");
    }

    sealed class ThrowingState : ISupervisorActorMetricsState
    {
        public SupervisorActorMetricsSnapshot Current => throw new InvalidOperationException("injected read failure");
        public void Store(SupervisorActorMetricsSnapshot snapshot) =>
            throw new InvalidOperationException("injected state failure");
    }

    sealed class ThrowingExceptionLog : ISupervisorExceptionLog
    {
        public void ActorSnapshotFailed(ActorMailboxId actorId, Exception exception) => throw new InvalidOperationException("injected fallback actor-log failure");
        public void PollCycleFailed(Exception exception) => throw new InvalidOperationException("injected fallback cycle-log failure");
        public void HeartbeatLogFailed(Exception exception) => throw new InvalidOperationException("injected fallback heartbeat-log failure");
    }

    sealed class ThrowingAdvisory : ISupervisorHealthLlmAdvisorySink
    {
        public void Observe(SupervisorHealthAdvisoryObservation observation) =>
            throw new InvalidOperationException("injected advisory failure");
    }

    sealed class ThrowingActions : ISupervisorHealthActionCoordinator
    {
        public void Observe(SupervisorActorMetricsSnapshot snapshot) =>
            throw new InvalidOperationException("injected action failure");
    }

    sealed class ThrowingLogger : ILogger<SupervisorActorMetricsPollingService>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter) =>
            throw new InvalidOperationException("injected logger failure");
    }
}
