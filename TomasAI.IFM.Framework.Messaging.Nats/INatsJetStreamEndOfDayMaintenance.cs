using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream;

/// <summary>Removes completed NATS transport history while preserving streams, consumers, and durable business data.</summary>
public interface INatsJetStreamEndOfDayMaintenance
{
    /// <summary>Waits for production consumers to drain and purges only messages at or below captured stream watermarks.</summary>
    Task<JetStreamEndOfDayMaintenanceResult> PurgeCompletedMessagesAsync(CancellationToken cancellationToken);
}
