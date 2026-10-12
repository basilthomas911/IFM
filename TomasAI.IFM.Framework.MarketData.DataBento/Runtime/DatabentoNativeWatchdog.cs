using System.Runtime.InteropServices;
using System.Text;
using TomasAI.IFM.Framework.MarketData.DataBento.Interop;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

/// <summary>Reads every native feed from the selected C++ or Rust backend in one FFI operation.</summary>
public static class DatabentoNativeWatchdog
{
    /// <summary>Attempts to read the next available batch or current snapshot.</summary>
    /// <param name="snapshot">The market data snapshot to update, or the snapshot returned when the read succeeds.</param>
    /// <param name="failureDetail">When the operation fails, receives the diagnostic failure detail.</param>
    /// <returns>True when the operation succeeds or the requested condition holds; otherwise, false.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "The diagnostic TryRead boundary reports native availability failures through failureDetail rather than throwing.")]
    public static bool TryRead(out DatabentoNativeWatchdogSnapshot snapshot, out string failureDetail)
    {
        try
        {
            snapshot = Read();
            failureDetail = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            snapshot = new(0, 0, []);
            failureDetail = exception.Message[..Math.Min(exception.Message.Length, 512)];
            return false;
        }
    }

    /// <summary>Reads the next batch or current native watchdog snapshot.</summary>
    /// <returns>The read result.</returns>
    public static unsafe DatabentoNativeWatchdogSnapshot Read()
    {
        var snapshot = Header();
        var status = NativeMethods.GetWatchdogSnapshot(&snapshot, null, 0);
        if (status == DatabentoFeedStatus.Ok)
            return new(snapshot.ObservedMonotonicNanoseconds, snapshot.SnapshotSequence, []);
        if (status != DatabentoFeedStatus.BufferTooSmall)
            throw new DatabentoFeedException(status, "Unable to size the native watchdog snapshot.");

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var capacity = snapshot.RequiredCount;
            var entries = (NativeWatchdogFeedStatus*)NativeMemory.Alloc(
                checked((nuint)capacity), (nuint)sizeof(NativeWatchdogFeedStatus));
            try
            {
                for (var index = 0u; index < capacity; index++)
                {
                    entries[index] = default;
                    entries[index].StructSize = (uint)sizeof(NativeWatchdogFeedStatus);
                    entries[index].AbiVersion = NativeConstants.AbiVersion;
                }
                snapshot = Header();
                status = NativeMethods.GetWatchdogSnapshot(&snapshot, entries, capacity);
                if (status == DatabentoFeedStatus.BufferTooSmall)
                    continue;
                if (status != DatabentoFeedStatus.Ok || snapshot.EntryCount != snapshot.RequiredCount)
                    throw new DatabentoFeedException(status,
                        "The native watchdog snapshot was incomplete or invalid.");
                var values = new DatabentoNativeFeedWatchdogStatus[snapshot.EntryCount];
                for (var index = 0u; index < snapshot.EntryCount; index++)
                    values[index] = Map(entries + index);
                return new(snapshot.ObservedMonotonicNanoseconds, snapshot.SnapshotSequence, values);
            }
            finally { NativeMemory.Free(entries); }
        }
        throw new DatabentoFeedException(DatabentoFeedStatus.BufferTooSmall,
            "The native feed registry changed repeatedly while acquiring a watchdog snapshot.");
    }

    static unsafe NativeWatchdogSnapshot Header() => new()
    {
        StructSize = (uint)sizeof(NativeWatchdogSnapshot),
        AbiVersion = NativeConstants.AbiVersion
    };

    static unsafe DatabentoNativeFeedWatchdogStatus Map(NativeWatchdogFeedStatus* value)
        => new(value->FeedInstanceId, value->GenerationId, value->FeedKind, value->MajorStatus,
            value->State, value->TerminalStatus, value->ProducerAlive != 0, value->ConsumerReady != 0,
            value->ExpectedSubscriptions, value->ReceivedSubscriptions, value->HeartbeatCount,
            value->ProviderMessageCount, value->LastHeartbeatMonotonicNanoseconds,
            value->LastProviderMessageMonotonicNanoseconds, value->RecordsProduced,
            value->RecordsConsumed, value->RingCapacityRecords, value->RingUsedRecords,
            value->RingHighWaterRecords, value->RingOverruns,
            Text(value->Dataset, 56), Text(value->FailureDetail, 128));

    static unsafe string Text(byte* source, int capacity)
    {
        var length = 0;
        while (length < capacity && source[length] != 0) length++;
        return Encoding.UTF8.GetString(source, length);
    }
}
