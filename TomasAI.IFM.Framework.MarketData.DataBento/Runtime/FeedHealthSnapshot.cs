using TomasAI.IFM.Shared.MarketData.Diagnostics;
using TomasAI.IFM.Framework.MarketData.DataBento.Interop;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

/// <summary>Initializes a new FeedHealthSnapshot instance.</summary>
/// <param name="State">The store holding the option-chain session state.</param>
/// <param name="TerminalStatus">The terminal status.</param>
/// <param name="RingCapacityRecords">The ring capacity records.</param>
/// <param name="RingUsedRecords">The ring used records.</param>
/// <param name="RingHighWaterRecords">The ring high water records.</param>
/// <param name="RecordsProduced">The records produced.</param>
/// <param name="RecordsConsumed">The records consumed.</param>
/// <param name="BatchesPublished">The batches published.</param>
/// <param name="ChannelFullCount">The channel full count.</param>
/// <param name="PoolMissCount">The pool miss count.</param>
/// <param name="DrainAllocatedBytes">The drain allocated bytes.</param>
/// <param name="Warning">The warning.</param>
public sealed record FeedHealthSnapshot(
    FeedState State,
    DatabentoFeedStatus TerminalStatus,
    ulong RingCapacityRecords,
    ulong RingUsedRecords,
    ulong RingHighWaterRecords,
    ulong RecordsProduced,
    ulong RecordsConsumed,
    ulong BatchesPublished,
    ulong ChannelFullCount,
    ulong PoolMissCount,
    long DrainAllocatedBytes,
    string? Warning)
{
    public bool TransportReady { get; init; }
    public bool TradingReady { get; init; }
    public int BaselineReadyInstrumentCount { get; init; }
    public int InstrumentCount { get; init; }
    public int ChannelBatchCapacity { get; init; }
    public int ChannelBatchCount { get; init; }
    public int PoolBatchCapacity { get; init; }
    public int PoolFreeBatchCount { get; init; }
    public ulong DrainPassLimitHitCount { get; init; }
    public TimeSpan MaximumChannelFullWait { get; init; }
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
    public FeedDrainDiagnostics? DrainDiagnostics { get; init; }
}
