using System.Collections.Concurrent;
using TomasAI.IFM.Domain.Supervisor.Shared.Service.Health.Policy;
using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Supervisor.Shared.Service.Health.Evaluation;

/// <summary>
/// Tracks continuous actor-thread limit incidents using monotonic time. Warnings are emitted immediately and no more
/// than once per minute; degradation begins after five minutes and restart is requested after fifteen minutes.
/// </summary>
public sealed class SupervisorActorThreadHealthEvaluator
{
    readonly ConcurrentDictionary<ActorThreadId, IncidentState> _states = new();
    readonly SupervisorActorHealthPolicy _policy;
    readonly TimeProvider _timeProvider;

    public SupervisorActorThreadHealthEvaluator(
        SupervisorActorHealthPolicy? policy = null,
        TimeProvider? timeProvider = null)
    {
        _policy = (policy ?? new SupervisorActorHealthPolicy()).Validate();
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Evaluates one actor thread without throwing for valid runtime observations.</summary>
    public SupervisorActorThreadHealthEvaluation Evaluate(ActorThreadId threadId, bool isAtLimit)
        => Evaluate(threadId, new SupervisorActorThreadHealthObservation(
            isAtLimit ? 1 : 0, 1, isAtLimit ? 1 : 0, 0, 0, false, TimeSpan.Zero, 1));

    /// <summary>Evaluates saturation, progress, handler duration, recovery hysteresis, and generation changes.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "The bounded actor observation and its health classification are calculated from one consistent snapshot.")]
    public SupervisorActorThreadHealthEvaluation Evaluate(
        ActorThreadId threadId,
        SupervisorActorThreadHealthObservation observation)
    {
        var now = _timeProvider.GetTimestamp();
        var state = _states.GetOrAdd(threadId, static _ => new());
        lock (state)
        {
            if (state.Generation != observation.Generation)
            {
                var restarted = state.Generation != 0;
                state.Reset();
                state.Generation = observation.Generation;
                state.RecoveringSince = restarted ? now : 0;
            }
            var utilization = observation.Capacity <= 0 ? 0 : (double)observation.Depth / observation.Capacity;
            if (state.LastProgressTimestamp == 0) state.LastProgressTimestamp = now;
            var rejection = observation.Rejected > state.LastRejected;
            var progress = observation.Dequeued > state.LastDequeued || observation.Completed > state.LastCompleted;
            state.LastRejected = observation.Rejected;
            state.LastDequeued = observation.Dequeued;
            state.LastCompleted = observation.Completed;
            if (progress || observation.Depth == 0) state.LastProgressTimestamp = now;
            var atLimit = observation.Capacity > 0 && observation.Depth >= observation.Capacity || rejection;
            if (atLimit)
            {
                if (state.FirstLimitTimestamp == 0) state.FirstLimitTimestamp = now;
                state.RecoverySince = 0;
            }
            else if (state.FirstLimitTimestamp != 0 && utilization < _policy.CriticalUtilization && !rejection)
            {
                if (state.RecoverySince == 0) state.RecoverySince = now;
                if (Elapsed(state.RecoverySince, now) >= _policy.SaturationRecoveryAfter)
                {
                    state.FirstLimitTimestamp = 0;
                    state.RecoveringSince = state.RecoveringSince == 0 ? state.RecoverySince : state.RecoveringSince;
                }
            }

            if (utilization < _policy.ElevatedUtilization) state.PressureSince = 0;
            var duration = state.FirstLimitTimestamp == 0 ? TimeSpan.Zero : Elapsed(state.FirstLimitTimestamp, now);
            var warning = atLimit && (state.LastWarningTimestamp == 0
                || Elapsed(state.LastWarningTimestamp, now) >= _policy.WarningInterval);
            if (warning) state.LastWarningTimestamp = now;
            var noProgress = observation.Depth > 0 && state.LastProgressTimestamp != 0
                && Elapsed(state.LastProgressTimestamp, now) >= _policy.NoProgressAfter;
            var longHandler = observation.IsProcessing && observation.ProcessingDuration >= _policy.HandlerWarningAfter;
            var restart = observation.IsAdmissionOpen && state.FirstLimitTimestamp != 0 && duration >= _policy.RestartRequiredAfter;
            SupervisorMailboxPressureState pressure;
            if (restart) pressure = SupervisorMailboxPressureState.Restarting;
            else if (state.FirstLimitTimestamp != 0 && duration >= _policy.DegradedAfter) pressure = SupervisorMailboxPressureState.Degraded;
            else if (atLimit) pressure = SupervisorMailboxPressureState.AtLimit;
            else if (utilization >= _policy.CriticalUtilization) pressure = ConfirmPressure(state, now, true);
            else if (utilization >= _policy.ElevatedUtilization) pressure = ConfirmPressure(state, now, false);
            else if (state.RecoveringSince != 0) pressure = SupervisorMailboxPressureState.Recovering;
            else pressure = SupervisorMailboxPressureState.Normal;
            var recovered = state.RecoveringSince != 0
                && utilization < _policy.ElevatedUtilization && !rejection
                && Elapsed(state.RecoveringSince, now) >= _policy.HealthRecoveryAfter;
            if (recovered) state.ResetHealthy(observation.Generation, now);
            if (restart) state.Health = SupervisorActorHealth.Critical;
            else if (pressure == SupervisorMailboxPressureState.Degraded || noProgress) state.Health = SupervisorActorHealth.Degraded;
            else state.Health = SupervisorActorHealth.Healthy;
            return new(threadId, state.Health, duration, warning, restart, recovered,
                recovered ? SupervisorMailboxPressureState.Normal : pressure, noProgress, longHandler);
        }
    }

    SupervisorMailboxPressureState ConfirmPressure(IncidentState state, long now, bool critical)
    {
        if (state.PressureSince == 0) state.PressureSince = now;
        if (Elapsed(state.PressureSince, now) < _policy.PressureConfirmation)
            return SupervisorMailboxPressureState.Normal;
        return critical ? SupervisorMailboxPressureState.Critical : SupervisorMailboxPressureState.Elevated;
    }

    TimeSpan Elapsed(long start, long end) => _timeProvider.GetElapsedTime(start, end);

    sealed class IncidentState
    {
        internal long FirstLimitTimestamp;
        internal long LastWarningTimestamp;
        internal long RecoverySince;
        internal long RecoveringSince;
        internal long PressureSince;
        internal long LastProgressTimestamp;
        internal long LastRejected;
        internal long LastDequeued;
        internal long LastCompleted;
        internal long Generation;
        internal SupervisorActorHealth Health = SupervisorActorHealth.Healthy;

        internal void Reset()
        {
            FirstLimitTimestamp = 0;
            LastWarningTimestamp = 0;
            RecoverySince = 0;
            RecoveringSince = 0;
            PressureSince = 0;
            Health = SupervisorActorHealth.Healthy;
        }

        internal void ResetHealthy(long generation, long now)
        {
            Reset();
            Generation = generation;
            LastProgressTimestamp = now;
        }
    }
}
