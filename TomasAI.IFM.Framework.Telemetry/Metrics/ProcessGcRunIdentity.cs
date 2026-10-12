using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace TomasAI.IFM.Framework.Telemetry.Metrics;

/// <summary>Identifies one process lifetime independently of PID reuse and application restarts.</summary>
public static class ProcessGcRunIdentity
{
    /// <summary>Gets the operating-system process start time in UTC.</summary>
    public static DateTimeOffset StartedUtc { get; } = ReadStartTime();
    /// <summary>Gets the unique identity shared by GC history and OTel resource attributes.</summary>
    public static string InstanceId { get; } = $"{Environment.MachineName}/{Environment.ProcessId}/{StartedUtc.UtcTicks}";
    static DateTimeOffset ReadStartTime()
    {
        using var process = Process.GetCurrentProcess();
        return new DateTimeOffset(process.StartTime.ToUniversalTime());
    }
}
