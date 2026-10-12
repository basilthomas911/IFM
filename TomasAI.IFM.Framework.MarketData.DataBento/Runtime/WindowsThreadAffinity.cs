using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Text;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

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

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]

    [LibraryImport("kernel32.dll")]
    private static partial nint GetCurrentThread();

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetThreadGroupAffinity(
        nint thread,
        ref GroupAffinity groupAffinity,
        nint previousGroupAffinity);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetThreadGroupAffinity(
        nint thread,
        ref GroupAffinity groupAffinity);
}
