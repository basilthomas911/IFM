namespace TomasAI.IFM.Framework.MarketData.DataBento.UnitTests;

public sealed class FeedOptionsTests
{
    [Fact]
    public void SyntheticCiProfileResolvesPhaseTwoDefaults()
    {
        var options = DatabentoFeedOptions.ForProfile(
            FeedDeploymentProfile.SyntheticCi,
            "SYNTHETIC");

        Assert.Equal(FeedDataSourceMode.Synthetic, options.DataSource);
        Assert.Equal(128 * 1024 * 64, options.RingMemoryBytes);
        Assert.Equal(8_192, options.ManagedChannelRecordCapacity);
        Assert.Equal(512, options.ManagedBatchRecordCapacity);
        Assert.Equal(512, options.Drain.NativeReadRecordCapacity);
        Assert.Equal(8_192, options.Drain.MaxRecordsPerDrainPass);
        Assert.False(options.CpuAffinity.PinFeedThreads);
        Assert.Equal(CpuAffinityMode.AutoPerformanceCores, options.CpuAffinity.Mode);
        Assert.False(options.Memory.LockRingMemory);
        Assert.Equal(NumaLocalityMode.Disabled, options.Numa.Mode);
    }

    [Fact]
    public void ProductionProfileAcceptsLivePhaseThreeConfiguration()
    {
        var options = DatabentoFeedOptions.ForProfile(
            FeedDeploymentProfile.Production,
            "GLBX.MDP3");

        using var feed = new DatabentoFeedFactory().CreateTickerFeed(options);
        Assert.NotNull(feed);
        Assert.True(options.CpuAffinity.PinFeedThreads);
        Assert.True(options.CpuAffinity.RequirePerformanceCore);
        Assert.True(options.CpuAffinity.AllowAffinityFallback);
        Assert.Equal(TimeSpan.FromSeconds(30), options.RingBackpressure.RingFullTimeout);
    }

    [Fact]
    public void DevelopmentProfileAllowsReplayBurstsToDrainWithoutImmediateDisconnect()
    {
        var options = DatabentoFeedOptions.ForProfile(
            FeedDeploymentProfile.Development,
            "GLBX.MDP3");

        Assert.Equal(TimeSpan.FromSeconds(30), options.RingBackpressure.RingFullTimeout);
    }

    [Fact]
    public void PinFeedThreadsDefaultsToTrue()
    {
        Assert.True(new FeedCpuAffinityOptions().PinFeedThreads);
    }

    [Fact]
    public void DisabledPinningOverridesPlacementAndStrictIsolation()
    {
        using var placement = ProcessCoreIsolationCoordinator.Acquire(
            new FeedCpuAffinityOptions
            {
                PinFeedThreads = false
            },
            new FeedCoreIsolationOptions
            {
                Mode = FeedCoreIsolationMode.ExcludeFromProcessWorkers,
                RequireCoreIsolation = true
            },
            new FeedNumaOptions(),
            new FeedProcessorResidencyOptions());

        Assert.Null(placement.NativeProducer);
        Assert.Null(placement.ManagedDrain);
        Assert.Equal(FeedProcessorSelectionKind.Unpinned, placement.SelectionKind);
    }

    [Fact]
    public void SyntheticCiRejectsLiveData()
    {
        var options = DatabentoFeedOptions.ForProfile(
            FeedDeploymentProfile.SyntheticCi,
            "GLBX.MDP3") with
        {
            DataSource = FeedDataSourceMode.DatabentoLive
        };

        Assert.Throws<InvalidOperationException>(() =>
            new DatabentoFeedFactory().CreateTickerFeed(options));
    }

    [Fact]
    public void InvalidManagedCapacityFailsBeforeNativeCreation()
    {
        var options = DatabentoFeedOptions.ForProfile(
            FeedDeploymentProfile.SyntheticCi,
            "SYNTHETIC") with
        {
            ManagedChannelRecordCapacity = 1_000,
            ManagedBatchRecordCapacity = 512
        };

        Assert.Throws<ArgumentException>(() =>
            new DatabentoFeedFactory().CreateTickerFeed(options));
    }

    [Fact]
    public void NonPowerOfTwoNativeRingFailsBeforeNativeCreation()
    {
        var options = DatabentoFeedOptions.ForProfile(
            FeedDeploymentProfile.SyntheticCi,
            "SYNTHETIC") with
        {
            RingMemoryBytes = 3 * 64
        };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DatabentoFeedFactory().CreateTickerFeed(options));
    }

    [Fact]
    public void ForcedMigrationRequiresResidencyTracking()
    {
        var options = DatabentoFeedOptions.ForProfile(
            FeedDeploymentProfile.SyntheticCi,
            "SYNTHETIC") with
        {
            CpuAffinity = new FeedCpuAffinityOptions
            {
                Mode = CpuAffinityMode.AutoPerformanceCores,
                RequirePerformanceCore = false
            },
            ProcessorResidency = new FeedProcessorResidencyOptions
            {
                ForcedMigrationIntervalRecords = 100
            }
        };

        Assert.Throws<ArgumentException>(() =>
            new DatabentoFeedFactory().CreateTickerFeed(options));
    }

    [Fact]
    public void TrackedSyntheticFeedAcceptsForcedMigrationBenchmarkConfiguration()
    {
        var options = DatabentoFeedOptions.ForProfile(
            FeedDeploymentProfile.SyntheticCi,
            "SYNTHETIC") with
        {
            CpuAffinity = new FeedCpuAffinityOptions
            {
                Mode = CpuAffinityMode.AutoPerformanceCores,
                RequirePerformanceCore = false
            },
            ProcessorResidency = new FeedProcessorResidencyOptions
            {
                EnableTracking = true,
                ForcedMigrationIntervalRecords = 100
            }
        };

        using var feed = new DatabentoFeedFactory().CreateTickerFeed(options);
        Assert.NotNull(feed);
    }
}
