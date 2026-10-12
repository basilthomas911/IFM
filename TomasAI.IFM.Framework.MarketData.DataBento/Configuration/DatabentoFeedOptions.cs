namespace TomasAI.IFM.Framework.MarketData.DataBento;

public sealed record DatabentoFeedOptions
{
    /// <summary>Initializes a new DatabentoFeedOptions instance.</summary>
    private DatabentoFeedOptions()
    {
    }

    public required FeedDeploymentProfile DeploymentProfile { get; init; }
    public required string Dataset { get; init; }
    public FeedDataSourceMode DataSource { get; init; } = FeedDataSourceMode.Synthetic;
    public int RingMemoryBytes { get; init; } = 128 * 1024 * 64;
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
    /// <summary>
    /// Inclusive UTC start used only by the Databento statistics subscription.
    /// Quote, trade, and MBO subscriptions remain live-only.
    /// </summary>
    public ulong StatisticsReplayStartTimestampNanoseconds { get; init; }
    /// <summary>
    /// Inclusive UTC start used by futures trade subscriptions to reconstruct the
    /// current session volume before live publication begins.
    /// </summary>
    public ulong TradeReplayStartTimestampNanoseconds { get; init; }

    /// <summary>Creates feed options for the specified capacity profile and dataset.</summary>
    /// <param name="profile">The supported feed capacity profile.</param>
    /// <param name="dataset">The Databento dataset identifier.</param>
    /// <returns>The for profile result.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep validation, native ownership and failure ordering in one method for this compatibility pass.")]
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
                CpuAffinity = new FeedCpuAffinityOptions
                {
                    PinFeedThreads = false,
                    RequirePerformanceCore = false
                },
                RingBackpressure = new FeedRingBackpressureOptions
                {
                    RingFullTimeout = TimeSpan.FromSeconds(30)
                },
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
                    RingBackpressure = new FeedRingBackpressureOptions
                    {
                        RingFullTimeout = TimeSpan.FromSeconds(30)
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
