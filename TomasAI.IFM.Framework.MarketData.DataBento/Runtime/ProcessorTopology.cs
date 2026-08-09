using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Text;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

internal enum ProcessorCoreKind : byte
{
    Unknown = 0,
    Performance = 1,
    Efficiency = 2
}

internal readonly record struct ProcessorCandidate(
    LogicalProcessorLocation Location,
    uint CpuSetId,
    int CoreIndex,
    ushort NumaNodeIndex,
    byte EfficiencyClass,
    ProcessorCoreKind CoreKind = ProcessorCoreKind.Unknown);

internal readonly record struct ProcessorPairResolution(
    LogicalProcessorLocation NativeProducer,
    LogicalProcessorLocation ManagedDrain,
    bool PerformanceCoreClassificationAvailable,
    bool PerformanceCoresSelected);

internal static partial class ProcessorTopology
{
    internal static IReadOnlyList<ProcessorCandidate> EnumerateCandidates()
    {
        List<ProcessorCandidate> candidates;
        if (OperatingSystem.IsWindows())
        {
            candidates = EnumerateWindowsCpuSets();
        }
        else if (OperatingSystem.IsLinux())
        {
            candidates = EnumerateLinuxCpuSet();
        }
        else
        {
            throw new PlatformNotSupportedException(
                "Processor topology is supported only on Windows and Linux.");
        }
        if (candidates.Count == 0)
        {
            throw new InvalidOperationException("No processors are available to this process.");
        }
        return ClassifyIntelHybridCores(candidates);
    }

    internal static (
        LogicalProcessorLocation NativeProducer,
        LogicalProcessorLocation ManagedDrain) ResolvePair(
        bool requirePerformanceCore,
        IReadOnlySet<LogicalProcessorLocation>? excluded = null)
    {
        var resolution = ResolvePairWithMetadata(requirePerformanceCore, excluded);
        return (resolution.NativeProducer, resolution.ManagedDrain);
    }

    internal static ProcessorPairResolution ResolvePairWithMetadata(
        bool preferPerformanceCore,
        IReadOnlySet<LogicalProcessorLocation>? excluded = null,
        bool allowAffinityFallback = true) =>
        ResolvePairWithMetadata(
            EnumerateCandidates(),
            preferPerformanceCore,
            excluded,
            allowAffinityFallback);

    internal static ProcessorPairResolution ResolvePairWithMetadata(
        IReadOnlyList<ProcessorCandidate> processors,
        bool preferPerformanceCore,
        IReadOnlySet<LogicalProcessorLocation>? excluded = null,
        bool allowAffinityFallback = true)
    {
        var performanceClassificationAvailable = HasPerformanceCoreClassification(processors);
        if (preferPerformanceCore
            && !performanceClassificationAvailable
            && !allowAffinityFallback)
        {
            throw new InvalidOperationException(
                "Performance-core classification is unavailable and affinity fallback is disabled.");
        }
        var performanceLocations = performanceClassificationAvailable
            ? GetPerformanceCoreLocations(processors)
            : null;
        var candidates = processors
            .Where(item => (excluded is null || !excluded.Contains(item.Location))
                           && (!preferPerformanceCore
                               || !performanceClassificationAvailable
                               || performanceLocations!.Contains(item.Location)))
            .OrderByDescending(static item => item.CoreKind == ProcessorCoreKind.Performance)
            .ThenByDescending(static item => item.EfficiencyClass)
            .ThenBy(static item => item.NumaNodeIndex)
            .ThenBy(static item => item.Location.ProcessorGroup)
            .ThenBy(static item => item.CoreIndex)
            .ThenBy(static item => item.Location.LogicalProcessorIndex)
            .ToArray();
        if (candidates.Length < 2)
        {
            throw new InvalidOperationException(
                performanceClassificationAvailable && preferPerformanceCore
                    ? "Two available performance-core processor locations are required for feed affinity."
                    : "Two available processor locations are required for feed affinity.");
        }
        var first = candidates[0];
        var secondIndex = Array.FindIndex(candidates, 1, item =>
            item.NumaNodeIndex == first.NumaNodeIndex
            && (item.Location.ProcessorGroup != first.Location.ProcessorGroup
                || item.CoreIndex != first.CoreIndex));
        if (secondIndex < 0)
        {
            secondIndex = Array.FindIndex(candidates, 1, item =>
                item.NumaNodeIndex == first.NumaNodeIndex);
        }
        if (secondIndex < 0)
        {
            throw new InvalidOperationException(
                "Two available processor locations on the same NUMA node are required for feed affinity.");
        }
        return new ProcessorPairResolution(
            first.Location,
            candidates[secondIndex].Location,
            performanceClassificationAvailable,
            performanceClassificationAvailable && preferPerformanceCore);
    }

