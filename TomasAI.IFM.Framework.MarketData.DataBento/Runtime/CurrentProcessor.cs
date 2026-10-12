using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Text;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

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

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]

    [LibraryImport("kernel32.dll")]
    private static partial void GetCurrentProcessorNumberEx(out ProcessorNumber processorNumber);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]

    [LibraryImport("libc", EntryPoint = "sched_getcpu", SetLastError = true)]
    private static partial int SchedGetCpu();
}
