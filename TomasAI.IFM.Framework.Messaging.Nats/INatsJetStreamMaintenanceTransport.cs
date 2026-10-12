using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream;

internal interface INatsJetStreamMaintenanceTransport
{
    ValueTask<IReadOnlyList<string>> ListStreamNamesAsync(CancellationToken cancellationToken);
    ValueTask<JetStreamMaintenanceSnapshot> GetSnapshotAsync(string streamName, CancellationToken cancellationToken);
    ValueTask<ulong> PurgeBeforeSequenceAsync(string streamName, ulong sequence, CancellationToken cancellationToken);
}
