using FluentAssertions;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Domain.MarketData.Shared;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class DatasetIncidentStateMachineTests
{
    static readonly DateOnly ValueDate = new(2026, 9, 4);

    [Theory]
    [InlineData(FuturesMarketState.OffTrading, FuturesMarketState.LiveTrading)]
    [InlineData(FuturesMarketState.LiveTrading, FuturesMarketState.OffTrading)]
    public void Session_transition_retains_incident_but_starts_new_policy_window(FuturesMarketState before, FuturesMarketState after)
    {
        var time = new ManualTimeProvider();
        var machine = Create(time);
        var original = machine.ObserveScheduled(before, false, DatabentoDatasetFailureReason.NativeDrainStalled, Guid.NewGuid());
        time.Advance(TimeSpan.FromMinutes(20));
        var transition = machine.ObserveScheduled(after, false, DatabentoDatasetFailureReason.NativeDrainStalled, Guid.NewGuid());
        transition.Snapshot.IncidentId.Should().Be(original.Snapshot.IncidentId);
        transition.Snapshot.UnhealthyDuration.Should().Be(TimeSpan.FromMinutes(20));
        transition.Snapshot.PolicyUnhealthyDuration.Should().Be(TimeSpan.Zero);
        transition.Action.Should().Be(after == FuturesMarketState.LiveTrading
            ? DatasetRecoveryAction.CooperativeReset : DatasetRecoveryAction.None);
    }

    [Fact]
    public void Replacement_backoff_and_rolling_failure_window_are_monotonic_and_survive_hydration()
    {
        var time = new ManualTimeProvider();
        var machine = Create(time);
        var generation = Guid.NewGuid();
        machine.ObserveTerminal(true, DatabentoDatasetFailureReason.NativeTerminalFailure, generation);
        machine.RecordProcessReplacement(false, generation);
        machine.ObserveTerminal(true, DatabentoDatasetFailureReason.NativeTerminalFailure, generation).Action.Should().Be(DatasetRecoveryAction.None);
        time.Advance(TimeSpan.FromSeconds(5));
        machine.ObserveTerminal(true, DatabentoDatasetFailureReason.NativeTerminalFailure, generation).Action.Should().Be(DatasetRecoveryAction.ReplaceProcess);
        machine.RecordProcessReplacement(false, generation);
        var restarted = Create(new ManualTimeProvider());
        restarted.Hydrate(machine.Current);
        restarted.Current.IsOpen.Should().BeTrue("persisted elapsed time can precede the new process timestamp origin");
        restarted.Current.ReplacementBackoffRemaining.Should().Be(TimeSpan.FromSeconds(30));
        restarted.RecordProcessReplacement(false, generation).ProcessReplacementLatched.Should().BeTrue();
        time.Advance(TimeSpan.FromMinutes(16));
        machine.RecordProcessReplacement(false, generation).ProcessReplacementLatched.Should().BeFalse();
        machine.Current.ReplacementFailureAges.Should().ContainSingle();
    }

    [Fact]
    public void Live_trading_attempts_once_per_probe_and_escalates_after_five_attempts()
    {
        var time = new ManualTimeProvider();
        var machine = Create(time);

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            var decision = machine.ObserveScheduled(FuturesMarketState.LiveTrading, false,
                DatabentoDatasetFailureReason.NativeDrainStalled, Guid.NewGuid());
            decision.Action.Should().Be(DatasetRecoveryAction.CooperativeReset);
            decision.Snapshot.CooperativeAttempts.Should().Be(attempt);
            if (attempt != 5) time.Advance(TimeSpan.FromMinutes(1));
        }

        machine.ObserveScheduled(FuturesMarketState.LiveTrading, false,
            DatabentoDatasetFailureReason.NativeDrainStalled, Guid.NewGuid()).Action
            .Should().Be(DatasetRecoveryAction.ReplaceProcess);
    }

    [Fact]
    public void Live_trading_escalates_on_elapsed_window_even_when_attempt_count_is_lower()
    {
        var time = new ManualTimeProvider();
        var machine = Create(time);
        machine.ObserveScheduled(FuturesMarketState.LiveTrading, false,
            DatabentoDatasetFailureReason.ManagedChannelBlocked, Guid.NewGuid());
        time.Advance(TimeSpan.FromMinutes(5));

        machine.ObserveScheduled(FuturesMarketState.LiveTrading, false,
            DatabentoDatasetFailureReason.ManagedChannelBlocked, Guid.NewGuid()).Action
            .Should().Be(DatasetRecoveryAction.ReplaceProcess);
    }

    [Fact]
    public void One_full_healthy_live_minute_closes_incident_without_generation_resetting_it()
    {
        var time = new ManualTimeProvider();
        var machine = Create(time);
        var oldGeneration = Guid.NewGuid();
        var replacement = Guid.NewGuid();
        machine.ObserveScheduled(FuturesMarketState.LiveTrading, false,
            DatabentoDatasetFailureReason.NativeProducerStopped, oldGeneration);
        machine.RecordCooperativeResult(true, replacement);

        machine.ObserveScheduled(FuturesMarketState.LiveTrading, true,
            DatabentoDatasetFailureReason.None, replacement).Snapshot.IsOpen.Should().BeTrue();
        time.Advance(TimeSpan.FromSeconds(59));
        machine.ObserveScheduled(FuturesMarketState.LiveTrading, true,
            DatabentoDatasetFailureReason.None, replacement).Snapshot.IsOpen.Should().BeTrue();
        time.Advance(TimeSpan.FromSeconds(1));

        var closed = machine.ObserveScheduled(FuturesMarketState.LiveTrading, true,
            DatabentoDatasetFailureReason.None, replacement).Snapshot;
        closed.IsOpen.Should().BeFalse();
        closed.CooperativeAttempts.Should().Be(0);
    }

    [Fact]
    public void Off_trading_waits_fifteen_minutes_then_reset_failure_escalates()
    {
        var time = new ManualTimeProvider();
        var machine = Create(time);
        var generation = Guid.NewGuid();

        machine.ObserveScheduled(FuturesMarketState.OffTrading, false,
            DatabentoDatasetFailureReason.NativeDrainStalled, generation).Action
            .Should().Be(DatasetRecoveryAction.None);
        time.Advance(TimeSpan.FromMinutes(14).Add(TimeSpan.FromSeconds(59)));
        machine.ObserveScheduled(FuturesMarketState.OffTrading, false,
            DatabentoDatasetFailureReason.NativeDrainStalled, generation).Action
            .Should().Be(DatasetRecoveryAction.None);
        time.Advance(TimeSpan.FromSeconds(1));
        machine.ObserveScheduled(FuturesMarketState.OffTrading, false,
            DatabentoDatasetFailureReason.NativeDrainStalled, generation).Action
            .Should().Be(DatasetRecoveryAction.CooperativeReset);
        machine.RecordCooperativeResult(false, generation);

        machine.ObserveScheduled(FuturesMarketState.OffTrading, false,
            DatabentoDatasetFailureReason.NativeDrainStalled, generation).Action
            .Should().Be(DatasetRecoveryAction.ReplaceProcess);
    }

    [Fact]
    public void Terminal_exit_skips_cooperative_reset_and_closed_requests_stop()
    {
        var machine = Create(new ManualTimeProvider());
        machine.ObserveTerminal(true, DatabentoDatasetFailureReason.NativeTerminalFailure,
            Guid.NewGuid()).Action.Should().Be(DatasetRecoveryAction.ReplaceProcess);
        machine.ObserveScheduled(FuturesMarketState.Closed, true,
            DatabentoDatasetFailureReason.None, Guid.Empty).Action
            .Should().Be(DatasetRecoveryAction.StopForClosure);
        machine.Current.IsOpen.Should().BeFalse();
    }

    [Fact]
    public void Three_failed_process_replacements_latch_only_this_incident()
    {
        var machine = Create(new ManualTimeProvider());
        machine.ObserveTerminal(true, DatabentoDatasetFailureReason.NativeTerminalFailure,
            Guid.NewGuid());
        machine.RecordProcessReplacement(false, Guid.NewGuid());
        machine.RecordProcessReplacement(false, Guid.NewGuid());
        var latched = machine.RecordProcessReplacement(false, Guid.NewGuid());

        latched.ProcessReplacementLatched.Should().BeTrue();
        machine.ObserveTerminal(true, DatabentoDatasetFailureReason.NativeTerminalFailure,
            Guid.NewGuid()).Action.Should().Be(DatasetRecoveryAction.None);
        machine.ClearLatch("operator approved retry").ProcessReplacementLatched.Should().BeFalse();
    }

    [Fact]
    public void Successful_process_replacements_do_not_consume_failed_replacement_budget()
    {
        var machine = Create(new ManualTimeProvider());
        machine.ObserveTerminal(true, DatabentoDatasetFailureReason.NativeTerminalFailure,
            Guid.NewGuid());
        machine.RecordProcessReplacement(true, Guid.NewGuid());
        machine.RecordProcessReplacement(true, Guid.NewGuid());
        machine.RecordProcessReplacement(false, Guid.NewGuid());
        machine.RecordProcessReplacement(false, Guid.NewGuid());

        machine.Current.ProcessReplacementLatched.Should().BeFalse();
        machine.RecordProcessReplacement(false, Guid.NewGuid())
            .ProcessReplacementLatched.Should().BeTrue();
    }

    [Fact]
    public void Admission_rejects_stale_identity_duplicate_and_decreasing_sequence()
    {
        var registry = new DatasetWorkerAdmissionRegistry();
        var admitted = new DatasetWorkerAdmission("GLBX.MDP3", ValueDate,
            Guid.NewGuid(), Guid.NewGuid(), 1);
        registry.Admit(admitted);

        registry.TryAccept(admitted, 1).Should().BeTrue();
        registry.TryAccept(admitted, 1).Should().BeFalse();
        registry.TryAccept(admitted, 0).Should().BeFalse();
        registry.TryAccept(admitted with { GenerationId = Guid.NewGuid() }, 2).Should().BeFalse();
        registry.Close(admitted.Dataset, admitted.GenerationId);
        registry.TryAccept(admitted, 2).Should().BeFalse();
        registry.RejectedPublications.Should().Be(4);
    }

    [Fact]
    public void Options_reject_invalid_policy_relationships()
    {
        var action = () => new DatabentoStage3Options
        {
            LiveTradingPollInterval = TimeSpan.FromMinutes(2),
            LiveTradingEscalationWindow = TimeSpan.FromMinutes(1)
        }.Validate();

        action.Should().Throw<InvalidOperationException>();
    }

    static DatasetIncidentStateMachine Create(ManualTimeProvider time) =>
        new("GLBX.MDP3", ValueDate, new DatabentoStage3Options(), time);

    sealed class ManualTimeProvider : TimeProvider
    {
        DateTimeOffset utcNow = new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);
        long timestamp;

        public override DateTimeOffset GetUtcNow() => utcNow;
        public override long GetTimestamp() => timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public void Advance(TimeSpan value)
        {
            utcNow += value;
            timestamp = checked(timestamp + value.Ticks);
        }
    }
}
