using System.Runtime.InteropServices;
namespace TomasAI.IFM.Application.MarketData.Host.Worker;

internal static class NativeMethods
{
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("libc", EntryPoint = "setpgid")]
    internal static extern int setpgid(int processId, int processGroupId);
}
