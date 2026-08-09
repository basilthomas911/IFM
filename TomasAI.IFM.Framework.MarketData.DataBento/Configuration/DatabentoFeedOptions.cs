namespace TomasAI.IFM.Framework.MarketData.DataBento;

public enum FeedDeploymentProfile : byte
{
    Development = 1,
    PaperTrading = 2,
    Production = 3,
    SyntheticCi = 4
}

public enum FeedDataSourceMode : byte
{
    Synthetic = 1,
    DatabentoLive = 2
}

public enum CpuAffinityMode : byte
{
    AutoPerformanceCores = 1,
    Explicit = 2,
    Unpinned = 3
}

public enum FeedProcessorSelectionKind : byte
{
    Unpinned = 0,
    PerformanceCore = 1,
    AffinityFallback = 2
}

public enum FeedThreadPriority : byte
{
    Normal = 0,
    AboveNormal = 1,
    Highest = 2
}

public enum NumaLocalityMode : byte
{
    Auto = 1,
    ExplicitNode = 2,
    Disabled = 3
}

public enum FeedCoreIsolationMode : byte
{
    PinnedOnly = 1,
    ExcludeFromProcessWorkers = 2
}

public readonly record struct LogicalProcessorLocation(
    ushort ProcessorGroup,
    ushort LogicalProcessorIndex);

public sealed record FeedCpuAffinityOptions
{
    public bool PinFeedThreads { get; init; } = true;
    public CpuAffinityMode Mode { get; init; } = CpuAffinityMode.AutoPerformanceCores;
    public LogicalProcessorLocation? NativeProducer { get; init; }
    public LogicalProcessorLocation? ManagedDrain { get; init; }
    public bool RequirePerformanceCore { get; init; } = true;
    public bool AllowAffinityFallback { get; init; } = true;
}

public sealed record FeedThreadPriorityOptions
{
    public FeedThreadPriority NativeProducer { get; init; } = FeedThreadPriority.AboveNormal;
    public FeedThreadPriority ManagedDrain { get; init; } = FeedThreadPriority.Highest;
    public bool RequireConfiguredPriority { get; init; }
}

public sealed record FeedRingBackpressureOptions
{
    public int SpinIterations { get; init; } = 256;
    public TimeSpan RingFullTimeout { get; init; } = TimeSpan.FromMilliseconds(2);
}

public sealed record FeedMemoryOptions
{
    public bool LockRingMemory { get; init; } = true;
    public bool RequireLockedMemory { get; init; }
    public bool RequireBasePagePolicy { get; init; }
}

public sealed record FeedDrainOptions
{
    public int NativeReadRecordCapacity { get; init; } = 512;
    public int MaxRecordsPerDrainPass { get; init; } = 8_192;
}

public sealed record FeedGcOptions
{
    public bool EnableSustainedLowLatency { get; init; } = true;
    public bool RequireGcConfiguration { get; init; }
}

public sealed record FeedNumaOptions
{
    public NumaLocalityMode Mode { get; init; } = NumaLocalityMode.Auto;
    public ushort? Node { get; init; }
    public bool RequireNumaLocality { get; init; }
}

public sealed record FeedCoreIsolationOptions
{
    public FeedCoreIsolationMode Mode { get; init; } =
        FeedCoreIsolationMode.ExcludeFromProcessWorkers;
    public bool RequireCoreIsolation { get; init; }
}

public sealed record FeedProcessorResidencyOptions
{
    public bool EnableTracking { get; init; }
    public int ForcedMigrationIntervalRecords { get; init; }
}

public sealed record FeedTransportHealthOptions
{
    public TimeSpan HeartbeatInterval { get; init; } = TimeSpan.FromSeconds(5);
    public TimeSpan HungConnectionTimeout => HeartbeatInterval + TimeSpan.FromSeconds(5);
    public TimeSpan HealthPollInterval { get; init; } = TimeSpan.FromSeconds(1);
    public TimeSpan MetricsExportInterval { get; init; } = TimeSpan.FromSeconds(5);
}

