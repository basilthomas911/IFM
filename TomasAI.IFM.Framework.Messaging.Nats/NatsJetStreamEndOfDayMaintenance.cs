using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream;

/// <summary>Implements safe, watermark-bounded end-of-day JetStream maintenance.</summary>
public sealed class NatsJetStreamEndOfDayMaintenance : INatsJetStreamEndOfDayMaintenance
{
    readonly NatsJetStreamEndOfDayMaintenanceOptions _options;
    readonly INatsJetStreamMaintenanceTransport _transport;
    readonly ILogger<NatsJetStreamEndOfDayMaintenance> _logger;
    readonly Regex[] _streamPatterns;

    /// <summary>Creates maintenance backed by the process-shared NATS connection.</summary>
    public NatsJetStreamEndOfDayMaintenance(
        NatsJetStreamEndOfDayMaintenanceOptions options,
        NatsConnectionManager connectionManager,
        ILogger<NatsJetStreamEndOfDayMaintenance> logger)
        : this(options, new NatsJetStreamMaintenanceTransport((options ?? throw new ArgumentNullException(nameof(options))).Url, connectionManager ?? throw new ArgumentNullException(nameof(connectionManager))), logger)
    {
        ArgumentNullException.ThrowIfNull(connectionManager);
        ArgumentNullException.ThrowIfNull(options);
    }

    internal NatsJetStreamEndOfDayMaintenance(
        NatsJetStreamEndOfDayMaintenanceOptions options,
        INatsJetStreamMaintenanceTransport transport,
        ILogger<NatsJetStreamEndOfDayMaintenance> logger)
    {
        options.Validate();
        _options = options;
        _transport = transport;
        _logger = logger;
        _streamPatterns = options.StreamNamePatterns
            .Select(static pattern => new Regex(
                pattern,
                RegexOptions.CultureInvariant | RegexOptions.Compiled,
                TimeSpan.FromSeconds(1)))
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<JetStreamEndOfDayMaintenanceResult> PurgeCompletedMessagesAsync(
        CancellationToken cancellationToken)
    {
        var names = (await _transport.ListStreamNamesAsync(cancellationToken).ConfigureAwait(false))
            .Where(IsTargetStream)
            .OrderBy(StreamOrder)
            .ThenBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        if (!names.Contains("EventStream", StringComparer.Ordinal))
            throw new InvalidOperationException("Required JetStream stream 'EventStream' was not found.");

        var results = new List<JetStreamEndOfDayStreamResult>(names.Length);
        foreach (var name in names)
        {
            var captured = await _transport.GetSnapshotAsync(name, cancellationToken).ConfigureAwait(false);
            if (captured.Messages == 0)
            {
                results.Add(new(name, captured.LastSequence, 0, 0));
                continue;
            }

            await WaitForDrainAsync(name, cancellationToken).ConfigureAwait(false);
            var purged = await _transport
                .PurgeBeforeSequenceAsync(name, checked(captured.LastSequence + 1), cancellationToken)
                .ConfigureAwait(false);
            global::TomasAI.IFM.Framework.Messaging.NatsJetStream.PersistenceDiagnosticLog.Write38(_logger, null, nameof(NatsJetStreamEndOfDayMaintenance), nameof(PurgeCompletedMessagesAsync), purged, name, captured.LastSequence);
            results.Add(new(name, captured.LastSequence, captured.Messages, purged));
        }

        return new(results);
    }

    async Task WaitForDrainAsync(string streamName, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        var timeout = TimeSpan.FromSeconds(_options.DrainTimeoutSeconds);
        JetStreamMaintenanceSnapshot last = default!;
        do
        {
            last = await _transport.GetSnapshotAsync(streamName, cancellationToken).ConfigureAwait(false);
            var consumers = RelevantConsumers(streamName, last.Consumers);
            if (consumers.Count > 0 && consumers.All(static consumer => consumer.Pending == 0 && consumer.AcknowledgementPending == 0))
                return;
            await Task.Delay(_options.DrainPollMilliseconds, cancellationToken).ConfigureAwait(false);
        }
        while (Stopwatch.GetElapsedTime(started) < timeout);

        var blocked = string.Join(
            ", ",
            RelevantConsumers(streamName, last.Consumers)
                .Select(static consumer => $"{consumer.Name}:pending={consumer.Pending},ack-pending={consumer.AcknowledgementPending}"));
        throw new TimeoutException(
            $"JetStream stream '{streamName}' did not drain within {timeout}. Consumers: {blocked}.");
    }

    IReadOnlyList<JetStreamMaintenanceConsumer> RelevantConsumers(
        string streamName,
        IReadOnlyList<JetStreamMaintenanceConsumer> consumers)
        => string.Equals(streamName, "EventStream", StringComparison.Ordinal)
            ? consumers.Where(consumer => string.Equals(
                consumer.Name,
                _options.EventStreamConsumerName,
                StringComparison.Ordinal)).ToArray()
            : consumers;

    bool IsTargetStream(string streamName) => _streamPatterns.Any(pattern => pattern.IsMatch(streamName));

    static int StreamOrder(string streamName)
    {
        if (string.Equals(streamName, "EventStream", StringComparison.Ordinal)) return 0;
        return streamName.EndsWith("_PROCESS", StringComparison.Ordinal) ? 1 : 2;
    }
}
