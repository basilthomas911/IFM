using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

[MessagePackObject]
public sealed record GetFundPerformanceMetricsRequest(
    [property: Key(0)] DateOnly From, [property: Key(1)] DateOnly Through);

/// <summary>Server ledger metrics. Ratios are fractions; amounts are in Currency.</summary>
[MessagePackObject]
public sealed record FundPerformanceMetrics
{
    [Key(0)] public bool HasHistory { get; init; }
    [Key(1)] public decimal WinRate { get; init; }
    [Key(2)] public decimal AverageProfit { get; init; }
    [Key(3)] public decimal LossRate { get; init; }
    [Key(4)] public decimal AverageLoss { get; init; }
    [Key(5)] public decimal WinLossRatio { get; init; }
    [Key(6)] public decimal ActualSharpeRatio { get; init; }
    [Key(7)] public decimal PnlAmount { get; init; }
    [Key(8)] public decimal PnlPercent { get; init; }
    [Key(9)] public decimal TradeCommission { get; init; }
    [Key(10)] public decimal? MaximumDrawdownPercent { get; init; }
    [Key(11)] public decimal? MaximumDrawdownAmount { get; init; }
    [Key(12)] public DateOnly From { get; init; }
    [Key(13)] public DateOnly Through { get; init; }
    [Key(14)] public string Currency { get; init; } = "USD";
    [Key(15)] public string CalculationBasis { get; init; } = "";
}

[MessagePackObject(AllowPrivate = true)]
public sealed record GetFundPerformanceMetricsQuery : IFinancialQueryMessage<GetFundPerformanceMetricsRequest, FundPerformanceMetrics>
{
    public const string Actor = "GeneralLedgerQuery";
    public const string Verb = "GetFundPerformanceMetrics";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public LedgerPortfolioId EntityId { get; init; } = new(0);
    [Key(2)] public GetFundPerformanceMetricsRequest Parameters { get; init; } = new(default, default);
    [Key(3)] public FinancialReadScope Scope { get; init; } = new();
    [Key(4)] public Guid CorrelationId { get; init; }
    [Key(5)] public DateTime RequestedAtUtc { get; init; }
    [IgnoreMember] public int SchemaVersion => 2;
    [IgnoreMember] public LedgerPortfolioId QueryEntityId => EntityId;
    [IgnoreMember] public int ErrorCode => FinancialReasons.PersistenceFailed;
    [IgnoreMember] public string? QueryParams => EntityId.Format();
    [IgnoreMember] IActorEntityId IQuery.EntityId => EntityId;
}
