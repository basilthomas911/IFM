using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventProjector.ReadModels;

namespace TomasAI.IFM.Application.EventProjector;

/// <summary>Qualifies retry, transition and outbox decisions before asking Storage to enforce their fences.</summary>
internal static class EventProjectorTransitionPreparation
{
    extension(IEventSourceActorDbWriteContext database)
    {
        internal Task<EventProjectorExecutionStateReadModel?> ReleaseQualifiedExecutionAsync(
            EventProjectorStateTransition transition, DateTime nowUtc, CancellationToken cancellationToken)
        {
            RequireTransition(transition, terminal: false);
            if (transition.Outcome != EventProjectorOutcomeType.Retrying)
                throw new ArgumentOutOfRangeException(nameof(transition), "A released execution must be retryable.");
            if (!transition.NextAttemptAtUtc.HasValue || !transition.LastErrorAtUtc.HasValue)
                throw new ArgumentException("A released execution requires retry and error timestamps.", nameof(transition));
            RequireUtc(nowUtc, nameof(nowUtc));
            return database.TryReleaseEventProjectorExecutionAsync(transition, nowUtc, cancellationToken);
        }

        internal Task<EventProjectorExecutionStateReadModel?> TransitionQualifiedExecutionAsync(
            EventProjectorStateTransition transition, DateTime nowUtc, CancellationToken cancellationToken)
        {
            RequireTransition(transition, terminal: false);
            RequireUtc(nowUtc, nameof(nowUtc));
            return database.TryTransitionEventProjectorExecutionAsync(transition, nowUtc, cancellationToken);
        }

        internal Task<EventProjectorExecutionStateReadModel?> TerminalizeQualifiedExecutionAsync(
            EventProjectorStateTransition transition, DateTime nowUtc, CancellationToken cancellationToken)
        {
            RequireTransition(transition, terminal: true);
            RequireUtc(nowUtc, nameof(nowUtc));
            return database.TryTerminalizeEventProjectorExecutionAsync(transition, nowUtc, cancellationToken);
        }

        internal Task<EventProjectorExecutionStateReadModel?> TransitionQualifiedExecutionWithOutboxAsync(
            EventProjectorStateTransition transition, EventProjectorOutboxMessage message, DateTime nowUtc, CancellationToken cancellationToken)
        {
            RequireTransition(transition, terminal: false);
            RequireOutbox(transition, message);
            RequireUtc(nowUtc, nameof(nowUtc));
            return database.TryTransitionEventProjectorExecutionWithOutboxAsync(transition, message, nowUtc, cancellationToken);
        }

        internal Task<EventProjectorExecutionStateReadModel?> TerminalizeQualifiedExecutionWithOutboxAsync(
            EventProjectorStateTransition transition, EventProjectorOutboxMessage message, DateTime nowUtc, CancellationToken cancellationToken)
        {
            RequireTransition(transition, terminal: true);
            RequireOutbox(transition, message);
            RequireUtc(nowUtc, nameof(nowUtc));
            return database.TryTerminalizeEventProjectorExecutionWithOutboxAsync(transition, message, nowUtc, cancellationToken);
        }
    }

    static void RequireTransition(EventProjectorStateTransition transition, bool terminal)
    {
        ArgumentNullException.ThrowIfNull(transition);
        if (transition.EventId <= 0)
            throw new ArgumentOutOfRangeException("eventId", transition.EventId, "The event id must be positive.");
        ArgumentException.ThrowIfNullOrWhiteSpace(transition.ProjectorName, "projectorName");
        if (transition.ExecutionToken == Guid.Empty)
            throw new ArgumentException("The execution token cannot be empty.", "executionToken");
        if (transition.ExpectedRevision < 0 || transition.RetryCount < 0)
            throw new ArgumentOutOfRangeException(nameof(transition));
        if (transition.ExpectedStage == EventProjectorStageType.Completed)
            throw new ArgumentOutOfRangeException(nameof(transition), "A completed execution cannot transition again.");
        if (transition.NextAttemptAtUtc is { } next) RequireUtc(next, nameof(transition.NextAttemptAtUtc));
        if (transition.LastErrorAtUtc is { } last) RequireUtc(last, nameof(transition.LastErrorAtUtc));
        if (terminal)
        {
            if (transition.Outcome is EventProjectorOutcomeType.Processing or EventProjectorOutcomeType.Retrying)
                throw new ArgumentOutOfRangeException(nameof(transition), "A terminal transition requires a terminal outcome.");
        }
        else
        {
            if (transition.NextStage == EventProjectorStageType.Completed)
                throw new ArgumentOutOfRangeException(nameof(transition), "Use terminalization to complete an execution.");
            if (transition.Outcome is not (EventProjectorOutcomeType.Processing or EventProjectorOutcomeType.Retrying))
                throw new ArgumentOutOfRangeException(nameof(transition), "A non-terminal transition requires an active outcome.");
        }
    }

    static void RequireOutbox(EventProjectorStateTransition transition, EventProjectorOutboxMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.Identity.EventId != transition.EventId ||
            !string.Equals(message.Identity.ProjectorName, transition.ProjectorName, StringComparison.Ordinal))
            throw new ArgumentException("The outbox identity must match the transitioned projection.", nameof(message));
        if (message.Identity.EffectKind is not (EventProjectorEffectKind.ProcessingPublication or
            EventProjectorEffectKind.CompletedPublication or EventProjectorEffectKind.FailedPublication))
            throw new ArgumentOutOfRangeException(nameof(message), "Only publication effects can be staged in the outbox.");
        ArgumentException.ThrowIfNullOrWhiteSpace(message.EventTypeName, nameof(message));
        ArgumentNullException.ThrowIfNull(message.EventPayload, nameof(message));
        if (message.EventPayload.Length == 0)
            throw new ArgumentException("The outbox payload cannot be empty.", nameof(message));
    }

    static void RequireUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
            throw new ArgumentException("The timestamp must use DateTimeKind.Utc.", parameterName);
    }
}