    internal static bool HasPerformanceCoreClassification(
        IReadOnlyList<ProcessorCandidate> processors) =>
        processors.Any(static item => item.CoreKind != ProcessorCoreKind.Unknown)
        || processors.Select(static item => item.EfficiencyClass).Distinct().Skip(1).Any();

    internal static HashSet<LogicalProcessorLocation> GetPerformanceCoreLocations(
        IReadOnlyList<ProcessorCandidate> processors)
    {
        if (processors.Any(static item => item.CoreKind != ProcessorCoreKind.Unknown))
        {
            return processors
                .Where(static item => item.CoreKind == ProcessorCoreKind.Performance)
                .Select(static item => item.Location)
                .ToHashSet();
        }
        var highestPerformanceClass = processors.Max(static item => item.EfficiencyClass);
        return processors
            .Where(item => item.EfficiencyClass == highestPerformanceClass)
            .Select(static item => item.Location)
            .ToHashSet();
    }

    private static List<ProcessorCandidate> EnumerateLinuxCpuSet()
    {
        var allowedProcessors = LinuxThreadConfiguration.GetAllowedProcessorIndices();
        var numaNodes = ReadLinuxNumaNodes();
        var physicalCores = new Dictionary<(int Package, int Core), int>();
        var result = new List<ProcessorCandidate>(allowedProcessors.Count);
        foreach (var processor in allowedProcessors)
        {
            var topologyPath = $"/sys/devices/system/cpu/cpu{processor}/topology";
            var package = ReadLinuxInteger(Path.Combine(topologyPath, "physical_package_id"), 0);
            var core = ReadLinuxInteger(Path.Combine(topologyPath, "core_id"), processor);
            if (!physicalCores.TryGetValue((package, core), out var physicalCoreIndex))
            {
                physicalCoreIndex = physicalCores.Count;
                physicalCores.Add((package, core), physicalCoreIndex);
            }
            result.Add(new ProcessorCandidate(
                new LogicalProcessorLocation(0, checked((ushort)processor)),
                checked((uint)processor),
                physicalCoreIndex,
                numaNodes.TryGetValue(processor, out var node) ? node : (ushort)0,
                0));
        }
        return result;
    }

    private static Dictionary<int, ushort> ReadLinuxNumaNodes()
    {
        const string nodesPath = "/sys/devices/system/node";
        var result = new Dictionary<int, ushort>();
        try
        {
            if (!Directory.Exists(nodesPath))
            {
                return result;
            }
            foreach (var nodePath in Directory.EnumerateDirectories(nodesPath, "node*"))
            {
                var name = Path.GetFileName(nodePath);
                if (!ushort.TryParse(name.AsSpan(4), out var node))
                {
                    continue;
                }
                var cpuListPath = Path.Combine(nodePath, "cpulist");
                if (!File.Exists(cpuListPath))
                {
                    continue;
                }
                foreach (var processor in ParseLinuxCpuList(File.ReadAllText(cpuListPath)))
                {
                    result[processor] = node;
                }
            }
        }
        catch (Exception exception) when (exception is IOException
                                          or UnauthorizedAccessException
                                          or InvalidDataException)
        {
            result.Clear();
        }
        return result;
    }

