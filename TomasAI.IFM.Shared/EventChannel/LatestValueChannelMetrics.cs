using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace TomasAI.IFM.Shared.EventChannel;

/// <summary>
/// Describes the current operating state of a latest-value channel.
/// </summary>
/// <param name="AcceptedCount">The number of values accepted while the channel was open.</param>
/// <param name="ProcessedCount">The number of values whose reader callback completed successfully.</param>
/// <param name="CoalescedCount">The number of pending values superseded by a newer value.</param>
/// <param name="FailureCount">The number of reader callback failures.</param>
/// <param name="AcceptedPerSecond">The average accepted event rate since the channel was created.</param>
/// <param name="LastQueueDelay">The queue delay of the most recently dispatched value.</param>
/// <param name="MaximumQueueDelay">The greatest observed queue delay.</param>
/// <param name="LastProcessingDuration">The processing duration of the most recently completed callback.</param>
/// <param name="MaximumProcessingDuration">The greatest observed callback processing duration.</param>
/// <param name="IsOpen">Whether the channel still accepts writes.</param>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
public readonly record struct LatestValueChannelMetrics(
    long AcceptedCount,
    long ProcessedCount,
    long CoalescedCount,
    long FailureCount,
    double AcceptedPerSecond,
    TimeSpan LastQueueDelay,
    TimeSpan MaximumQueueDelay,
    TimeSpan LastProcessingDuration,
    TimeSpan MaximumProcessingDuration,
    bool IsOpen);
