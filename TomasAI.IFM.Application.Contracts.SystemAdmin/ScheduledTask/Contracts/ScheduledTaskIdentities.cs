using System.Security.Cryptography;
using System.Text;
namespace TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
/// <summary>Derives stable host routes and scheduled occurrence identities across delivery retries.</summary>
public static class ScheduledTaskIdentities
{
    /// <summary>Identifies the catalog owned by exactly one environment and host.</summary>
    public static ScheduledTaskId Catalog(string environment, string hostId) => Hash($"Catalog:{environment}:{hostId}");
    /// <summary>Identifies one automatic occurrence independently of definition revision or UTC offset representation.</summary>
    public static ScheduledTaskId Occurrence(ScheduledTaskId scheduleId, DateTimeOffset intendedFireTimeUtc) => Hash($"Occurrence:{scheduleId.Format()}:{intendedFireTimeUtc.UtcTicks}");
    /// <summary>Formats a host-specific runtime mailbox to avoid cross-host control consumption.</summary>
    public static string RuntimeActor(string environment, string hostId) => $"ScheduledTaskRuntime{Catalog(environment, hostId).Format()}";
    /// <summary>Derives a fixed 128-bit route identity from a namespaced SHA-256 value.</summary>
    private static ScheduledTaskId Hash(string value) => new(new Guid(SHA256.HashData(Encoding.UTF8.GetBytes("IFM.ScheduledTask:" + value)).AsSpan(0, 16)));
}
