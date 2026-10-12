using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Text;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

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
            if (!int.TryParse(bounds[0], System.Globalization.CultureInfo.InvariantCulture, out var first) || first < 0)
            {
                throw new InvalidDataException($"Invalid Linux CPU-list segment '{segment}'.");
            }
            var last = first;
            if (bounds.Length == 2
                && (!int.TryParse(bounds[1], System.Globalization.CultureInfo.InvariantCulture, out last) || last < first))
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
            return int.TryParse(File.ReadAllText(path).Trim(), System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : fallback;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return fallback;
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Transfer the dedicated probe thread failure back to the owning thread.")]
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

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool GetSystemCpuSetInformation(
        void* information,
        uint bufferLength,
        out uint returnedLength,
        nint process,
        uint flags);
}
