using Microsoft.Extensions.Diagnostics.HealthChecks;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.RealtimeMailboxes;

namespace TomasAI.IFM.Application.Api.Server.Core.HostedServices;

/// <summary>Archive capacity/failure affects recording readiness; archival ownership is never a financial handler receipt.</summary>
public sealed class SessionCaptureArchiveHealthCheck(DurableRealtimeSessionArchive archive) : IHealthCheck
{
    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var failure = archive.CurrentFailure?.ToString();
        var data = new Dictionary<string, object> { ["enabled"] = archive.Enabled, ["archiveOwnershipOnly"] = true,
            ["retainedBytes"] = archive.RetainedBytes, ["receiptBytes"] = archive.ReceiptBytes, ["pendingRecords"] = archive.PendingRecords };
        return Task.FromResult(!archive.Enabled || string.IsNullOrWhiteSpace(failure)
            ? HealthCheckResult.Healthy(archive.Enabled ? "Session source archive is available; business outcome reconciliation remains required." : "Session source recording is disabled.", data)
            : HealthCheckResult.Unhealthy("Session source recording is blocked: " + failure, data: data));
    }
}