public sealed record SyntheticFeedOptions
{
    public int RecordCount { get; init; } = 10_000;
    public int RecordsPerSecond { get; init; }
    public ulong StartSequence { get; init; } = 1;
}

public sealed record DatabentoFeedOptions
{
    private DatabentoFeedOptions()
    {
    }

    public required FeedDeploymentProfile DeploymentProfile { get; init; }
    public required string Dataset { get; init; }
    public FeedDataSourceMode DataSource { get; init; } = FeedDataSourceMode.Synthetic;
    public int RingMemoryBytes { get; init; } = 1 << 20;
    public int ManagedChannelRecordCapacity { get; init; } = 8_192;
    public int ManagedBatchRecordCapacity { get; init; } = 512;
    public FeedCpuAffinityOptions CpuAffinity { get; init; } = new();
    public FeedThreadPriorityOptions ThreadPriority { get; init; } = new();
    public FeedRingBackpressureOptions RingBackpressure { get; init; } = new();
    public FeedMemoryOptions Memory { get; init; } = new();
    public FeedDrainOptions Drain { get; init; } = new();
    public FeedGcOptions GarbageCollection { get; init; } = new();
    public FeedNumaOptions Numa { get; init; } = new();
    public FeedCoreIsolationOptions CoreIsolation { get; init; } = new();
    public FeedProcessorResidencyOptions ProcessorResidency { get; init; } = new();
    public FeedTransportHealthOptions TransportHealth { get; init; } = new();
    public SyntheticFeedOptions Synthetic { get; init; } = new();

    public static DatabentoFeedOptions ForProfile(
        FeedDeploymentProfile profile,
        string dataset)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataset);
        return profile switch
        {
            FeedDeploymentProfile.Development => new DatabentoFeedOptions
            {
                DeploymentProfile = profile,
                Dataset = dataset,
                DataSource = FeedDataSourceMode.Synthetic,
                CoreIsolation = new FeedCoreIsolationOptions
                {
                    Mode = FeedCoreIsolationMode.PinnedOnly
                }
            },
            FeedDeploymentProfile.PaperTrading or FeedDeploymentProfile.Production =>
                new DatabentoFeedOptions
                {
                    DeploymentProfile = profile,
                    Dataset = dataset,
                    DataSource = FeedDataSourceMode.DatabentoLive,
                    CpuAffinity = new FeedCpuAffinityOptions
                    {
                        RequirePerformanceCore = true
                    },
                    ThreadPriority = new FeedThreadPriorityOptions
                    {
                        RequireConfiguredPriority = true
                    },
                    Memory = new FeedMemoryOptions
                    {
                        LockRingMemory = true,
                        RequireLockedMemory = true,
                        RequireBasePagePolicy = true
                    },
                    GarbageCollection = new FeedGcOptions
                    {
                        RequireGcConfiguration = true
                    },
                    Numa = new FeedNumaOptions
                    {
                        RequireNumaLocality = true
                    },
                    CoreIsolation = new FeedCoreIsolationOptions
                    {
                        Mode = FeedCoreIsolationMode.ExcludeFromProcessWorkers,
                        RequireCoreIsolation = true
                    }
                },
            FeedDeploymentProfile.SyntheticCi => new DatabentoFeedOptions
            {
                DeploymentProfile = profile,
                Dataset = dataset,
                DataSource = FeedDataSourceMode.Synthetic,
                CpuAffinity = new FeedCpuAffinityOptions
                {
                    PinFeedThreads = false,
                    RequirePerformanceCore = false
                },
                ThreadPriority = new FeedThreadPriorityOptions(),
                Memory = new FeedMemoryOptions
                {
                    LockRingMemory = false
                },
                GarbageCollection = new FeedGcOptions
                {
                    EnableSustainedLowLatency = false
                },
                Numa = new FeedNumaOptions
                {
                    Mode = NumaLocalityMode.Disabled
                },
                CoreIsolation = new FeedCoreIsolationOptions
                {
                    Mode = FeedCoreIsolationMode.PinnedOnly
                }
            },
            _ => throw new ArgumentOutOfRangeException(nameof(profile))
        };
    }
}
