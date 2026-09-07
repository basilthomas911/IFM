using MessagePack;
using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;

/// <summary>Frozen evidence of calendar coverage, distinct from the time the coverage was checked.</summary>
[MessagePackObject]
public sealed record MarketConditionCalendarDownloadEvidence
{
    MarketDataDownloadLogReadModel[] _attempts = [];
    [Key(0)] public string PolicyVersion { get; init; } = "FMP.CalendarCoverage.v1";
    [Key(1)] public DateTime CheckedAtUtc { get; init; }
    [Key(2)] public DateOnly FromDate { get; init; }
    [Key(3)] public DateOnly ToDate { get; init; }
    [Key(4)] public string Country { get; init; } = "US";
    [Key(5)] public int MaximumDownloadAgeSeconds { get; init; } = 86400;
    [Key(6)] public bool CoverageConfirmed { get; init; }
    [Key(7)] public string Reason { get; init; } = string.Empty;
    [Key(8)] public DateTime? ValidUntilUtc { get; init; }
    [Key(9)] public MarketDataDownloadLogReadModel[] Attempts
    {
        get => [.. _attempts];
        init => _attempts = value is null ? [] : [.. value];
    }
}
