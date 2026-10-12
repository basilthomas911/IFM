using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Text;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

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

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]

    [LibraryImport("libc", EntryPoint = "sched_setaffinity", SetLastError = true)]
    private static unsafe partial int SchedSetAffinity(
        int processId,
        nuint cpuSetSize,
        NativeCpuSet* mask);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]

    [LibraryImport("libc", EntryPoint = "sched_getaffinity", SetLastError = true)]
    private static unsafe partial int SchedGetAffinity(
        int processId,
        nuint cpuSetSize,
        NativeCpuSet* mask);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]

    [LibraryImport("libc", EntryPoint = "gettid")]
    private static partial int GetTid();

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]

    [LibraryImport("libc", EntryPoint = "setpriority", SetLastError = true)]
    private static partial int SetPriority(int which, int who, int priority);
}
