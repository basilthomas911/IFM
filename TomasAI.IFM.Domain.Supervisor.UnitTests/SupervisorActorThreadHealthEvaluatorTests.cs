using TomasAI.IFM.Domain.Supervisor.Health.Evaluation;
using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Supervisor.UnitTests;

public sealed class SupervisorActorThreadHealthEvaluatorTests
{
    static readonly ActorThreadId ThreadId = new(ActorType.Command, "Risk", "portfolio-1");

    [Fact]
    public void Warns_immediately_then_rate_limits_for_one_minute()
    {
        var time = new ManualTimeProvider();
        var evaluator = new SupervisorActorThreadHealthEvaluator(timeProvider: time);

        Assert.True(evaluator.Evaluate(ThreadId, true).LogWarning);
        time.Advance(TimeSpan.FromSeconds(59));
        Assert.False(evaluator.Evaluate(ThreadId, true).LogWarning);
        time.Advance(TimeSpan.FromSeconds(1));
        Assert.True(evaluator.Evaluate(ThreadId, true).LogWarning);
    }

    [Fact]
    public void Degrades_at_five_minutes_and_requests_restart_at_fifteen()
    {
        var time = new ManualTimeProvider();
        var evaluator = new SupervisorActorThreadHealthEvaluator(timeProvider: time);

        evaluator.Evaluate(ThreadId, true);
        time.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal(SupervisorActorHealth.Degraded, evaluator.Evaluate(ThreadId, true).Health);
        time.Advance(TimeSpan.FromMinutes(10));
        var critical = evaluator.Evaluate(ThreadId, true);
        Assert.Equal(SupervisorActorHealth.Critical, critical.Health);
        Assert.True(critical.RestartRequired);
    }

    [Fact]
    public void Requires_two_clear_minutes_before_recovery()
    {
        var time = new ManualTimeProvider();
        var evaluator = new SupervisorActorThreadHealthEvaluator(timeProvider: time);
        evaluator.Evaluate(ThreadId, true);
        time.Advance(TimeSpan.FromMinutes(5));
        evaluator.Evaluate(ThreadId, true);

        Assert.Equal(SupervisorActorHealth.Degraded, evaluator.Evaluate(ThreadId, false).Health);
        time.Advance(TimeSpan.FromMinutes(2));
        var recovered = evaluator.Evaluate(ThreadId, false);
        Assert.True(recovered.Recovered);
        Assert.Equal(SupervisorActorHealth.Healthy, recovered.Health);
    }

    [Fact]
    public void Confirms_elevated_and_critical_depth_after_thirty_seconds()
    {
        var time = new ManualTimeProvider();
        var evaluator = new SupervisorActorThreadHealthEvaluator(timeProvider: time);
        var elevated = new SupervisorActorThreadHealthObservation(75, 100, 0, 0, 0, false, default, 1);
        Assert.Equal(SupervisorMailboxPressureState.Normal, evaluator.Evaluate(ThreadId, elevated).PressureState);
        time.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal(SupervisorMailboxPressureState.Elevated, evaluator.Evaluate(ThreadId, elevated).PressureState);
        var critical = elevated with { Depth = 90 };
        Assert.Equal(SupervisorMailboxPressureState.Critical, evaluator.Evaluate(ThreadId, critical).PressureState);
    }

    [Fact]
    public void Detects_nonempty_mailbox_without_progress_and_long_handler()
    {
        var time = new ManualTimeProvider();
        var evaluator = new SupervisorActorThreadHealthEvaluator(timeProvider: time);
        var observation = new SupervisorActorThreadHealthObservation(1, 100, 0, 0, 0, true, TimeSpan.FromMinutes(1), 1);
        evaluator.Evaluate(ThreadId, observation);
        time.Advance(TimeSpan.FromMinutes(1));
        var result = evaluator.Evaluate(ThreadId, observation);
        Assert.True(result.NoProgressWarning);
        Assert.True(result.HandlerDurationWarning);
        Assert.Equal(SupervisorActorHealth.Degraded, result.Health);
    }

    [Fact]
    public void New_generation_enters_recovering_for_two_minutes()
    {
        var time = new ManualTimeProvider();
        var evaluator = new SupervisorActorThreadHealthEvaluator(timeProvider: time);
        var observation = new SupervisorActorThreadHealthObservation(0, 100, 0, 0, 0, false, default, 1);
        evaluator.Evaluate(ThreadId, observation);
        var recovering = evaluator.Evaluate(ThreadId, observation with { Generation = 2 });
        Assert.Equal(SupervisorMailboxPressureState.Recovering, recovering.PressureState);
        time.Advance(TimeSpan.FromMinutes(2));
        Assert.True(evaluator.Evaluate(ThreadId, observation with { Generation = 2 }).Recovered);
    }

    sealed class ManualTimeProvider : TimeProvider
    {
        long _timestamp = 1;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Volatile.Read(ref _timestamp);
        public void Advance(TimeSpan duration) => Interlocked.Add(ref _timestamp, duration.Ticks);
    }
}
