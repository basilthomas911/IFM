using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.Recovery;

/// <summary>Immutable version-one native fact. Dates are supplied by the API, never inferred by the recorder.</summary>
public sealed record RecoveryRecord
{
    public int Version { get; init; } = 1;
    public Guid RecordId { get; init; }
    public Guid OperationId { get; init; }
    public long Sequence { get; init; }
    public string Kind { get; init; } = "";
    public string RuntimeId { get; init; } = "";
    public string Role { get; init; } = "";
    public string Dataset { get; init; } = "";
    public DateOnly ValueDate { get; init; }
    public DateTimeOffset OccurredAtUtc { get; init; }
    public DateTimeOffset StartedAtUtc { get; init; }
    public DateTimeOffset RecoveryDeadlineUtc { get; init; }
    public DateTimeOffset? ResetDeadlineUtc { get; init; }
    public long ElapsedMilliseconds { get; init; }
    public string OldOwner { get; init; } = "";
    public string NewOwner { get; init; } = "";
    public long Generation { get; init; }
    public long RetryCount { get; init; }
    public long Revision { get; init; }
    public long EpisodeCounter { get; init; }
    public string OriginalError { get; init; } = "";
    public string LatestError { get; init; } = "";
    public string CompletionScope { get; init; } = "TransportOnly";
    public bool OldResourcesRetired { get; init; }
    public bool SubscriptionVerified { get; init; }
    public bool ProviderActivityVerified { get; init; }
    public bool MonitoringReady { get; init; }
    public string[] MissingProofs { get; init; } = [];
    public string[] AffectedWorkloads { get; init; } = [];
    public bool DiagnosticsTruncated { get; init; }
    public static readonly IReadOnlySet<string> Kinds = new HashSet<string>(StringComparer.Ordinal)
    { "Started", "ResetStarted", "ResetCompleted", "ConnectionRestored", "Completed", "Failed", "MonitoringRestored", "Interrupted" };
    /// <summary>Validates before persistence. The digest covers the exact UTF-8 JSON bytes, including field order.</summary>
    public static RecoveryRecord Parse(string json, string digest)
    {
        if (Encoding.UTF8.GetByteCount(json) > 256 * 1024 ||
            !string.Equals(Digest(json), digest, StringComparison.Ordinal))
            throw new InvalidDataException("Recovery record size/digest mismatch.");
        var record = JsonSerializer.Deserialize<RecoveryRecord>(json) ?? throw new InvalidDataException("Missing recovery record.");
        if (record.Version != 1 || record.RecordId == Guid.Empty || record.OperationId == Guid.Empty ||
            record.Sequence < 1 || !Kinds.Contains(record.Kind) || record.ValueDate == default ||
            string.IsNullOrWhiteSpace(record.RuntimeId) || record.Role.Length is < 1 or > 32 ||
            record.Dataset.Length is < 1 or > 64 || record.ElapsedMilliseconds < 0 || record.RetryCount<0 || record.Revision<0 || record.EpisodeCounter<0 ||
            record.OccurredAtUtc==default || record.StartedAtUtc==default || record.RecoveryDeadlineUtc==default ||
            record.OccurredAtUtc.Offset != TimeSpan.Zero || record.StartedAtUtc.Offset != TimeSpan.Zero ||
            record.RecoveryDeadlineUtc.Offset!=TimeSpan.Zero || record.ResetDeadlineUtc?.Offset is { } offset && offset!=TimeSpan.Zero ||
            record.RecoveryDeadlineUtc < record.StartedAtUtc || record.OccurredAtUtc < record.StartedAtUtc ||
            record.OriginalError.Length > 16384 || record.LatestError.Length > 16384 ||
            record.MissingProofs.Length > 16 || record.AffectedWorkloads.Length > 256 ||
            record.CompletionScope is not ("TransportOnly" or "DatasetOperational" or "MonitoringReady") ||
            ((record.CompletionScope == "MonitoringReady") != record.MonitoringReady) ||
            (record.MonitoringReady && record.MissingProofs.Length != 0) ||
            (record.Kind == "MonitoringRestored" && !record.MonitoringReady) ||
            ((record.Sequence == 1) != (record.Kind == "Started")))
            throw new InvalidDataException("Invalid recovery record identity, timing, sequence or evidence.");
        return record;
    }
    /// <summary>Lower-case SHA-256 of the unchanged wire payload; attempts do not enter this digest.</summary>
    public static string Digest(string json) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
}
