using System.Runtime.InteropServices;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

internal readonly record struct ProcessorCandidate(
    LogicalProcessorLocation Location,
    uint CpuSetId,
    byte CoreIndex,
    byte NumaNodeIndex,
    byte EfficiencyClass);

internal static partial class ProcessorTopology
{
    internal static IReadOnlyList<ProcessorCandidate> EnumerateCandidates()
    {
        if (!OperatingSystem.IsWindows())
        {
            var count = Math.Max(1, Environment.ProcessorCount);
            var result = new ProcessorCandidate[count];
            for (var index = 0; index < count; index++)
            {
                result[index] = new ProcessorCandidate(
                    new LogicalProcessorLocation(0, checked((ushort)index)),
                    checked((uint)index),
                    checked((byte)Math.Min(index, byte.MaxValue)),
                    0,
                    0);
            }
            return result;
        }
        return EnumerateWindowsCpuSets();
    }

    internal static (
        LogicalProcessorLocation NativeProducer,
        LogicalProcessorLocation ManagedDrain) ResolvePair(
        bool requirePerformanceCore,
        IReadOnlySet<LogicalProcessorLocation>? excluded = null)
    {
        var processors = EnumerateCandidates();
        var highestPerformanceClass = processors.Max(static item => item.EfficiencyClass);
        var candidates = processors
            .Where(item => (excluded is null || !excluded.Contains(item.Location))
                           && (!requirePerformanceCore
                               || item.EfficiencyClass == highestPerformanceClass))
            .OrderByDescending(static item => item.EfficiencyClass)
            .ThenBy(static item => item.NumaNodeIndex)
            .ThenBy(static item => item.Location.ProcessorGroup)
            .ThenBy(static item => item.CoreIndex)
            .ThenBy(static item => item.Location.LogicalProcessorIndex)
            .ToArray();
        if (candidates.Length < 2)
        {
            throw new InvalidOperationException(
                "Two available performance-core processor locations are required for feed affinity.");
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
        return (first.Location, candidates[secondIndex].Location);
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
        ushort? numaNode = null,
        bool releasable = false,
        bool restoreWorkerSet = false,
        IReadOnlyList<LogicalProcessorLocation>? reservations = null)
    {
        NativeProducer = nativeProducer;
        ManagedDrain = managedDrain;
        NumaNode = numaNode;
        _releasable = releasable;
        _restoreWorkerSet = restoreWorkerSet;
        _reservations = reservations ?? Array.Empty<LogicalProcessorLocation>();
    }

    internal LogicalProcessorLocation? NativeProducer { get; }
    internal LogicalProcessorLocation? ManagedDrain { get; }
    internal ushort? NumaNode { get; }

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
        FeedNumaOptions numa)
    {
        lock (Gate)
        {
            if (affinity.Mode == CpuAffinityMode.Unpinned)
            {
                if (isolation.RequireCoreIsolation)
                {
                    throw new InvalidOperationException(
                        "Strict core isolation cannot be combined with unpinned feed threads.");
                }
                return new FeedPlacementLease(null, null);
            }
            var candidates = ProcessorTopology.EnumerateCandidates();
            var performanceClass = candidates.Max(static item => item.EfficiencyClass);
            (LogicalProcessorLocation Producer, LogicalProcessorLocation Drain) pair;
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
                    && (producer.EfficiencyClass != performanceClass
                        || drain.EfficiencyClass != performanceClass))
                {
                    throw new InvalidOperationException(
                        "Explicit feed processors must be selected from performance cores.");
                }
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
                pair = ProcessorTopology.ResolvePair(
                    affinity.RequirePerformanceCore,
                    Reservations);
                resolvedNumaNode = candidates
                    .Single(item => item.Location == pair.Producer)
                    .NumaNodeIndex;
            }
            if (numa.Mode == NumaLocalityMode.ExplicitNode
                && resolvedNumaNode != numa.Node)
            {
                throw new InvalidOperationException(
                    "The selected feed processors do not belong to the explicit NUMA node.");
            }
            var selectedProducer = candidates.Single(item => item.Location == pair.Producer);
            var selectedDrain = candidates.Single(item => item.Location == pair.Drain);
            var newReservations = isolation.Mode == FeedCoreIsolationMode.ExcludeFromProcessWorkers
                ? candidates
                    .Where(item =>
                        item.Location.ProcessorGroup == selectedProducer.Location.ProcessorGroup
                        && item.CoreIndex == selectedProducer.CoreIndex
                        || item.Location.ProcessorGroup == selectedDrain.Location.ProcessorGroup
                        && item.CoreIndex == selectedDrain.CoreIndex)
                    .Select(static item => item.Location)
                    .Distinct()
                    .ToArray()
                : [pair.Producer, pair.Drain];
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
                    numa.Mode == NumaLocalityMode.Disabled ? null : resolvedNumaNode,
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

    internal static void Apply(LogicalProcessorLocation location)
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
    }

    [LibraryImport("kernel32.dll")]
    private static partial nint GetCurrentThread();

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetThreadGroupAffinity(
        nint thread,
        ref GroupAffinity groupAffinity,
        nint previousGroupAffinity);
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

    internal static unsafe void Apply(
        LogicalProcessorLocation? location,
        FeedThreadPriority priority)
    {
        if (location is { } processor)
        {
            if (processor.ProcessorGroup != 0
                || processor.LogicalProcessorIndex >= CpuSetBitCount)
            {
                throw new InvalidOperationException(
                    "The managed drain processor is outside the Linux CPU-set range.");
            }
            var cpuSet = new NativeCpuSet();
            cpuSet.Bits[processor.LogicalProcessorIndex / 64] =
                1UL << (processor.LogicalProcessorIndex % 64);
            if (SchedSetAffinity(0, (nuint)sizeof(NativeCpuSet), &cpuSet) != 0)
            {
                throw new InvalidOperationException(
                    $"sched_setaffinity failed with {Marshal.GetLastPInvokeError()}.");
            }
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
    }

    [LibraryImport("libc", EntryPoint = "sched_setaffinity", SetLastError = true)]
    private static unsafe partial int SchedSetAffinity(
        int processId,
        nuint cpuSetSize,
        NativeCpuSet* mask);

    [LibraryImport("libc", EntryPoint = "gettid")]
    private static partial int GetTid();

    [LibraryImport("libc", EntryPoint = "setpriority", SetLastError = true)]
    private static partial int SetPriority(int which, int who, int priority);
}