    internal static IReadOnlyList<int> ParseLinuxCpuList(string value)
    {
        var result = new List<int>();
        foreach (var segment in value.Trim().Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var bounds = segment.Split('-', 2);
            if (!int.TryParse(bounds[0], out var first) || first < 0)
            {
                throw new InvalidDataException($"Invalid Linux CPU-list segment '{segment}'.");
            }
            var last = first;
            if (bounds.Length == 2
                && (!int.TryParse(bounds[1], out last) || last < first))
            {
                throw new InvalidDataException($"Invalid Linux CPU-list segment '{segment}'.");
            }
            for (var processor = first; processor <= last; processor++)
            {
                result.Add(processor);
            }
        }
        return result;
    }

    private static int ReadLinuxInteger(string path, int fallback)
    {
        try
        {
            if (!File.Exists(path))
            {
                return fallback;
            }
            return int.TryParse(File.ReadAllText(path).Trim(), out var value) ? value : fallback;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return fallback;
        }
    }

    private static IReadOnlyList<ProcessorCandidate> ClassifyIntelHybridCores(
        List<ProcessorCandidate> candidates)
    {
        if (!IsIntelHybridProcessor())
        {
            return candidates;
        }
        var classified = candidates.ToArray();
        Exception? failure = null;
        var probe = new Thread(() =>
        {
            try
            {
                for (var index = 0; index < classified.Length; index++)
                {
                    ApplyProbeAffinity(classified[index].Location);
                    var cpuId = X86Base.CpuId(0x1a, 0);
                    var coreType = (byte)((uint)cpuId.Eax >> 24);
                    classified[index] = classified[index] with
                    {
                        CoreKind = coreType switch
                        {
                            0x40 => ProcessorCoreKind.Performance,
                            0x20 => ProcessorCoreKind.Efficiency,
                            _ => ProcessorCoreKind.Unknown
                        }
                    };
                }
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        })
        {
            IsBackground = true,
            Name = "Databento CPU topology probe"
        };
        probe.Start();
        probe.Join();
        return failure is null ? classified : candidates;
    }

    private static void ApplyProbeAffinity(LogicalProcessorLocation location)
    {
        if (OperatingSystem.IsWindows())
        {
            WindowsThreadAffinity.Apply(location);
            return;
        }
        LinuxThreadConfiguration.ApplyAffinity(location);
    }

    private static bool IsIntelHybridProcessor()
    {
        if (!X86Base.IsSupported)
        {
            return false;
        }
        var root = X86Base.CpuId(0, 0);
        if ((uint)root.Eax < 0x1a)
        {
            return false;
        }
        Span<byte> vendorBytes = stackalloc byte[12];
        BinaryPrimitives.WriteInt32LittleEndian(vendorBytes, root.Ebx);
        BinaryPrimitives.WriteInt32LittleEndian(vendorBytes[4..], root.Edx);
        BinaryPrimitives.WriteInt32LittleEndian(vendorBytes[8..], root.Ecx);
        if (!Encoding.ASCII.GetString(vendorBytes).Equals("GenuineIntel", StringComparison.Ordinal))
        {
            return false;
        }
        var features = X86Base.CpuId(7, 0);
        return ((uint)features.Edx & (1u << 15)) != 0;
    }

    private static unsafe List<ProcessorCandidate> EnumerateWindowsCpuSets()
    {
        GetSystemCpuSetInformation(null, 0, out var required, 0, 0);
        if (required == 0)
        {
            throw new InvalidOperationException("Windows CPU-set topology is unavailable.");
        }
        var buffer = new byte[required];
        fixed (byte* pointer = buffer)
        {
            if (!GetSystemCpuSetInformation(pointer, required, out _, 0, 0))
            {
                throw new InvalidOperationException(
                    $"GetSystemCpuSetInformation failed with {Marshal.GetLastPInvokeError()}.");
            }
        }
        var result = new List<ProcessorCandidate>();
        var offset = 0u;
        while (offset + 20 <= required)
        {
            var size = BitConverter.ToUInt32(buffer, checked((int)offset));
            var type = BitConverter.ToInt32(buffer, checked((int)offset + 4));
            if (size < 20 || offset + size > required)
            {
                throw new InvalidDataException("Windows returned malformed CPU-set topology data.");
            }
            if (type == 0)
            {
                var id = BitConverter.ToUInt32(buffer, checked((int)offset + 8));
                var group = BitConverter.ToUInt16(buffer, checked((int)offset + 12));
                var logical = buffer[offset + 14];
                var core = buffer[offset + 15];
                var numaNode = buffer[offset + 17];
                var efficiency = buffer[offset + 18];
                var flags = buffer[offset + 19];
                var parked = (flags & 1) != 0;
                var allocated = (flags & 2) != 0;
                if (!parked && !allocated)
                {
                    result.Add(new ProcessorCandidate(
                        new LogicalProcessorLocation(group, logical),
                        id,
                        core,
                        numaNode,
                        efficiency));
                }
            }
            offset += size;
        }
        return result;
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool GetSystemCpuSetInformation(
        void* information,
        uint bufferLength,
        out uint returnedLength,
        nint process,
        uint flags);
}

internal sealed class FeedPlacementLease : IDisposable
{
    private readonly bool _releasable;
    private readonly bool _restoreWorkerSet;
    private readonly IReadOnlyList<LogicalProcessorLocation> _reservations;
    private int _committed;
    private int _disposed;

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

internal static partial class ProcessCoreIsolationCoordinator
{
    private static readonly object Gate = new();
    private static readonly HashSet<LogicalProcessorLocation> Reservations = [];

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

    [LibraryImport("kernel32.dll")]
    private static partial nint GetCurrentProcess();

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool SetProcessDefaultCpuSets(
        nint process,
        uint* cpuSetIds,
        uint cpuSetIdCount);
}

internal static partial class WindowsThreadAffinity
{
    [StructLayout(LayoutKind.Sequential)]
    private struct GroupAffinity
    {
        internal nuint Mask;
        internal ushort Group;
        private ushort Reserved0;
        private ushort Reserved1;
        private ushort Reserved2;
    }

    internal static LogicalProcessorLocation Apply(LogicalProcessorLocation location)
    {
        var affinity = new GroupAffinity
        {
            Group = location.ProcessorGroup,
            Mask = (nuint)1 << (location.LogicalProcessorIndex % 64)
        };
        if (!SetThreadGroupAffinity(GetCurrentThread(), ref affinity, 0))
        {
            throw new InvalidOperationException(
                $"SetThreadGroupAffinity failed with {Marshal.GetLastPInvokeError()}.");
        }
        var observed = new GroupAffinity();
        if (!GetThreadGroupAffinity(GetCurrentThread(), ref observed)
            || observed.Group != affinity.Group
            || observed.Mask != affinity.Mask)
        {
            throw new InvalidOperationException(
                "Windows did not retain the requested single-processor thread affinity.");
        }
        return location;
    }

    [LibraryImport("kernel32.dll")]
    private static partial nint GetCurrentThread();

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetThreadGroupAffinity(
        nint thread,
        ref GroupAffinity groupAffinity,
        nint previousGroupAffinity);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetThreadGroupAffinity(
        nint thread,
        ref GroupAffinity groupAffinity);
}

internal static partial class LinuxThreadConfiguration
{
    private const int PriorityProcess = 0;
    private const int CpuSetBitCount = 1_024;

    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct NativeCpuSet
    {
        internal fixed ulong Bits[CpuSetBitCount / 64];
    }

    internal static unsafe LogicalProcessorLocation? Apply(
        LogicalProcessorLocation? location,
        FeedThreadPriority priority)
    {
        LogicalProcessorLocation? observed = null;
        if (location is { } processor)
        {
            observed = ApplyAffinity(processor);
        }

        var niceValue = priority switch
        {
            FeedThreadPriority.Normal => 0,
            FeedThreadPriority.AboveNormal => -5,
            FeedThreadPriority.Highest => -10,
            _ => throw new ArgumentOutOfRangeException(nameof(priority))
        };
        if (niceValue != 0
            && SetPriority(PriorityProcess, GetTid(), niceValue) != 0)
        {
            throw new InvalidOperationException(
                $"setpriority failed with {Marshal.GetLastPInvokeError()}.");
        }
        return observed;
    }

    internal static unsafe LogicalProcessorLocation ApplyAffinity(
        LogicalProcessorLocation processor)
    {
        ValidateProcessor(processor);
        var cpuSet = new NativeCpuSet();
        cpuSet.Bits[processor.LogicalProcessorIndex / 64] =
            1UL << (processor.LogicalProcessorIndex % 64);
        if (SchedSetAffinity(0, (nuint)sizeof(NativeCpuSet), &cpuSet) != 0)
        {
            throw new InvalidOperationException(
                $"sched_setaffinity failed with {Marshal.GetLastPInvokeError()}.");
        }
        var observed = new NativeCpuSet();
        if (SchedGetAffinity(0, (nuint)sizeof(NativeCpuSet), &observed) != 0)
        {
            throw new InvalidOperationException(
                $"sched_getaffinity failed with {Marshal.GetLastPInvokeError()}.");
        }
        for (var index = 0; index < CpuSetBitCount; index++)
        {
            var isSet = (observed.Bits[index / 64] & (1UL << (index % 64))) != 0;
            if (isSet != (index == processor.LogicalProcessorIndex))
            {
                throw new InvalidOperationException(
                    "Linux did not retain the requested single-processor thread affinity.");
            }
        }
        return processor;
    }

    internal static unsafe IReadOnlyList<int> GetAllowedProcessorIndices()
    {
        var cpuSet = new NativeCpuSet();
        if (SchedGetAffinity(0, (nuint)sizeof(NativeCpuSet), &cpuSet) != 0)
        {
            throw new InvalidOperationException(
                $"sched_getaffinity failed with {Marshal.GetLastPInvokeError()}.");
        }
        var result = new List<int>();
        for (var index = 0; index < CpuSetBitCount; index++)
        {
            if ((cpuSet.Bits[index / 64] & (1UL << (index % 64))) != 0)
            {
                result.Add(index);
            }
        }
        return result;
    }

    private static void ValidateProcessor(LogicalProcessorLocation processor)
    {
        if (processor.ProcessorGroup != 0
            || processor.LogicalProcessorIndex >= CpuSetBitCount)
        {
            throw new InvalidOperationException(
                "The processor is outside the Linux CPU-set range.");
        }
    }

    [LibraryImport("libc", EntryPoint = "sched_setaffinity", SetLastError = true)]
    private static unsafe partial int SchedSetAffinity(
        int processId,
        nuint cpuSetSize,
        NativeCpuSet* mask);

    [LibraryImport("libc", EntryPoint = "sched_getaffinity", SetLastError = true)]
    private static unsafe partial int SchedGetAffinity(
        int processId,
        nuint cpuSetSize,
        NativeCpuSet* mask);

    [LibraryImport("libc", EntryPoint = "gettid")]
    private static partial int GetTid();

    [LibraryImport("libc", EntryPoint = "setpriority", SetLastError = true)]
    private static partial int SetPriority(int which, int who, int priority);
}

internal static partial class CurrentProcessor
{
    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessorNumber
    {
        internal ushort Group;
        internal byte Number;
        private byte Reserved;
    }

    internal static LogicalProcessorLocation Get()
    {
        if (OperatingSystem.IsWindows())
        {
            GetCurrentProcessorNumberEx(out var processor);
            return new LogicalProcessorLocation(processor.Group, processor.Number);
        }
        if (OperatingSystem.IsLinux())
        {
            var processor = SchedGetCpu();
            if (processor < 0 || processor > ushort.MaxValue)
            {
                throw new InvalidOperationException(
                    $"sched_getcpu failed with {Marshal.GetLastPInvokeError()}.");
            }
            return new LogicalProcessorLocation(0, checked((ushort)processor));
        }
        throw new PlatformNotSupportedException(
            "Processor observation is supported only on Windows and Linux.");
    }

    [LibraryImport("kernel32.dll")]
    private static partial void GetCurrentProcessorNumberEx(out ProcessorNumber processorNumber);

    [LibraryImport("libc", EntryPoint = "sched_getcpu", SetLastError = true)]
    private static partial int SchedGetCpu();
}
