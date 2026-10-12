using FluentValidation;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVxTermStructureSignal;

/// <summary>Represents a calculated front/back VX futures term structure.</summary>
[MessagePackObject]
public sealed record FuturesVxTermStructureSignalReadModel
{
    [Key(0)] public DateOnly ValueDate { get; init; }
    [Key(1)] public string ConfigurationId { get; init; } = string.Empty;
    [Key(2)] public string FrontVxContractId { get; init; } = string.Empty;
    [Key(3)] public DateOnly FrontExpiry { get; init; }
    [Key(4)] public decimal FrontVxPrice { get; init; }
    [Key(5)] public string BackVxContractId { get; init; } = string.Empty;
    [Key(6)] public DateOnly BackExpiry { get; init; }
    [Key(7)] public decimal BackVxPrice { get; init; }
    [Key(8)] public decimal FrontBackSpread { get; init; }
    [Key(9)] public decimal FrontBackRatio { get; init; }
    [Key(10)] public decimal TermStructurePercent { get; init; }
    [Key(11)] public FuturesVxTermStructureState TermStructureState { get; init; }
    [Key(12)] public decimal? PriorFrontBackRatio { get; init; }
    [Key(13)] public decimal? PriorTermStructurePercent { get; init; }
    [Key(14)] public DateTimeOffset FrontSourceTimestampUtc { get; init; }
    [Key(15)] public DateTimeOffset BackSourceTimestampUtc { get; init; }
    [Key(16)] public long FrontSourceSequence { get; init; }
    [Key(17)] public long BackSourceSequence { get; init; }
    [Key(18)] public DateTimeOffset CalculatedAtUtc { get; init; }
    [Key(19)] public bool IsWarm { get; init; }
    [Key(20)] public bool IsValid { get; init; }
    [Key(21)] public int SchemaVersion { get; init; } = 1;
    [Key(22)] public string CalculationVersion { get; init; } = "vx-term-structure-v1";
}
