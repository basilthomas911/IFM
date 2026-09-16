using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream;

/// <summary>Configures bounded end-of-day removal of completed JetStream transport messages.</summary>
public sealed class NatsJetStreamEndOfDayMaintenanceOptions
{
    /// <summary>Gets or sets the NATS server URL.</summary>
    public string Url { get; set; } = "nats://localhost:4222";

    /// <summary>Gets or sets regular expressions that select streams owned by the production event transport.</summary>
    public string[] StreamNamePatterns { get; set; } =
    [
        "^EventStream$",
        "^IFM_[A-Z][A-Za-z0-9]*Projector_(PROCESS|REPLAY)$"
    ];

    /// <summary>Gets or sets the durable production consumer that must drain on the shared event stream.</summary>
    public string EventStreamConsumerName { get; set; } = "EventConsumer";

    /// <summary>Gets or sets the maximum time allowed for each stream to drain.</summary>
    public int DrainTimeoutSeconds { get; set; } = 120;

    /// <summary>Gets or sets the interval between consumer-state checks.</summary>
    public int DrainPollMilliseconds { get; set; } = 500;

    /// <summary>Validates configuration before any JetStream mutation occurs.</summary>
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Url);
        ArgumentException.ThrowIfNullOrWhiteSpace(EventStreamConsumerName);
        if (DrainTimeoutSeconds <= 0 || DrainPollMilliseconds <= 0)
            throw new InvalidOperationException("JetStream drain timeout and poll interval must be positive.");
        if (StreamNamePatterns.Length == 0 || StreamNamePatterns.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("At least one non-empty JetStream stream-name pattern is required.");
        foreach (var pattern in StreamNamePatterns)
            _ = new Regex(pattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    }
}

/// <summary>Describes one completed end-of-day stream purge.</summary>
public sealed record JetStreamEndOfDayStreamResult(
    string StreamName,
    ulong CapturedLastSequence,
    ulong CapturedMessages,
    ulong PurgedMessages);

/// <summary>Describes all transport messages removed by one end-of-day run.</summary>
public sealed record JetStreamEndOfDayMaintenanceResult(
    IReadOnlyList<JetStreamEndOfDayStreamResult> Streams)
{
    /// <summary>Gets the total number of purged messages.</summary>
    public ulong PurgedMessages => Streams.Aggregate(0UL, static (total, stream) => checked(total + stream.PurgedMessages));
}

/// <summary>Removes completed NATS transport history while preserving streams, consumers, and durable business data.</summary>
public interface INatsJetStreamEndOfDayMaintenance
{
    /// <summary>Waits for production consumers to drain and purges only messages at or below captured stream watermarks.</summary>
    Task<JetStreamEndOfDayMaintenanceResult> PurgeCompletedMessagesAsync(CancellationToken cancellationToken);
}

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
        : this(options, new NatsJetStreamMaintenanceTransport(options.Url, connectionManager), logger)
    {
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
            _logger.LogInformation(
                "Purged {PurgedMessages} completed JetStream messages from {StreamName} through sequence {LastSequence}; stream and consumers were retained.",
                purged,
                name,
                captured.LastSequence);
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
        => string.Equals(streamName, "EventStream", StringComparison.Ordinal)
            ? 0
            : streamName.EndsWith("_PROCESS", StringComparison.Ordinal)
                ? 1
                : 2;
}

internal sealed record JetStreamMaintenanceConsumer(
    string Name,
    ulong Pending,
    ulong AcknowledgementPending);

internal sealed record JetStreamMaintenanceSnapshot(
    string StreamName,
    ulong Messages,
    ulong LastSequence,
    IReadOnlyList<JetStreamMaintenanceConsumer> Consumers);

internal interface INatsJetStreamMaintenanceTransport
{
    ValueTask<IReadOnlyList<string>> ListStreamNamesAsync(CancellationToken cancellationToken);
    ValueTask<JetStreamMaintenanceSnapshot> GetSnapshotAsync(string streamName, CancellationToken cancellationToken);
    ValueTask<ulong> PurgeBeforeSequenceAsync(string streamName, ulong sequence, CancellationToken cancellationToken);
}

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
