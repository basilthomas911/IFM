using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Text;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

internal static partial class ProcessCoreIsolationCoordinator
{
    private static readonly System.Threading.Lock Gate = new();
    private static readonly HashSet<LogicalProcessorLocation> Reservations = [];

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep validation, native ownership and failure ordering in one method for this compatibility pass.")]
    internal static FeedPlacementLease Acquire(
        FeedCpuAffinityOptions affinity,
        FeedCoreIsolationOptions isolation,
        FeedNumaOptions numa,
        FeedProcessorResidencyOptions processorResidency)
    {
        lock (Gate)
        {
            if (!affinity.PinFeedThreads || affinity.Mode == CpuAffinityMode.Unpinned)
            {
                if (affinity.PinFeedThreads && isolation.RequireCoreIsolation)
                {
                    throw new InvalidOperationException(
                        "Strict core isolation cannot be combined with unpinned feed threads.");
                }
                return new FeedPlacementLease(null, null);
            }
            var candidates = ProcessorTopology.EnumerateCandidates();
            var performanceClassificationAvailable =
                ProcessorTopology.HasPerformanceCoreClassification(candidates);
            var performanceLocations = performanceClassificationAvailable
                ? ProcessorTopology.GetPerformanceCoreLocations(candidates)
                : null;
            if (affinity.RequirePerformanceCore
                && !performanceClassificationAvailable
                && !affinity.AllowAffinityFallback)
            {
                throw new InvalidOperationException(
                    "Performance-core classification is unavailable and affinity fallback is disabled.");
            }
            (LogicalProcessorLocation Producer, LogicalProcessorLocation Drain) pair;
            var performanceCoresSelected = false;
            LogicalProcessorLocation? producerAlternate = null;
            LogicalProcessorLocation? drainAlternate = null;
            ushort? resolvedNumaNode;
            if (affinity.Mode == CpuAffinityMode.Explicit)
            {
                pair = (affinity.NativeProducer!.Value, affinity.ManagedDrain!.Value);
                if (pair.Producer == pair.Drain
                    || Reservations.Contains(pair.Producer)
                    || Reservations.Contains(pair.Drain))
                {
                    throw new InvalidOperationException(
                        "Explicit feed processors are duplicate or already reserved.");
                }
                if (!candidates.Any(item => item.Location == pair.Producer)
                    || !candidates.Any(item => item.Location == pair.Drain))
                {
                    throw new InvalidOperationException(
                        "An explicit feed processor is unavailable to this process.");
                }
                var producer = candidates.Single(item => item.Location == pair.Producer);
                var drain = candidates.Single(item => item.Location == pair.Drain);
                if (affinity.RequirePerformanceCore
                    && performanceClassificationAvailable
                    && (!performanceLocations!.Contains(pair.Producer)
                        || !performanceLocations.Contains(pair.Drain)))
                {
                    throw new InvalidOperationException(
                        "Explicit feed processors must be selected from performance cores.");
                }
                performanceCoresSelected = affinity.RequirePerformanceCore
                                           && performanceClassificationAvailable;
                if (producer.NumaNodeIndex != drain.NumaNodeIndex)
                {
                    throw new InvalidOperationException(
                        "Producer and drain processors must reside on the same NUMA node.");
                }
                if (producer.Location.ProcessorGroup == drain.Location.ProcessorGroup
                    && producer.CoreIndex == drain.CoreIndex)
                {
                    throw new InvalidOperationException(
                        "Producer and drain processors must use distinct physical cores.");
                }
                resolvedNumaNode = producer.NumaNodeIndex;
            }
            else
            {
                var resolution = ProcessorTopology.ResolvePairWithMetadata(
                    candidates,
                    affinity.RequirePerformanceCore,
                    Reservations,
                    affinity.AllowAffinityFallback);
                pair = (resolution.NativeProducer, resolution.ManagedDrain);
                performanceCoresSelected = resolution.PerformanceCoresSelected;
                resolvedNumaNode = candidates
                    .Single(item => item.Location == pair.Producer)
                    .NumaNodeIndex;
            }
            if (processorResidency.ForcedMigrationIntervalRecords > 0)
            {
                var excluded = Reservations.ToHashSet();
                var primaryPhysicalCores = candidates
                    .Where(candidate => candidate.Location == pair.Producer
                                        || candidate.Location == pair.Drain)
                    .Select(static candidate => (
                        candidate.Location.ProcessorGroup,
                        candidate.CoreIndex))
                    .ToHashSet();
                foreach (var candidate in candidates.Where(candidate =>
                             primaryPhysicalCores.Contains((
                                 candidate.Location.ProcessorGroup,
                                 candidate.CoreIndex))))
                {
                    excluded.Add(candidate.Location);
                }
                var alternateResolution = ProcessorTopology.ResolvePairWithMetadata(
                    candidates,
                    affinity.RequirePerformanceCore,
                    excluded,
                    affinity.AllowAffinityFallback);
                producerAlternate = alternateResolution.NativeProducer;
                drainAlternate = alternateResolution.ManagedDrain;
                var alternateNumaNode = candidates
                    .Single(item => item.Location == producerAlternate.Value)
                    .NumaNodeIndex;
                if (alternateNumaNode != resolvedNumaNode)
                {
                    throw new InvalidOperationException(
                        "Forced migration processors must use the feed's NUMA node.");
                }
            }
            if (numa.Mode == NumaLocalityMode.ExplicitNode
                && resolvedNumaNode != numa.Node)
            {
                throw new InvalidOperationException(
                    "The selected feed processors do not belong to the explicit NUMA node.");
            }
            var selectedLocations = new[]
                {
                    pair.Producer,
                    pair.Drain,
                    producerAlternate,
                    drainAlternate
                }
                .Where(static location => location is not null)
                .Select(static location => location!.Value)
                .ToArray();
            var selectedPhysicalCores = candidates
                .Where(candidate => selectedLocations.Contains(candidate.Location))
                .Select(static candidate => (
                    candidate.Location.ProcessorGroup,
                    candidate.CoreIndex))
                .ToHashSet();
            var newReservations = isolation.Mode == FeedCoreIsolationMode.ExcludeFromProcessWorkers
                ? candidates
                    .Where(item => selectedPhysicalCores.Contains((
                        item.Location.ProcessorGroup,
                        item.CoreIndex)))
                    .Select(static item => item.Location)
                    .Distinct()
                    .ToArray()
                : selectedLocations;
            if (newReservations.Any(Reservations.Contains))
            {
                throw new InvalidOperationException(
                    "A selected feed physical core is already reserved.");
            }
            foreach (var reservation in newReservations)
            {
                Reservations.Add(reservation);
            }
            try
            {
                if (isolation.Mode == FeedCoreIsolationMode.ExcludeFromProcessWorkers)
                {
                    ApplyProcessWorkerExclusion(isolation.RequireCoreIsolation);
                }
                return new FeedPlacementLease(
                    pair.Producer,
                    pair.Drain,
                    producerAlternate,
                    drainAlternate,
                    numa.Mode == NumaLocalityMode.Disabled ? null : resolvedNumaNode,
                    performanceCoresSelected
                        ? FeedProcessorSelectionKind.PerformanceCore
                        : FeedProcessorSelectionKind.AffinityFallback,
                    releasable: true,
                    restoreWorkerSet:
                        isolation.Mode == FeedCoreIsolationMode.ExcludeFromProcessWorkers,
                    reservations: newReservations);
            }
            catch
            {
                foreach (var reservation in newReservations)
                {
                    Reservations.Remove(reservation);
                }
                throw;
            }
        }
    }

