using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.HistoricalDataLoader;

/// <summary>Provides provider-neutral data load diagnostics to Query clients.</summary>
[MessagePackObject]
public sealed record FuturesAnalyticsHistoricalDataLoaderDiagnosticsReadModel
{
    /// <summary>Gets the data load attempt identity.</summary>
    [Key(0)] public Guid DataLoadAttemptId { get; init; }
    /// <summary>Gets the stable request hash.</summary>
    [Key(1)] public string RequestSha256 { get; init; } = string.Empty;
    /// <summary>Gets the provider-neutral lifecycle status.</summary>
    [Key(2)] public string Status { get; init; } = string.Empty;
    /// <summary>Gets the manifest identity after completion.</summary>
    [Key(3)] public Guid? ManifestId { get; init; }
    /// <summary>Gets the last completed batch ordinal.</summary>
    [Key(4)] public int LastCompletedBatchOrdinal { get; init; }
    /// <summary>Gets the last completed record ordinal.</summary>
    [Key(5)] public long LastCompletedRecordOrdinal { get; init; }
    /// <summary>Gets the number of audited valid sessions.</summary>
    [Key(6)] public int ValidSessionCount { get; init; }
    /// <summary>Gets the number of audited gaps.</summary>
    [Key(7)] public int GapCount { get; init; }
    /// <summary>Gets the number of audited rolls.</summary>
    [Key(8)] public int RollCount { get; init; }
    /// <summary>Gets the sanitized terminal failure text.</summary>
    [Key(9)] public string ErrorMessage { get; init; } = string.Empty;
    /// <summary>Gets the last durable update time.</summary>
    [Key(10)] public DateTimeOffset UpdatedAtUtc { get; init; }
}
