using TomasAI.IFM.Framework.MarketData.DataBento.Interop;

namespace TomasAI.IFM.Framework.MarketData.DataBento.UnitTests;

public sealed class Phase6QualificationTests
{
    [Fact]
    public void QualificationGate_EnforcesLatencyIntegrityAndRegressionLimits()
    {
        var observation = new FeedQualificationObservation
        {
            RecordsPerSecond = 5_000_000,
            P50Latency = TimeSpan.FromMicroseconds(30),
            P99Latency = TimeSpan.FromMicroseconds(500),
            P999Latency = TimeSpan.FromMilliseconds(2),
            Duration = TimeSpan.FromMinutes(30)
        };

        var result = DatabentoQualificationGate.Evaluate(
            observation,
            5,
            new FeedQualificationBaseline(5_200_000, TimeSpan.FromMicroseconds(450)),
            TimeSpan.FromMinutes(30));

        Assert.True(result.Passed);
    }

    [Fact]
    public void QualificationGate_FailsMoreThanTenPercentThroughputRegression()
    {
        var observation = new FeedQualificationObservation
        {
            RecordsPerSecond = 5_000_000,
            P50Latency = TimeSpan.Zero,
            P99Latency = TimeSpan.FromMicroseconds(400),
            P999Latency = TimeSpan.FromMilliseconds(1),
            Duration = TimeSpan.FromSeconds(10)
        };

        var result = DatabentoQualificationGate.Evaluate(
            observation,
            5,
            new FeedQualificationBaseline(6_000_000, TimeSpan.FromMicroseconds(400)));

        Assert.False(result.Passed);
        Assert.Contains(result.Failures, x => x.Contains("more than 10%"));
    }

    [Fact]
    public void NativeResolver_UsesOnlyExpectedRidDirectory()
    {
        var path = DatabentoNativeLibraryResolver.GetExpectedPath(
            "C:\\application",
            "win-x64");

        Assert.EndsWith(
            Path.Combine("runtimes", "win-x64", "native", "databento_feed_native.dll"),
            path,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SoakDurations_AreThirtyMinutesAndTwentyFourHours()
    {
        Assert.Equal(TimeSpan.FromMinutes(30),
            DatabentoQualificationGate.GetRequiredSoakDuration(false));
        Assert.Equal(TimeSpan.FromHours(24),
            DatabentoQualificationGate.GetRequiredSoakDuration(true));
    }

    [Fact]
    public void SyntheticProbe_CollectsThroughputLatencyAndIntegrityEvidence()
    {
        var options = DatabentoFeedOptions.ForProfile(
            FeedDeploymentProfile.SyntheticCi,
            "SYNTHETIC") with
        {
            Synthetic = new SyntheticFeedOptions { RecordCount = 10_000 }
        };

        var observation = DatabentoSyntheticQualificationProbe.Run(
            options,
            TimeSpan.FromSeconds(5));

        Assert.True(observation.RecordsPerSecond > 0);
        Assert.True(observation.Duration > TimeSpan.Zero);
        Assert.Equal(0, observation.LostRecords);
        Assert.Equal(0, observation.OutOfOrderRecords);
        Assert.Equal(0, observation.AllocatedBytesAfterWarmup);
    }
}
