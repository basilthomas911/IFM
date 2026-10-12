using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace TomasAI.IFM.Framework.Telemetry.Metrics;

/// <summary>A timestamped process-lifetime GC observation; heap sizes describe the last GC, including fragmentation.</summary>
public sealed record ProcessGcSnapshot(
    string Service, string ProcessRunId, int ProcessId, DateTimeOffset ProcessStartedUtc,
    DateTimeOffset RecordedUtc, string Phase, double UptimeSeconds,
    int Gen0CollectionsSinceStart, int Gen1CollectionsSinceStart, int Gen2CollectionsSinceStart,
    long AllocatedBytesSinceStartApproximate, double PauseMillisecondsSinceStart,
    long LastGcHeapBytes, long LastGcCommittedBytes, long LastGcFragmentedBytes,
    long LastGcLargeObjectHeapBytes, long WorkingSetBytes);
