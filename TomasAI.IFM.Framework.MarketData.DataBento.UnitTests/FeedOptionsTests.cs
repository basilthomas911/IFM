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
        Assert.Equal(1 << 20, options.RingMemoryBytes);
        Assert.Equal(8_192, options.ManagedChannelRecordCapacity);
        Assert.Equal(512, options.ManagedBatchRecordCapacity);
        Assert.Equal(512, options.Drain.NativeReadRecordCapacity);
        Assert.Equal(8_192, options.Drain.MaxRecordsPerDrainPass);
        Assert.Equal(CpuAffinityMode.Unpinned, options.CpuAffinity.Mode);
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
}
