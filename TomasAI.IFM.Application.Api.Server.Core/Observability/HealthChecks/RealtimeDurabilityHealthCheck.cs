using Microsoft.Extensions.Diagnostics.HealthChecks;
using TomasAI.IFM.Domain.BrokerAccount.Realtime;
using TomasAI.IFM.Domain.Trade.Persistence.Realtime;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.RealtimeMailboxes;

namespace TomasAI.IFM.Application.Api.Server.Core.Observability.HealthChecks;

public sealed class RealtimeDurabilityHealthCheck(DurableRealtimeMailboxHub hub, PositionRealtimeIntents positions,
    BrokerAccountObservationBridge accounts) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        IReadOnlyDictionary<string, object> data = new Dictionary<string, object>
        {
            ["enabled"] = hub.Enabled, ["running"] = hub.IsRunning, ["mailboxes"] = hub.MailboxCount,
            ["pendingSources"] = hub.PendingSourceCount, ["pendingSourceBytes"] = hub.PendingSourceBytes,
            ["retainedSourceBytes"] = hub.RetainedSourceBytes,
            ["receiptBytes"] = hub.ReceiptBytes, ["brokerStoredBytes"] = hub.BrokerStoredBytes, ["brokerStoredMessages"] = hub.BrokerStoredMessages, ["brokerPendingMessages"] = hub.BrokerPendingMessages,
            ["brokerUnacknowledgedMessages"] = hub.BrokerUnacknowledgedMessages, ["published"] = hub.Metrics.PublishedDestinations,
            ["acknowledged"] = hub.Metrics.AcknowledgedDeliveries, ["failedDeliveries"] = hub.Metrics.FailedDeliveries,
            ["fencedDeliveries"] = hub.Metrics.FencedDeliveries, ["positionIntentBytes"] = positions.RetainedBytes,
            ["accountSnapshotsConfirmed"] = accounts.ConfirmedSnapshots,
            ["lastTransportIncident"] = hub.LastFailure ?? "none"
        };
        var failure = !positions.IsHealthy ? positions.LastFailure ?? "Position intent journal unavailable."
            : !accounts.IsHealthy ? accounts.LastFailure ?? "Account convergence unavailable."
            : hub.Enabled && !hub.IsRunning ? "Realtime durable mailbox transport is not running."
            : hub.Enabled ? hub.CurrentFailure : null;
        return Task.FromResult(failure is null
            ? HealthCheckResult.Healthy(hub.Enabled ? "Realtime durable ownership is active." : "Realtime durable transport remains disabled pending qualification.", data)
            : HealthCheckResult.Unhealthy(failure, data: data));
    }
}
