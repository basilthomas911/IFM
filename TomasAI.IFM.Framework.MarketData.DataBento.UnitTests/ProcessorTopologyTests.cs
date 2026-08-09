namespace TomasAI.IFM.Framework.MarketData.DataBento.UnitTests;

public sealed class ProcessorTopologyTests
{
    [Fact]
    public void VerifiedIntelCoreTypesSelectOnlyPerformanceCores()
    {
        var processors = new[]
        {
            Candidate(0, 0, ProcessorCoreKind.Performance),
            Candidate(1, 1, ProcessorCoreKind.Performance),
            Candidate(2, 2, ProcessorCoreKind.Efficiency),
            Candidate(3, 3, ProcessorCoreKind.Efficiency)
        };

        Assert.True(ProcessorTopology.HasPerformanceCoreClassification(processors));
        Assert.Equal(
            [new LogicalProcessorLocation(0, 0), new LogicalProcessorLocation(0, 1)],
            ProcessorTopology.GetPerformanceCoreLocations(processors).OrderBy(x => x.LogicalProcessorIndex));
    }

    [Fact]
    public void WindowsEfficiencyClassesProvideOsPerformanceFallback()
    {
        var processors = new[]
        {
            Candidate(0, 0, efficiencyClass: 1),
            Candidate(1, 1, efficiencyClass: 1),
            Candidate(2, 2, efficiencyClass: 0)
        };

        Assert.True(ProcessorTopology.HasPerformanceCoreClassification(processors));
        Assert.Equal(
            [new LogicalProcessorLocation(0, 0), new LogicalProcessorLocation(0, 1)],
            ProcessorTopology.GetPerformanceCoreLocations(processors).OrderBy(x => x.LogicalProcessorIndex));
    }

    [Fact]
    public void HomogeneousTopologyFallsBackToOrdinaryAffinity()
    {
        var processors = new[]
        {
            Candidate(0, 0),
            Candidate(1, 1)
        };

        Assert.False(ProcessorTopology.HasPerformanceCoreClassification(processors));

        var resolution = ProcessorTopology.ResolvePairWithMetadata(
            processors,
            preferPerformanceCore: true);

        Assert.False(resolution.PerformanceCoreClassificationAvailable);
        Assert.False(resolution.PerformanceCoresSelected);
        Assert.NotEqual(resolution.NativeProducer, resolution.ManagedDrain);
    }

    [Fact]
    public void HomogeneousTopologyCanDisableAffinityFallback()
    {
        var processors = new[]
        {
            Candidate(0, 0),
            Candidate(1, 1)
        };

        Assert.Throws<InvalidOperationException>(() =>
            ProcessorTopology.ResolvePairWithMetadata(
                processors,
                preferPerformanceCore: true,
                allowAffinityFallback: false));
    }

    [Fact]
    public void HybridTopologyResolutionDoesNotAdmitKnownEfficiencyCores()
    {
        var processors = new[]
        {
            Candidate(0, 0, ProcessorCoreKind.Performance),
            Candidate(1, 1, ProcessorCoreKind.Performance),
            Candidate(2, 2, ProcessorCoreKind.Efficiency),
            Candidate(3, 3, ProcessorCoreKind.Efficiency)
        };

        var resolution = ProcessorTopology.ResolvePairWithMetadata(
            processors,
            preferPerformanceCore: true);

        Assert.True(resolution.PerformanceCoreClassificationAvailable);
        Assert.True(resolution.PerformanceCoresSelected);
        Assert.Equal(new LogicalProcessorLocation(0, 0), resolution.NativeProducer);
        Assert.Equal(new LogicalProcessorLocation(0, 1), resolution.ManagedDrain);
    }

    [Fact]
    public void LinuxCpuListParserSupportsRangesAndIndividualProcessors()
    {
        Assert.Equal([0, 1, 2, 4, 7, 8], ProcessorTopology.ParseLinuxCpuList("0-2,4,7-8\n"));
    }

    [Fact]
    public void ManagedAffinityCanBeAppliedAndReadBackOnSupportedHosts()
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
        {
            return;
        }
        var processor = ProcessorTopology.EnumerateCandidates()[0].Location;
        LogicalProcessorLocation? observed = null;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                observed = OperatingSystem.IsWindows()
                    ? WindowsThreadAffinity.Apply(processor)
                    : LinuxThreadConfiguration.ApplyAffinity(processor);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });

        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(5)));
        Assert.Null(failure);
        Assert.Equal(processor, observed);
    }

    private static ProcessorCandidate Candidate(
        ushort logicalProcessor,
        int core,
        ProcessorCoreKind coreKind = ProcessorCoreKind.Unknown,
        byte efficiencyClass = 0) =>
        new(
            new LogicalProcessorLocation(0, logicalProcessor),
            logicalProcessor,
            core,
            0,
            efficiencyClass,
            coreKind);
}
