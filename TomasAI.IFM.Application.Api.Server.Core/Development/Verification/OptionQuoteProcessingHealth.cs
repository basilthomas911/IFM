using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Application.Api.Server.Core.Development.Verification;

/// <summary>Checks quote-processing admission and backlog independently of provider connectivity.</summary>
public static class OptionQuoteProcessingHealth
{
    /// <summary>Reports required degradation for closed, full or stalled option mailboxes, including empty quarantined queues.</summary>
    public static LivePipelineCheck Evaluate(ActorMetricsSnapshot metrics, bool running, bool accepting, DateTime nowUtc, bool positionUpdatesHealthy = true)
    {
        var closed = metrics.Mailboxes.Count(mailbox => !mailbox.IsAdmissionOpen);
        var full = metrics.Mailboxes.Count(mailbox => mailbox.QueueCapacity > 0 && mailbox.QueueDepth >= mailbox.QueueCapacity);
        var stalled = metrics.Mailboxes.Count(mailbox => (mailbox.QueueDepth > 0 || mailbox.IsProcessing)
            && ((mailbox.IsProcessing ? mailbox.LastStartedUtc : mailbox.LastCompletedUtc) ?? nowUtc) < nowUtc - TimeSpan.FromSeconds(30));
        var healthy = running && accepting && closed == 0 && full == 0 && stalled == 0 && positionUpdatesHealthy;
        return new("Option quote processing", "option-router", healthy ? "Healthy" : "Degraded",
            $"Running={running}; AdmissionOpen={accepting}; ClosedMailboxes={closed}; FullMailboxes={full}; StalledMailboxes={stalled}; PositionUpdatesHealthy={positionUpdatesHealthy}.",
            nowUtc, metrics.Mailboxes.Select(mailbox => mailbox.LastCompletedUtc).Max());
    }
}
