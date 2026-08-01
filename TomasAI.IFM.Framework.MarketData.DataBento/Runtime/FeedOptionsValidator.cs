using System.Numerics;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

internal static class FeedOptionsValidator
{
    internal static DatabentoFeedOptions ValidateAndSnapshot(DatabentoFeedOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Dataset);
        if (!Enum.IsDefined(options.DeploymentProfile)
            || !Enum.IsDefined(options.DataSource)
            || !Enum.IsDefined(options.CpuAffinity.Mode)
            || !Enum.IsDefined(options.ThreadPriority.NativeProducer)
            || !Enum.IsDefined(options.ThreadPriority.ManagedDrain)
            || !Enum.IsDefined(options.Numa.Mode)
            || !Enum.IsDefined(options.CoreIsolation.Mode))
        {
            throw new ArgumentException("A feed configuration enum value is invalid.");
        }
        if (options.DataSource != FeedDataSourceMode.Synthetic)
        {
            throw new NotSupportedException(
                "The Databento live adapter is intentionally deferred until Phase 3 and a licence is available.");
        }
        if (options.DeploymentProfile is FeedDeploymentProfile.PaperTrading
            or FeedDeploymentProfile.Production)
        {
            throw new InvalidOperationException(
                "Paper-trading and production profiles require the Phase 3 live adapter.");
        }
        if (options.RingMemoryBytes < 128
            || options.RingMemoryBytes % 64 != 0
            || !BitOperations.IsPow2(options.RingMemoryBytes / 64))
        {
            throw new ArgumentOutOfRangeException(
                nameof(options.RingMemoryBytes),
                "The native ring must contain a power-of-two number of 64-byte records.");
        }
        if (options.ManagedChannelRecordCapacity <= 0
            || options.ManagedBatchRecordCapacity <= 0
            || options.ManagedBatchRecordCapacity > options.ManagedChannelRecordCapacity
            || options.ManagedChannelRecordCapacity % options.ManagedBatchRecordCapacity != 0)
        {
            throw new ArgumentException(
                "Managed channel capacity must be a positive integral number of managed batches.");
        }
        if (options.Drain.NativeReadRecordCapacity <= 0
            || options.Drain.MaxRecordsPerDrainPass < options.Drain.NativeReadRecordCapacity
            || options.Drain.MaxRecordsPerDrainPass % options.Drain.NativeReadRecordCapacity != 0
            || options.Drain.NativeReadRecordCapacity > options.RingMemoryBytes / 64)
        {
            throw new ArgumentException(
                "The drain pass must be a positive integral number of native reads.");
        }
        if (options.RingBackpressure.SpinIterations < 0
            || options.RingBackpressure.RingFullTimeout <= TimeSpan.Zero
            || options.RingBackpressure.RingFullTimeout.TotalMicroseconds > uint.MaxValue)
        {
            throw new ArgumentException("Native ring backpressure values must be positive.");
        }
        if (options.TransportHealth.HeartbeatInterval < TimeSpan.FromSeconds(5)
            || options.TransportHealth.HeartbeatInterval.TotalMilliseconds > uint.MaxValue
            || options.TransportHealth.HealthPollInterval <= TimeSpan.Zero
            || options.TransportHealth.MetricsExportInterval <= TimeSpan.Zero
            || options.TransportHealth.MetricsExportInterval.Ticks
               % options.TransportHealth.HealthPollInterval.Ticks != 0)
        {
            throw new ArgumentException("Transport health intervals are invalid.");
        }
        if (options.Numa.Mode == NumaLocalityMode.ExplicitNode && options.Numa.Node is null
            || options.Numa.Mode != NumaLocalityMode.ExplicitNode && options.Numa.Node is not null)
        {
            throw new ArgumentException("Explicit NUMA mode and node must be configured together.");
        }
        if (options.CpuAffinity.Mode == CpuAffinityMode.Explicit
            && (options.CpuAffinity.NativeProducer is null
                || options.CpuAffinity.ManagedDrain is null))
        {
            throw new ArgumentException("Explicit affinity requires producer and drain locations.");
        }
        if (options.CpuAffinity.Mode != CpuAffinityMode.Explicit
            && (options.CpuAffinity.NativeProducer is not null
                || options.CpuAffinity.ManagedDrain is not null))
        {
            throw new ArgumentException(
                "Explicit processor locations require explicit affinity mode.");
        }
        if (options.CpuAffinity.Mode == CpuAffinityMode.Explicit
            && options.CpuAffinity.NativeProducer == options.CpuAffinity.ManagedDrain)
        {
            throw new ArgumentException("Producer and drain processors must be distinct.");
        }
        if (options.CpuAffinity.Mode == CpuAffinityMode.Unpinned
            && (options.CpuAffinity.RequirePerformanceCore
                || options.CoreIsolation.RequireCoreIsolation))
        {
            throw new ArgumentException(
                "Unpinned mode cannot require performance-core placement or core isolation.");
        }
        if (options.Numa.Mode == NumaLocalityMode.Disabled
            && options.Numa.RequireNumaLocality)
        {
            throw new ArgumentException("Disabled NUMA placement cannot require locality.");
        }
        if (options.Memory.RequireLockedMemory && !options.Memory.LockRingMemory)
        {
            throw new ArgumentException("Required memory locking cannot be disabled.");
        }
        if (options.CoreIsolation.RequireCoreIsolation
            && options.CoreIsolation.Mode != FeedCoreIsolationMode.ExcludeFromProcessWorkers)
        {
            throw new ArgumentException(
                "Strict core isolation requires worker CPU-set exclusion.");
        }
        if (options.Synthetic.RecordCount <= 0
            || options.Synthetic.RecordsPerSecond < 0
            || options.Synthetic.StartSequence == 0
            || options.Synthetic.StartSequence > uint.MaxValue
            || options.Synthetic.StartSequence
               + checked((ulong)options.Synthetic.RecordCount - 1) > uint.MaxValue)
        {
            throw new ArgumentException("Synthetic producer settings are invalid.");
        }

        return options with
        {
            Dataset = options.Dataset,
            CpuAffinity = options.CpuAffinity with { },
            ThreadPriority = options.ThreadPriority with { },
            RingBackpressure = options.RingBackpressure with { },
            Memory = options.Memory with { },
            Drain = options.Drain with { },
            GarbageCollection = options.GarbageCollection with { },
            Numa = options.Numa with { },
            CoreIsolation = options.CoreIsolation with { },
            TransportHealth = options.TransportHealth with { },
            Synthetic = options.Synthetic with { }
        };
    }
}
