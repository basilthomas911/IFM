using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TomasAI.IFM.Application.ServerManager.SchedulerHost;

/// <summary>Owns the launched task and its descendants independently of task cooperation.</summary>
public interface IScheduledProcessContainment : IDisposable
{
    /// <summary>Prepares the executable to establish platform containment before task code runs.</summary>
    void Prepare(ProcessStartInfo startInfo);
    /// <summary>Verifies/assigns ownership immediately after starting the process.</summary>
    void Assign(Process process);
    /// <summary>Terminates only the owned process tree.</summary>
    void Terminate();
}

/// <summary>Creates the supported platform's task containment.</summary>
public static class ScheduledProcessContainment
{
    /// <summary>Creates a Windows Job Object or an owned Linux process group.</summary>
    public static IScheduledProcessContainment Create()
        => OperatingSystem.IsWindows() ? new WindowsContainment()
         : OperatingSystem.IsLinux() ? new LinuxContainment()
         : throw new PlatformNotSupportedException("SchedulerHost supports Windows and Linux.");

    private sealed class WindowsContainment : IScheduledProcessContainment
    {
        private readonly WindowsJobObject job = new();
        public void Prepare(ProcessStartInfo startInfo) { }
        public void Assign(Process process) => job.Assign(process);
        public void Terminate() => job.Terminate();
        public void Dispose() => job.Dispose();
    }

    private sealed class LinuxContainment : IScheduledProcessContainment
    {
        private int group;
        // A group-owned watcher also kills descendants if a foreground host dies without cleanup.
        // argv is passed as positional arguments, never interpolated into shell source.
        private const string Wrapper = """
            parent=$1
            stamp=$2
            shift 2
            group=$$
            (
                while [ -r "/proc/$parent/stat" ] && [ "$(awk '{sub(/^.*\) /, ""); split($0, fields, " " ); print fields[20]}' "/proc/$parent/stat")" = "$stamp" ]; do
                    sleep 0.2
                done
                kill -KILL -"$group"
            ) >/dev/null 2>&1 &
            watcher=$!
            "$@" &
            child=$!
            wait "$child"
            result=$?
            kill "$watcher" 2>/dev/null
            wait "$watcher" 2>/dev/null
            exit "$result"
            """;

        public void Prepare(ProcessStartInfo startInfo)
        {
            const string setsid = "/usr/bin/setsid";
            if (!File.Exists(setsid) || !File.Exists("/usr/bin/awk"))
                throw new PlatformNotSupportedException("Linux task containment requires setsid and awk.");
            var executable = startInfo.FileName;
            var arguments = startInfo.ArgumentList.ToArray();
            if (!string.IsNullOrEmpty(startInfo.Arguments))
                throw new InvalidOperationException("Scheduled tasks require structured ArgumentList arguments.");
            var stat = File.ReadAllText($"/proc/{Environment.ProcessId}/stat");
            var stamp = stat[(stat.LastIndexOf(')') + 2)..].Split(' ')[19];
            startInfo.FileName = setsid;
            startInfo.ArgumentList.Clear();
            foreach (var argument in new[] { "/bin/sh", "-c", Wrapper.Replace("\r\n", "\n", StringComparison.Ordinal), "ifm-task",
                Environment.ProcessId.ToString(), stamp, executable }.Concat(arguments))
                startInfo.ArgumentList.Add(argument);
        }

        public void Assign(Process process)
        {
            // setsid must establish the new session before this check; the initial child may
            // still be in its parent's group for a few scheduler ticks.
            var started = Stopwatch.GetTimestamp();
            while (!process.HasExited && Native.getpgid(process.Id) != process.Id)
            {
                if (Stopwatch.GetElapsedTime(started) >= TimeSpan.FromSeconds(2))
                    throw new TimeoutException("SchedulerHost.PROCESS.CONTAINMENT_FAILED; setsid did not establish task ownership.");
                Thread.Sleep(1);
            }
            if (!process.HasExited || Native.getpgid(process.Id) == process.Id)
                group = process.Id;
        }

        public void Terminate()
        {
            if (group == 0) return;
            if (Native.kill(-group, 9) != 0 && Marshal.GetLastWin32Error() != 3)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not terminate the owned scheduled-task group.");
        }

        public void Dispose() => Terminate();

        private static class Native
        {
            [DllImport("libc", SetLastError = true)] internal static extern int getpgid(int pid);
            [DllImport("libc", SetLastError = true)] internal static extern int kill(int pid, int signal);
        }
    }
}
