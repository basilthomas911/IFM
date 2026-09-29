using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.ReadModels;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Domain.Supervisor.Health.Evaluation;
using TomasAI.IFM.Domain.Supervisor.Logging;
using Microsoft.Extensions.Logging;

namespace TomasAI.IFM.Domain.Supervisor.Metrics;

/// <summary>Adapts read-only runtime metrics sources to Supervisor-owned immutable read models.</summary>
public sealed class SupervisorManagedActorMetricsSource(
    IActorRuntimeMetricsSourceProvider runtime,
    SupervisorActorThreadHealthEvaluator healthEvaluator,
    ILogger<SupervisorManagedActorMetricsSource> logger)
    : ISupervisorManagedActorMetricsSource
{
    readonly IActorRuntimeMetricsSourceProvider _runtime =
        runtime ?? throw new ArgumentNullException(nameof(runtime));
    readonly SupervisorActorThreadHealthEvaluator _healthEvaluator =
        healthEvaluator ?? throw new ArgumentNullException(nameof(healthEvaluator));
    readonly ILogger<SupervisorManagedActorMetricsSource> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc />
    public IReadOnlyList<ISupervisorActorMetricsSource> Current =>
        _runtime.CaptureActorMetricsSources()
            .Select(source => (ISupervisorActorMetricsSource)new ActorMetricsSource(source, _healthEvaluator, _logger))
            .ToArray();

    sealed class ActorMetricsSource(
        IActorRuntimeMetricsSource source,
        SupervisorActorThreadHealthEvaluator healthEvaluator,
        ILogger logger) : ISupervisorActorMetricsSource
    {
        public ActorMailboxId ActorId => source.ActorId;

        public SupervisorActorMetrics CaptureSnapshot()
        {
            var snapshot = source.CaptureSnapshot();
            var now = DateTime.UtcNow;
            var threads = snapshot.Mailboxes.Select(mailbox =>
            {
                var evaluation = healthEvaluator.Evaluate(
                    mailbox.ThreadId,
                    new SupervisorActorThreadHealthObservation(
                        mailbox.QueueDepth, mailbox.QueueCapacity, mailbox.Rejected, mailbox.Dequeued,
                        mailbox.Succeeded + mailbox.HandledFailures + mailbox.EscapedFailures + mailbox.Cancelled,
                        mailbox.IsProcessing,
                        mailbox.IsProcessing && mailbox.LastStartedUtc.HasValue
                            ? now - mailbox.LastStartedUtc.Value
                            : TimeSpan.Zero,
                        mailbox.Generation));
                if (evaluation.LogWarning)
                    SupervisorActorHealthLog.ActorThreadAtLimit(
                        logger,
                        mailbox.ThreadId,
                        mailbox.QueueDepth,
                        mailbox.QueueCapacity,
                        evaluation.ContinuousLimitDuration,
                        evaluation.Health);
                return new SupervisorActorThreadMetrics(
                    mailbox.ThreadId,
                    mailbox.QueueDepth,
                    mailbox.QueueCapacity,
                    mailbox.PeakQueueDepth,
                    mailbox.QueueDepth > 0 && mailbox.LastAcceptedUtc.HasValue
                        ? now - mailbox.LastAcceptedUtc.Value
                        : TimeSpan.Zero,
                    mailbox.Accepted,
                    mailbox.Dequeued,
                    mailbox.Succeeded,
                    mailbox.HandledFailures + mailbox.EscapedFailures,
                    mailbox.Cancelled,
                    mailbox.Rejected,
                    string.IsNullOrEmpty(mailbox.CurrentVerb) ? null : mailbox.CurrentVerb,
                    mailbox.IsProcessing ? "Execution" : null,
                    mailbox.IsProcessing && mailbox.LastStartedUtc.HasValue
                        ? now - mailbox.LastStartedUtc.Value
                        : TimeSpan.Zero,
                    evaluation.Health,
                    evaluation.ContinuousLimitDuration,
                    evaluation.RestartRequired,
                    mailbox.Generation,
                    evaluation.PressureState,
                    mailbox.QueueCapacity <= 0 ? 0 : (double)mailbox.QueueDepth / mailbox.QueueCapacity,
                    Math.Max(0, mailbox.QueueCapacity - mailbox.QueueDepth),
                    mailbox.LastCompletedUtc ?? mailbox.LastStartedUtc,
                    mailbox.QueueDepth == 0 || mailbox.LastCompletedUtc.HasValue,
                    evaluation.HandlerDurationWarning,
                    evaluation.NoProgressWarning);
            }).ToArray();
            var health = !snapshot.IsRunning
                ? SupervisorActorHealth.Critical
                : threads.Any(static thread => thread.Health == SupervisorActorHealth.Critical)
                    ? SupervisorActorHealth.Critical
                    : threads.Any(static thread => thread.Health == SupervisorActorHealth.Degraded)
                        ? SupervisorActorHealth.Degraded
                        : Map(snapshot.Status);
            return new(
                snapshot.ActorId,
                snapshot.Generation,
                snapshot.IsRunning,
                health,
                threads.Length,
                snapshot.QueueDepth,
                snapshot.Rejected,
                snapshot.HandledFailures + snapshot.EscapedFailures,
                SupervisorSnapshotQuality.Complete,
                threads);
        }

        static SupervisorActorHealth Map(SupervisorActorHealthStatus status) => status switch
        {
            SupervisorActorHealthStatus.Green => SupervisorActorHealth.Healthy,
            SupervisorActorHealthStatus.Yellow => SupervisorActorHealth.Degraded,
            SupervisorActorHealthStatus.Red => SupervisorActorHealth.Critical,
            _ => SupervisorActorHealth.Unknown
        };
    }
}
