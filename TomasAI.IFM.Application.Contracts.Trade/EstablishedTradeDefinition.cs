using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

namespace TomasAI.IFM.Domain.Trade.Shared;

/// <summary>Durable established trade created from accepted execution evidence.</summary>
[MessagePackObject]
public sealed record EstablishedTradeDefinition
{
    [Key(0)] public ushort SchemaVersion { get; init; } = 4;
    [Key(1)] public TradeEntityId Id { get; init; }
    [Key(2)] public TradeAssetFamily AssetFamily { get; init; }
    [Key(3)] public TradeStrategyKind StrategyKind { get; init; }
    [Key(4)] public Guid SourceComponentId { get; init; }
    [Key(5)] public Guid ExecutionAttemptId { get; init; }
    [Key(6)] public EstablishedTradeStatus Status { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(7)] public TradeLegDefinition[] Legs { get; init; } = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(8)] public ExecutionFillEvidence[] OriginalFills { get; init; } = [];
    [Key(9)] public decimal OpeningValue { get; init; }
    [Key(10)] public decimal OpeningCommission { get; init; }
    [Key(11)] public DateTime EstablishedAtUtc { get; init; }
    [Key(12)] public int EvidenceRevision { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(13)] public ExecutionFillEvidence[] ClosingFills { get; init; } = [];
    [Key(14)] public DateTime? ClosedAtUtc { get; init; }
    /// <summary>Portfolio-approved risk amounts finalized using actual opening fills; absent on historical events.</summary>
    [Key(15)] public EstablishedTradeMonitoringLimits? MonitoringLimits { get; init; }
    /// <summary>Query-enriched execution-backed opening snapshot, never synthesized in the frontend.</summary>
    [Key(16)] public StrategyPositionSnapshot? OpeningPositionSnapshot { get; init; }
    [Key(17)] public StrategyPositionHistoryValuation? OpeningHistoryValuation { get; init; }
    /// <summary>The execution-derived futures session date; no date is assigned before accepted fills establish the trade.</summary>
    [IgnoreMember] public DateOnly? TradeDate => EstablishedAtUtc == default ? null
        : TomasAI.IFM.Domain.MarketData.Shared.FuturesTradingValueDate.GetOperational(new DateTimeOffset(EstablishedAtUtc));
    /// <summary>The trade matures on the latest leg expiry; an incomplete contract definition has no maturity.</summary>
    [IgnoreMember] public DateOnly? MaturityDate => Legs.Length == 0 || Legs.Any(leg => !leg.Expiry.HasValue)
        ? null : Legs.Max(leg => leg.Expiry!.Value);
}
