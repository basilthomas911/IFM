using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace TomasAI.IFM.Shared.EventChannel;

/// <summary>
/// Describes the operating state of a bounded, lossless, ordered batch channel.
/// </summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
public readonly record struct OrderedBatchChannelMetrics(
    long AcceptedCount,
    long ProcessedCount,
    long BatchCount,
    long BackpressuredWriteCount,
    long FailureCount,
    double AcceptedPerSecond,
    int Capacity,
    TimeSpan LastQueueDelay,
    TimeSpan MaximumQueueDelay,
    TimeSpan LastBatchDuration,
    TimeSpan MaximumBatchDuration,
    bool IsOpen);
