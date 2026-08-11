using TomasAI.IFM.Framework.MarketData.DataBento;

if (args.Contains("--last-price", StringComparer.OrdinalIgnoreCase))
{
    return LastPriceBenchmark.Run(args);
}
if (args.Contains("--api-price", StringComparer.OrdinalIgnoreCase))
{
    return ApplicationPriceBenchmark.Run(args);
}

var pinned = args.Contains("--pinned", StringComparer.OrdinalIgnoreCase);
var forcedMigration = args.Contains("--forced-migration", StringComparer.OrdinalIgnoreCase);
var useAffinity = pinned || forcedMigration;
var recordCount = ReadRecordCount(args);
var migrationInterval = forcedMigration ? ReadMigrationInterval(args) : 0;
if (forcedMigration && migrationInterval >= recordCount)
{
    throw new ArgumentOutOfRangeException(
        nameof(migrationInterval),
        "The migration interval must be smaller than the record count.");
}
var options = DatabentoFeedOptions.ForProfile(
    FeedDeploymentProfile.SyntheticCi,
    "SYNTHETIC") with
{
    RingMemoryBytes = 64 << 20,
    ManagedChannelRecordCapacity = 1_048_576,
    CpuAffinity = new FeedCpuAffinityOptions
    {
        PinFeedThreads = useAffinity,
        Mode = CpuAffinityMode.AutoPerformanceCores,
        RequirePerformanceCore = true
    },
    ThreadPriority = new FeedThreadPriorityOptions
    {
        NativeProducer = FeedThreadPriority.Normal,
        ManagedDrain = FeedThreadPriority.Normal
    },
    CoreIsolation = new FeedCoreIsolationOptions
    {
        Mode = FeedCoreIsolationMode.PinnedOnly
    },
    Numa = new FeedNumaOptions
    {
        Mode = NumaLocalityMode.Disabled
    },
    ProcessorResidency = new FeedProcessorResidencyOptions
    {
        EnableTracking = true,
        ForcedMigrationIntervalRecords = migrationInterval
    },
    RingBackpressure = new FeedRingBackpressureOptions
    {
        RingFullTimeout = TimeSpan.FromSeconds(5)
    },
    Synthetic = new SyntheticFeedOptions
    {
        RecordCount = recordCount
    }
};

var observation = DatabentoSyntheticQualificationProbe.Run(
    options,
    TimeSpan.FromSeconds(30));

Console.WriteLine($"Mode: {ModeName(pinned, forcedMigration)}");
Console.WriteLine($"Records: {recordCount:N0}");
if (forcedMigration)
{
    Console.WriteLine($"Forced migration interval: {migrationInterval:N0} records");
}
Console.WriteLine($"Throughput: {observation.RecordsPerSecond:N0} records/second");
Console.WriteLine($"Duration: {observation.Duration.TotalMilliseconds:N2} ms");
Console.WriteLine($"Processor selection: {observation.ProcessorSelection}");
Console.WriteLine(
    $"Native producer: requested={Format(observation.ResolvedNativeProducer)}, "
    + $"alternate={Format(observation.AlternateNativeProducer)}, "
    + $"observed={Format(observation.ObservedNativeProducer)}, "
    + $"verified={observation.NativeProducerAffinityVerified}");
Console.WriteLine(
    $"Managed drain: requested={Format(observation.ResolvedManagedDrain)}, "
    + $"alternate={Format(observation.AlternateManagedDrain)}, "
    + $"observed={Format(observation.ObservedManagedDrain)}, "
    + $"verified={observation.ManagedDrainAffinityVerified}");
Console.WriteLine(
    $"Native residency: samples={observation.NativeProducerProcessorSamples:N0}, "
    + $"unique={observation.NativeProducerUniqueProcessors}, "
    + $"migrations={observation.NativeProducerProcessorMigrations:N0}, "
    + $"off-assignment={observation.NativeProducerOffAssignmentSamples:N0}");
Console.WriteLine(
    $"Managed residency: samples={observation.ManagedDrainProcessorSamples:N0}, "
    + $"unique={observation.ManagedDrainUniqueProcessors}, "
    + $"migrations={observation.ManagedDrainProcessorMigrations:N0}, "
    + $"off-assignment={observation.ManagedDrainOffAssignmentSamples:N0}");

if (pinned
    && (!observation.NativeProducerAffinityVerified
        || !observation.ManagedDrainAffinityVerified
        || observation.ResolvedNativeProducer != observation.ObservedNativeProducer
        || observation.ResolvedManagedDrain != observation.ObservedManagedDrain
        || observation.NativeProducerProcessorSamples != (ulong)recordCount
        || observation.ManagedDrainProcessorSamples != (ulong)recordCount
        || observation.NativeProducerUniqueProcessors != 1
        || observation.ManagedDrainUniqueProcessors != 1
        || observation.NativeProducerProcessorMigrations != 0
        || observation.ManagedDrainProcessorMigrations != 0
        || observation.NativeProducerOffAssignmentSamples != 0
        || observation.ManagedDrainOffAssignmentSamples != 0))
{
    Console.Error.WriteLine("Affinity verification failed.");
    return 2;
}
if (forcedMigration)
{
    var expectedMigrations = (ulong)((recordCount - 1) / migrationInterval);
    if (observation.NativeProducerProcessorSamples != (ulong)recordCount
        || observation.ManagedDrainProcessorSamples != (ulong)recordCount
        || observation.NativeProducerUniqueProcessors != 2
        || observation.ManagedDrainUniqueProcessors != 2
        || observation.NativeProducerProcessorMigrations != expectedMigrations
        || observation.ManagedDrainProcessorMigrations != expectedMigrations)
    {
        Console.Error.WriteLine(
            $"Forced migration verification failed; expected {expectedMigrations:N0} migrations.");
        return 3;
    }
}
return 0;

static int ReadRecordCount(string[] arguments)
{
    var value = arguments.FirstOrDefault(static argument =>
        argument.StartsWith("--records=", StringComparison.OrdinalIgnoreCase));
    return value is null
        ? 5_000_000
        : int.Parse(value.AsSpan(value.IndexOf('=') + 1));
}

static int ReadMigrationInterval(string[] arguments)
{
    var value = arguments.FirstOrDefault(static argument =>
        argument.StartsWith("--migration-interval=", StringComparison.OrdinalIgnoreCase));
    return value is null
        ? Random.Shared.Next(10_000, 100_001)
        : int.Parse(value.AsSpan(value.IndexOf('=') + 1));
}

static string ModeName(bool pinned, bool forcedMigration) => forcedMigration
    ? "Forced migration"
    : pinned ? "Pinned" : "Naturally unpinned";

static string Format(LogicalProcessorLocation? location) => location is { } value
    ? $"group {value.ProcessorGroup}, logical processor {value.LogicalProcessorIndex}"
    : "none";