    internal static void ReleaseUncommitted(
        IReadOnlyList<LogicalProcessorLocation> reservations,
        bool restoreWorkerSet)
    {
        lock (Gate)
        {
            foreach (var reservation in reservations)
            {
                Reservations.Remove(reservation);
            }
            if (restoreWorkerSet)
            {
                ApplyProcessWorkerExclusion(required: false);
            }
        }
    }

    private static unsafe void ApplyProcessWorkerExclusion(bool required)
    {
        if (!OperatingSystem.IsWindows())
        {
            if (required)
            {
                throw new PlatformNotSupportedException(
                    "Strict Linux worker isolation must be supplied by the host cpuset/cgroup.");
            }
            return;
        }
        var allowedIds = ProcessorTopology.EnumerateCandidates()
            .Where(candidate => !Reservations.Contains(candidate.Location))
            .Select(static candidate => candidate.CpuSetId)
            .ToArray();
        if (allowedIds.Length == 0)
        {
            throw new InvalidOperationException("Core isolation left no processors for ordinary workers.");
        }
        fixed (uint* pointer = allowedIds)
        {
            if (!SetProcessDefaultCpuSets(
                    GetCurrentProcess(), pointer, checked((uint)allowedIds.Length))
                && required)
            {
                throw new InvalidOperationException(
                    $"SetProcessDefaultCpuSets failed with {Marshal.GetLastPInvokeError()}.");
            }
        }
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]

    [LibraryImport("kernel32.dll")]
    private static partial nint GetCurrentProcess();

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool SetProcessDefaultCpuSets(
        nint process,
        uint* cpuSetIds,
        uint cpuSetIdCount);
}
