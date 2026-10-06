using MessagePack;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared;

/// <summary>Immutable exact decision inputs stored with financial commands/events, independently of market history.</summary>
[MessagePackObject]
public sealed record MarketDecisionEvidence
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public string Source { get; init; } = "";
    [Key(2)] public DateTime CapturedAtUtc { get; init; }
    [Key(3)] public string Encoding { get; init; } = "";
    [Key(4)] public string Payload { get; init; } = "";
    [Key(5)] public string PayloadSha256 { get; init; } = "";

    /// <summary>Captures the exact typed inputs already used by the caller; never rereads a changing market cache.</summary>
    public static MarketDecisionEvidence Capture<T>(T inputs, DateTime capturedAtUtc)
    {
        var bytes = MessagePackSerializer.Serialize(inputs);
        return new() { Source = typeof(T).FullName!, CapturedAtUtc = capturedAtUtc, Encoding = "messagepack/base64",
            Payload = Convert.ToBase64String(bytes), PayloadSha256 = Convert.ToHexString(SHA256.HashData(bytes)) };
    }

    /// <summary>Captures immutable JSON for a manual screen's selected legs, quotes, risk values and execution choices.</summary>
    public static MarketDecisionEvidence CaptureJson<T>(string source, T inputs, DateTime capturedAtUtc)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(inputs);
        return new() { Source = source, CapturedAtUtc = capturedAtUtc, Encoding = "json/utf8",
            Payload = System.Text.Encoding.UTF8.GetString(bytes), PayloadSha256 = Convert.ToHexString(SHA256.HashData(bytes)) };
    }

    /// <summary>Validates stored bytes without depending on market history, the cache or a current quote.</summary>
    [IgnoreMember, System.Text.Json.Serialization.JsonIgnore]
    public bool IsValid
    {
        get
        {
            if (SchemaVersion != 1 || string.IsNullOrWhiteSpace(Source) || CapturedAtUtc.Kind != DateTimeKind.Utc) return false;
            try
            {
                var bytes = Encoding switch { "messagepack/base64" => Convert.FromBase64String(Payload),
                    "json/utf8" => System.Text.Encoding.UTF8.GetBytes(Payload), _ => [] };
                return bytes.Length > 0 && string.Equals(Convert.ToHexString(SHA256.HashData(bytes)), PayloadSha256, StringComparison.Ordinal);
            }
            catch (FormatException) { return false; }
        }
    }

    /// <summary>Rehydrates verified typed inputs for replay/audit without consulting realtime state.</summary>
    public T Read<T>() => IsValid && Encoding == "messagepack/base64" && Source == typeof(T).FullName
        ? MessagePackSerializer.Deserialize<T>(Convert.FromBase64String(Payload))
        : throw new InvalidDataException("Market decision evidence does not match the requested type or integrity hash.");
}
