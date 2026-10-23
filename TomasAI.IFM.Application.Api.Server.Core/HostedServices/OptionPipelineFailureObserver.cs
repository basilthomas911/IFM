using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Api.Server.Core.HostedServices;

/// <summary>Observes unresolved disposable option mailbox failures without changing admission or restarting anything.</summary>
public sealed class OptionPipelineFailureObserver(TimeProvider clock)
{
    public static readonly TimeSpan RecoveryDeadline = TimeSpan.FromMinutes(2);
    readonly Dictionary<ActorThreadId, Episode> episodes = [];
    DateOnly? session;

    /// <summary>Returns due failures; callers may retry publication with the same episode identity.</summary>
    public IReadOnlyList<MarketDataDatasetRecoveryFailedEvent> Observe(DateOnly? valueDate, IEnumerable<ActorMailboxMetricsSnapshot> mailboxes)
    {
        if (session != valueDate) { episodes.Clear(); session = valueDate; }
        if (valueDate is null) return [];
        var now = clock.GetUtcNow().UtcDateTime;
        var observed = new HashSet<ActorThreadId>();
        var failures = new List<MarketDataDatasetRecoveryFailedEvent>();
        foreach (var mailbox in mailboxes)
        {
            if (mailbox.ThreadId.ActorType != ActorType.Realtime
                || mailbox.ThreadId.Name is not ("IronCondorTradePositionRealtime" or "FuturesOptionRealtime")) continue;
            observed.Add(mailbox.ThreadId);
            var handlerStalled = mailbox.IsProcessing && mailbox.LastStartedUtc is { } started
                && now - started >= RecoveryDeadline;
            var reason = mailbox.LifecycleState == ActorMailboxLifecycleState.Quarantined ? "mailbox quarantined"
                : !mailbox.IsAdmissionOpen && (mailbox.QueueDepth > 0 || mailbox.IsProcessing || mailbox.LastRejectionReason == "Stopping") ? "mailbox admission remains closed"
                : mailbox.QueueCapacity > 0 && mailbox.QueueDepth >= mailbox.QueueCapacity ? "mailbox remains saturated"
                : handlerStalled ? "active handler has not completed within two minutes" : null;
            if (reason is null) { episodes.Remove(mailbox.ThreadId); continue; }
            if (!episodes.TryGetValue(mailbox.ThreadId, out var episode))
                episodes[mailbox.ThreadId] = episode = new(Guid.NewGuid(), clock.GetTimestamp());
            // A handler already observed running for two minutes has exhausted the budget.
            if (!handlerStalled && clock.GetElapsedTime(episode.Started) < RecoveryDeadline) continue;
            if (episode.LastPublished is { } last && clock.GetElapsedTime(last) < TimeSpan.FromSeconds(10)) continue;
            var id = new MarketDataFeedId(valueDate.Value);
            failures.Add(new()
            {
                Id = Guid.NewGuid(), CommandId = episode.Id, EntityId = id,
                Subject = new(ActorType.Event, MarketDataDatasetRecoveryFailedEvent.Actor, MarketDataDatasetRecoveryFailedEvent.Verb, id.Format()),
                ErrorDate = now, ReceivedOn = now, EventSource = nameof(OptionPipelineFailureObserver),
                ErrorCode = 503, ErrorType = ErrorType.System, Dataset = "GLBX.MDP3", Role = "Options",
                ErrorMessage = $"Option pipeline recovery failed after 120 seconds: {mailbox.ThreadId}; {reason}. "
                    + "Fresh trade monitoring is unavailable. Notification-only mode: automatic API restart is disabled. "
                    + "Check the live trade before taking further action."
            });
        }
        foreach (var missing in episodes.Keys.Where(key => !observed.Contains(key)).ToArray()) episodes.Remove(missing);
        return failures;
    }

    /// <summary>Only successful acknowledged publication starts repeat suppression.</summary>
    public void MarkPublished(Guid operationId)
    {
        foreach (var episode in episodes.Values.Where(value => value.Id == operationId)) episode.LastPublished = clock.GetTimestamp();
    }

    sealed class Episode(Guid id, long started)
    {
        public Guid Id { get; } = id;
        public long Started { get; } = started;
        public long? LastPublished { get; set; }
    }
}
