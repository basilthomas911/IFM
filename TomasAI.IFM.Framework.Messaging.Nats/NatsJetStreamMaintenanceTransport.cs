using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream;

internal sealed class NatsJetStreamMaintenanceTransport(
    string url,
    NatsConnectionManager connectionManager) : INatsJetStreamMaintenanceTransport
{
    public async ValueTask<IReadOnlyList<string>> ListStreamNamesAsync(CancellationToken cancellationToken)
    {
        var context = await connectionManager.GetJetStreamContextAsync(url, cancellationToken).ConfigureAwait(false);
        var names = new List<string>();
        await foreach (var stream in context.ListStreamsAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
            names.Add(stream.Info.Config.Name);
        return names;
    }

    public async ValueTask<JetStreamMaintenanceSnapshot> GetSnapshotAsync(
        string streamName,
        CancellationToken cancellationToken)
    {
        var context = await connectionManager.GetJetStreamContextAsync(url, cancellationToken).ConfigureAwait(false);
        var stream = await context.GetStreamAsync(streamName, cancellationToken: cancellationToken).ConfigureAwait(false);
        var consumers = new List<JetStreamMaintenanceConsumer>();
        await foreach (var consumer in stream.ListConsumersAsync(cancellationToken).ConfigureAwait(false))
        {
            consumers.Add(new(
                consumer.Info.Name,
                Convert.ToUInt64(consumer.Info.NumPending),
                Convert.ToUInt64(consumer.Info.NumAckPending)));
        }

        return new(
            streamName,
            Convert.ToUInt64(stream.Info.State.Messages),
            Convert.ToUInt64(stream.Info.State.LastSeq),
            consumers);
    }

    public async ValueTask<ulong> PurgeBeforeSequenceAsync(
        string streamName,
        ulong sequence,
        CancellationToken cancellationToken)
    {
        var context = await connectionManager.GetJetStreamContextAsync(url, cancellationToken).ConfigureAwait(false);
        var response = await context.PurgeStreamAsync(
            streamName,
            new StreamPurgeRequest { Seq = sequence },
            cancellationToken).ConfigureAwait(false);
        return Convert.ToUInt64(response.Purged);
    }
}
