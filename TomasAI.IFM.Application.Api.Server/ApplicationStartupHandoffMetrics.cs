using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace TomasAI.IFM.Application.Api.Server;

/// <summary>Low-cardinality telemetry for the API-to-lifecycle startup handoff.</summary>
static class ApplicationStartupHandoffMetrics
{
    internal const string MeterName = "TomasAI.IFM.Application.Api.Server.StartupHandoff";

    static readonly Meter Meter = new(MeterName, "1.0.0");
    static readonly Counter<long> Transitions = Meter.CreateCounter<long>(
        "ifm.application.startup_handoff.transitions");
    static readonly Histogram<double> ObservationDuration = Meter.CreateHistogram<double>(
        "ifm.application.startup_handoff.observation.duration",
        "ms");

    internal static void Record(ApplicationStartupHandoffStatus status)
    {
        var tags = new TagList
        {
            { "state", status.State.ToString() },
            { "attempt", status.AttemptCount }
        };
        Transitions.Add(1, tags);

        if (status.AcceptedAtUtc is { } acceptedAtUtc
            && status.ObservedAtUtc is { } observedAtUtc)
        {
            ObservationDuration.Record(
                Math.Max(0, (observedAtUtc - acceptedAtUtc).TotalMilliseconds),
                tags);
        }
    }
}
