using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Text;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

internal sealed class FeedPlacementLease : IDisposable
{
    private readonly bool _releasable;
    private readonly bool _restoreWorkerSet;
    private readonly IReadOnlyList<LogicalProcessorLocation> _reservations;
    private int _committed;
    private int _disposed;

    /// <summary>Initializes a new FeedPlacementLease instance.</summary>
    /// <param name="nativeProducer">The native producer.</param>
    /// <param name="managedDrain">The managed drain.</param>
    /// <param name="nativeProducerAlternate">The native producer alternate.</param>
    /// <param name="managedDrainAlternate">The managed drain alternate.</param>
    /// <param name="numaNode">The numa node.</param>
    /// <param name="selectionKind">The selection kind.</param>
    /// <param name="releasable">The releasable.</param>
    /// <param name="restoreWorkerSet">The restore worker set.</param>
    /// <param name="reservations">The requested quote buffer capacities and number of reserved slots per capacity.</param>
    internal FeedPlacementLease(
        LogicalProcessorLocation? nativeProducer,
        LogicalProcessorLocation? managedDrain,
        LogicalProcessorLocation? nativeProducerAlternate = null,
        LogicalProcessorLocation? managedDrainAlternate = null,
        ushort? numaNode = null,
        FeedProcessorSelectionKind selectionKind = FeedProcessorSelectionKind.Unpinned,
        bool releasable = false,
        bool restoreWorkerSet = false,
        IReadOnlyList<LogicalProcessorLocation>? reservations = null)
    {
        NativeProducer = nativeProducer;
        ManagedDrain = managedDrain;
        NativeProducerAlternate = nativeProducerAlternate;
        ManagedDrainAlternate = managedDrainAlternate;
        NumaNode = numaNode;
        SelectionKind = selectionKind;
        _releasable = releasable;
        _restoreWorkerSet = restoreWorkerSet;
        _reservations = reservations ?? Array.Empty<LogicalProcessorLocation>();
    }

    internal LogicalProcessorLocation? NativeProducer { get; }
    internal LogicalProcessorLocation? ManagedDrain { get; }
    internal LogicalProcessorLocation? NativeProducerAlternate { get; }
    internal LogicalProcessorLocation? ManagedDrainAlternate { get; }
    internal ushort? NumaNode { get; }
    internal FeedProcessorSelectionKind SelectionKind { get; }

    internal void Commit() => Volatile.Write(ref _committed, 1);

    /// <summary>Releases the resources owned by this instance.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0
            || !_releasable
            || Volatile.Read(ref _committed) != 0
            || _reservations.Count == 0)
        {
            return;
        }
        ProcessCoreIsolationCoordinator.ReleaseUncommitted(
            _reservations,
            _restoreWorkerSet);
    }
}
