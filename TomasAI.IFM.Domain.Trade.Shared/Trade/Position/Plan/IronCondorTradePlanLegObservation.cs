using MessagePack;

namespace TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

/// <summary>One frozen option-leg observation. Prices are points; IV is annual decimal; vega is per percentage point and theta per day.</summary>
[MessagePackObject]
public sealed record IronCondorTradePlanLegObservation
{
    /// <summary>Gets the captured TradeLegId observation; nullable values are unavailable when absent.</summary>
    [Key(0)] public Guid TradeLegId { get; init; }
    /// <summary>Gets the captured ContractId observation; nullable values are unavailable when absent.</summary>
    [Key(1)] public string? ContractId { get; init; }
    /// <summary>Gets the captured LegRole observation; nullable values are unavailable when absent.</summary>
    [Key(2)] public string? LegRole { get; init; }
    /// <summary>Gets the captured Strike observation; nullable values are unavailable when absent.</summary>
    [Key(3)] public decimal? Strike { get; init; }
    /// <summary>Gets the captured Expiry observation; nullable values are unavailable when absent.</summary>
    [Key(4)] public DateOnly? Expiry { get; init; }
    /// <summary>Gets the captured PutCall observation; nullable values are unavailable when absent.</summary>
    [Key(5)] public byte? PutCall { get; init; }
    /// <summary>Gets the captured SignedQuantity observation; nullable values are unavailable when absent.</summary>
    [Key(6)] public int? SignedQuantity { get; init; }
    /// <summary>Gets the captured BidPrice observation; nullable values are unavailable when absent.</summary>
    [Key(7)] public decimal? BidPrice { get; init; }
    /// <summary>Gets the captured AskPrice observation; nullable values are unavailable when absent.</summary>
    [Key(8)] public decimal? AskPrice { get; init; }
    /// <summary>Gets the captured MarkPrice observation; nullable values are unavailable when absent.</summary>
    [Key(9)] public decimal? MarkPrice { get; init; }
    /// <summary>Gets the captured TheoreticalPrice observation; nullable values are unavailable when absent.</summary>
    [Key(10)] public decimal? TheoreticalPrice { get; init; }
    /// <summary>Gets the captured ForwardPrice observation; nullable values are unavailable when absent.</summary>
    [Key(11)] public decimal? ForwardPrice { get; init; }
    /// <summary>Gets the captured ImpliedVolatility observation; nullable values are unavailable when absent.</summary>
    [Key(12)] public double? ImpliedVolatility { get; init; }
    /// <summary>Gets the captured Delta observation; nullable values are unavailable when absent.</summary>
    [Key(13)] public double? Delta { get; init; }
    /// <summary>Gets the captured Gamma observation; nullable values are unavailable when absent.</summary>
    [Key(14)] public double? Gamma { get; init; }
    /// <summary>Gets the captured Vega observation; nullable values are unavailable when absent.</summary>
    [Key(15)] public double? Vega { get; init; }
    /// <summary>Gets the captured Theta observation; nullable values are unavailable when absent.</summary>
    [Key(16)] public double? Theta { get; init; }
    /// <summary>Gets the captured ForwardDelta observation; nullable values are unavailable when absent.</summary>
    [Key(17)] public double? ForwardDelta { get; init; }
    /// <summary>Gets the captured QuoteAtUtc observation; nullable values are unavailable when absent.</summary>
    [Key(18)] public DateTime? QuoteAtUtc { get; init; }
    /// <summary>Gets the captured RiskCalculatedAtUtc observation; nullable values are unavailable when absent.</summary>
    [Key(19)] public DateTime? RiskCalculatedAtUtc { get; init; }
}
