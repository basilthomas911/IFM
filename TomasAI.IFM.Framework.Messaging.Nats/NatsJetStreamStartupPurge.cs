using Microsoft.Extensions.Logging;
namespace TomasAI.IFM.Framework.Messaging.NatsJetStream;

/// <summary>Purges selected pre-startup transport messages without deleting streams or consumers.</summary>
public sealed class NatsJetStreamStartupPurge
{
    readonly INatsJetStreamMaintenanceTransport _transport;
    readonly ILogger<NatsJetStreamStartupPurge> _logger;

    public NatsJetStreamStartupPurge(string url, NatsConnectionManager connectionManager,
        ILogger<NatsJetStreamStartupPurge> logger)
        : this(new NatsJetStreamMaintenanceTransport(url, connectionManager), logger) { }

    internal NatsJetStreamStartupPurge(INatsJetStreamMaintenanceTransport transport,
        ILogger<NatsJetStreamStartupPurge> logger)
    {
        _transport = transport;
        _logger = logger;
    }

    public async Task PurgeAsync(Func<string, bool> shouldPurge, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(shouldPurge);
        var names = await _transport.ListStreamNamesAsync(cancellationToken).ConfigureAwait(false);
        foreach (var name in names.Where(shouldPurge).OrderBy(static name => name, StringComparer.Ordinal))
        {
            var snapshot = await _transport.GetSnapshotAsync(name, cancellationToken).ConfigureAwait(false);
            if (snapshot.LastSequence == 0 || snapshot.Messages == 0) continue;

            // New publications after this snapshot have a greater sequence and survive.
            var purged = await _transport.PurgeBeforeSequenceAsync(
                    name, checked(snapshot.LastSequence + 1), cancellationToken)
                .ConfigureAwait(false);
            _logger.LogInformation(
                "Startup purged {PurgedMessages} JetStream messages from {StreamName} through sequence {LastSequence}.",
                purged, name, snapshot.LastSequence);
        }
    }
}
