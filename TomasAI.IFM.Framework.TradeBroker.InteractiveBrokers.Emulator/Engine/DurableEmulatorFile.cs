using System.Runtime.InteropServices;

namespace TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine;

internal static class DurableEmulatorFile
{
    // Atomic replacement must persist both file contents and directory metadata before success.
#pragma warning disable SYSLIB1054
    [DllImport("kernel32.dll", EntryPoint = "MoveFileExW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool MoveFileEx(string source, string target, uint flags);
    [DllImport("libc", EntryPoint = "open", SetLastError = true)] static extern int OpenDirectory([MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags);
    [DllImport("libc", EntryPoint = "fsync", SetLastError = true)] static extern int SyncDirectory(int descriptor);
    [DllImport("libc", EntryPoint = "close")] static extern int CloseDirectory(int descriptor);
#pragma warning restore SYSLIB1054
    internal static void Replace(string source, string target)
    {
        if (OperatingSystem.IsWindows())
        {
            static string Native(string path) => path.StartsWith(@"\\?\", StringComparison.Ordinal) ? path
                : path.StartsWith(@"\\", StringComparison.Ordinal) ? @"\\?\UNC\" + path[2..] : @"\\?\" + path;
            if (!MoveFileEx(Native(source), Native(target), 1 | 8))
                throw new IOException("Durable recovery record replacement failed.", new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError()));
            return;
        }
        File.Move(source, target, true);
        FlushDirectory(Path.GetDirectoryName(target)!);
        if (!string.Equals(Path.GetDirectoryName(source), Path.GetDirectoryName(target), StringComparison.Ordinal))
            FlushDirectory(Path.GetDirectoryName(source)!);
    }

    internal static void EnsureDirectory(string directory)
    {
        if (Directory.Exists(directory)) return;
        var parent = Path.GetDirectoryName(directory);
        if (parent is not null) EnsureDirectory(parent);
        Directory.CreateDirectory(directory);
        if (!OperatingSystem.IsWindows() && parent is not null) FlushDirectory(parent);
    }

    static void FlushDirectory(string directory)
    {
        var descriptor = OpenDirectory(directory, 0);
        if (descriptor < 0) throw new IOException("Cannot open recovery record directory for flush.");
        try { if (SyncDirectory(descriptor) != 0) throw new IOException("Cannot flush recovery record directory."); }
        finally { _ = CloseDirectory(descriptor); }
    }

}
