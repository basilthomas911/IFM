using System.Runtime.InteropServices;
using System.Text;
using TomasAI.IFM.Framework.MarketData.DataBento.Interop;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

/// <summary>Initializes a new DatabentoNativeWatchdogSnapshot instance.</summary>
/// <param name="ObservedMonotonicNanoseconds">The observed monotonic nanoseconds.</param>
/// <param name="SnapshotSequence">The snapshot sequence.</param>
/// <param name="Feeds">The factory used to create option-chain feeds.</param>
public sealed record DatabentoNativeWatchdogSnapshot(
    ulong ObservedMonotonicNanoseconds, ulong SnapshotSequence,
    IReadOnlyList<DatabentoNativeFeedWatchdogStatus> Feeds);
