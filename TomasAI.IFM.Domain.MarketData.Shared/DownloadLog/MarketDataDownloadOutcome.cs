using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;

public enum MarketDataDownloadDataset { Unknown = 0, EconomicCalendar = 1, TreasuryCurve = 2 }
public enum MarketDataDownloadStatus { Unknown = 0, Completed = 1, Failed = 2 }

[MessagePackObject]
public sealed record DownloadLogId([property: Key(0)] Guid ImportCommandId) : IActorEntityId
{
    public string Format() => ImportCommandId.ToString("N");
}

/// <summary>An immutable observation of one provider request, independent of log delivery or projection.</summary>
[MessagePackObject]
public sealed record MarketDataDownloadOutcome
{
    [Key(0)] public short SchemaVersion { get; init; } = 1;
    [Key(1)] public MarketDataDownloadDataset Dataset { get; init; }
    [Key(2)] public string Provider { get; init; } = "FMP";
    [Key(3)] public DateOnly ValueDate { get; init; }
    [Key(4)] public string Scope { get; init; } = "ALL";
    [Key(5)] public Guid ImportCommandId { get; init; }
    [Key(6)] public Guid SourceTerminalEventId { get; init; }
    [Key(7)] public DateTime RequestedAtUtc { get; init; }
    [Key(8)] public DateTime StartedAtUtc { get; init; }
    [Key(9)] public DateTime FinishedAtUtc { get; init; }
    [Key(10)] public MarketDataDownloadStatus Status { get; init; }
    [Key(11)] public long? DownloadedRecordCount { get; init; }
    [Key(12)] public long? PersistedRecordCount { get; init; }
    [Key(13)] public long ElapsedMilliseconds { get; init; }
    [Key(14)] public string? ErrorCode { get; init; }
    [Key(15)] public string? ErrorMessage { get; init; }

    public static DateTime MillisecondUtc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc) throw new ArgumentException("Download timestamps must be UTC.");
        return new DateTime(value.Ticks - value.Ticks % TimeSpan.TicksPerMillisecond, DateTimeKind.Utc);
    }

    public static string CanonicalScope(IEnumerable<string>? countries)
    {
        var codes = countries?.Select(c => c.Trim().ToUpperInvariant()).Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal).ToArray() ?? [];
        if (codes.Any(c => c == "ALL" || c.Length is < 2 or > 3 || c.Any(x => !char.IsAsciiLetter(x))))
            throw new ArgumentException("Country scope must contain two- or three-letter country codes.");
        return codes.Length == 0 ? "ALL" : string.Join(",", codes);
    }

    public void Validate()
    {
        if (SchemaVersion != 1 || Dataset is not (MarketDataDownloadDataset.EconomicCalendar or MarketDataDownloadDataset.TreasuryCurve)
            || Status is not (MarketDataDownloadStatus.Completed or MarketDataDownloadStatus.Failed))
            throw new ArgumentException("Unsupported download schema, dataset or terminal status.");
        if (Provider != "FMP" || ValueDate == default || string.IsNullOrWhiteSpace(Scope)
            || Scope != CanonicalScope(Scope == "ALL" ? [] : Scope.Split(','))
            || Dataset == MarketDataDownloadDataset.TreasuryCurve && Scope != "US")
            throw new ArgumentException("Invalid download partition.");
        if (ImportCommandId == Guid.Empty || SourceTerminalEventId == Guid.Empty)
            throw new ArgumentException("Import and terminal identities are required.");
        foreach (var time in new[] { RequestedAtUtc, StartedAtUtc, FinishedAtUtc })
            if (time == default || time != MillisecondUtc(time)) throw new ArgumentException("Timestamps require UTC millisecond precision.");
        if (RequestedAtUtc > StartedAtUtc || StartedAtUtc > FinishedAtUtc || ElapsedMilliseconds < 0
            || DownloadedRecordCount < 0 || PersistedRecordCount < 0)
            throw new ArgumentException("Invalid download timing or record counts.");
        if (Status == MarketDataDownloadStatus.Completed
            && (DownloadedRecordCount is null || PersistedRecordCount is null || ErrorCode is not null || ErrorMessage is not null))
            throw new ArgumentException("Completed downloads require known counts and no error.");
        if (Status == MarketDataDownloadStatus.Failed
            && (string.IsNullOrWhiteSpace(ErrorCode) || string.IsNullOrWhiteSpace(ErrorMessage)))
            throw new ArgumentException("Failed downloads require diagnostic information.");
        if (ErrorCode?.Length > 128 || ErrorMessage?.Length > 512)
            throw new ArgumentException("Download diagnostic exceeds the bounded length.");
    }

    public string ComputeHash()
    {
        Validate();
        return Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(this)));
    }

    public static Guid LoggingCommandId(Guid importCommandId)
        => new(SHA256.HashData(Encoding.UTF8.GetBytes($"IFM.DownloadLog.v1:{importCommandId:N}")).AsSpan(0, 16));
}
