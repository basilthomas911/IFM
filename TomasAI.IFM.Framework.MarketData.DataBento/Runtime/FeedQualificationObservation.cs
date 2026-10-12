using System.Diagnostics;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

public sealed record FeedQualificationObservation
{
    public required double RecordsPerSecond { get; init; }
    public required TimeSpan P50Latency { get; init; }
    public required TimeSpan P99Latency { get; init; }
    public required TimeSpan P999Latency { get; init; }
    public long AllocatedBytesAfterWarmup { get; init; }
    public long LostRecords { get; init; }
    public long OutOfOrderRecords { get; init; }
    public TimeSpan Duration { get; init; }
    public long HandleCountDelta { get; init; }
    public FeedProcessorSelectionKind ProcessorSelection { get; init; }
    public LogicalProcessorLocation? ResolvedNativeProducer { get; init; }
    public LogicalProcessorLocation? AlternateNativeProducer { get; init; }
    public LogicalProcessorLocation? ObservedNativeProducer { get; init; }
    public LogicalProcessorLocation? ResolvedManagedDrain { get; init; }
    public LogicalProcessorLocation? AlternateManagedDrain { get; init; }
    public LogicalProcessorLocation? ObservedManagedDrain { get; init; }
    public bool NativeProducerAffinityVerified { get; init; }
    public bool ManagedDrainAffinityVerified { get; init; }
    public ulong NativeProducerProcessorSamples { get; init; }
    public ulong NativeProducerProcessorMigrations { get; init; }
    public uint NativeProducerUniqueProcessors { get; init; }
    public ulong NativeProducerOffAssignmentSamples { get; init; }
    public ulong ManagedDrainProcessorSamples { get; init; }
    public ulong ManagedDrainProcessorMigrations { get; init; }
    public uint ManagedDrainUniqueProcessors { get; init; }
    public ulong ManagedDrainOffAssignmentSamples { get; init; }
}
