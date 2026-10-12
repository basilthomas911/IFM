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
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056", Justification = "Preserve the published interface/configuration contract used by actor callers, named arguments, and serialized settings.")]
    public string Url { get; set; } = "nats://localhost:4222";

    /// <summary>Gets or sets regular expressions that select streams owned by the production event transport.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized message/configuration contract, including enum widths, numeric values, and collection shape.")]
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
        if (string.IsNullOrWhiteSpace(Url)) throw new InvalidOperationException("JetStream Url is required.");
        if (string.IsNullOrWhiteSpace(EventStreamConsumerName)) throw new InvalidOperationException("EventStreamConsumerName is required.");
        if (DrainTimeoutSeconds <= 0 || DrainPollMilliseconds <= 0)
            throw new InvalidOperationException("JetStream drain timeout and poll interval must be positive.");
        if (StreamNamePatterns.Length == 0 || StreamNamePatterns.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("At least one non-empty JetStream stream-name pattern is required.");
        foreach (var pattern in StreamNamePatterns)
            _ = new Regex(pattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    }
}
