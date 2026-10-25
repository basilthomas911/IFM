using MessagePack;

namespace TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

/// <summary>Daily-loss monitoring fields. Missing risk values are null; existing wire keys remain unchanged.</summary>
public sealed partial record IronCondorTradePlanSnapshot
{
    /// <summary>Gets UTC time this daily-risk snapshot was calculated.</summary>
    [Key(53)] public DateTime? ActionDateTime { get; init; }
    /// <summary>Gets current session PnL in Currency, with commission basis captured by the calculation version.</summary>
    [Key(54)] public decimal? DailyPnl { get; init; }
    /// <summary>Gets max(0, -DailyPnl), in currency.</summary>
    [Key(55)] public decimal? DailyLoss { get; init; }
    /// <summary>Gets positive configured daily currency loss threshold.</summary>
    [Key(56)] public decimal? DailyLossLimit { get; init; }
    /// <summary>Gets dailyLossLimit minus DailyLoss, in currency.</summary>
    [Key(57)] public decimal? LossHeadroom { get; init; }
    /// <summary>Gets dailyPnl plus projected position value change minus additional exit costs.</summary>
    [Key(58)] public decimal? ForwardDailyPnl { get; init; }
    /// <summary>Gets max(0, -ForwardDailyPnl), in currency.</summary>
    [Key(59)] public decimal? ForwardLoss { get; init; }
    /// <summary>Gets dailyLossLimit minus ForwardLoss, in currency.</summary>
    [Key(60)] public decimal? ForwardLossHeadroom { get; init; }
    /// <summary>Gets signed forward minus current position valuation, in currency.</summary>
    [Key(61)] public decimal? ProjectedPositionValueChange { get; init; }
    /// <summary>Gets additional closing commissions, in currency.</summary>
    [Key(62)] public decimal? EstimatedExitCommission { get; init; }
    /// <summary>Gets additional closing slippage not already included in valuation prices, in currency.</summary>
    [Key(63)] public decimal? EstimatedExitSlippage { get; init; }
    /// <summary>Gets additional closing commissions plus slippage, in currency.</summary>
    [Key(64)] public decimal? EstimatedExitCosts { get; init; }
    /// <summary>Gets signed current marked put spread price per strategy unit, in points.</summary>
    [Key(65)] public decimal? PutSpreadPrice { get; init; }
    /// <summary>Gets signed current marked call spread price per strategy unit, in points.</summary>
    [Key(66)] public decimal? CallSpreadPrice { get; init; }
    /// <summary>Gets signed sum of current put and call spread prices per strategy unit.</summary>
    [Key(67)] public decimal? CombinedSpreadPrice { get; init; }
    /// <summary>Gets signed put spread forward value in currency, including quantity and the validated contract multiplier.</summary>
    [Key(68)] public decimal? PutForwardPrice { get; init; }
    /// <summary>Gets signed call spread forward value in currency, including quantity and the validated contract multiplier.</summary>
    [Key(69)] public decimal? CallForwardPrice { get; init; }
    /// <summary>Gets combined forward points per strategy unit: raw closing cost for entry-credit v2, signed value for older scenario models.</summary>
    [Key(70)] public decimal? CombinedForwardPrice { get; init; }
    /// <summary>Gets forward spread points on the same signed holding-value basis as CombinedSpreadPrice.</summary>
    /// <remarks>The entry-credit calculation stores a positive closing cost; only this chart projection negates it.
    /// Historical scenario models already used signed position values and retain their recorded convention.</remarks>
    [IgnoreMember] public decimal? ForwardSpreadForChart => CalculationVersion == TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position.IronCondor.ForwardPrice.IronCondorForwardRisk.EntryCreditCalculationVersion
        ? -CombinedForwardPrice : CombinedForwardPrice;
    /// <summary>Gets signed current position valuation in currency.</summary>
    [Key(71)] public decimal? CurrentPositionValue { get; init; }
    /// <summary>Gets signed scenario position valuation in currency.</summary>
    [Key(72)] public decimal? ForwardPositionValue { get; init; }
    /// <summary>Gets signed valuation using executable closing quotes, in currency.</summary>
    [Key(73)] public decimal? EstimatedCloseValue { get; init; }
    /// <summary>Gets signed current delta per strategy unit.</summary>
    [Key(74)] public double? NetDelta { get; init; }
    /// <summary>Gets signed current gamma per strategy unit per underlying point.</summary>
    [Key(75)] public double? NetGamma { get; init; }
    /// <summary>Gets signed current vega per strategy unit per one percentage point IV change.</summary>
    [Key(76)] public double? NetVega { get; init; }
    /// <summary>Gets signed current theta per strategy unit per calendar day.</summary>
    [Key(77)] public double? NetTheta { get; init; }
    /// <summary>Gets current underlying futures price in points.</summary>
    [Key(78)] public decimal? UnderlyingPrice { get; init; }
    /// <summary>Gets underlying price minus short put strike, in points.</summary>
    [Key(79)] public decimal? DistanceToShortPut { get; init; }
    /// <summary>Gets short call strike minus underlying price, in points.</summary>
    [Key(80)] public decimal? DistanceToShortCall { get; init; }
    /// <summary>Gets remaining time to expiry in fractional calendar days.</summary>
    [Key(81)] public double? TimeToExpiryDays { get; init; }
    /// <summary>Gets projection horizon in seconds.</summary>
    [Key(82)] public long? ScenarioHorizonSeconds { get; init; }
    /// <summary>Gets underlying futures price used for the captured forward scenario.</summary>
    [Key(83)] public decimal? ScenarioUnderlyingPrice { get; init; }
    /// <summary>Gets scenario underlying price minus current underlying price.</summary>
    [Key(84)] public decimal? ScenarioUnderlyingMove { get; init; }
    /// <summary>Gets scenario IV shift in percentage points.</summary>
    [Key(85)] public double? ScenarioVolatilityShift { get; init; }
    /// <summary>Gets identity of the selected adverse price and volatility scenario.</summary>
    [Key(86)] public string? ScenarioKind { get; init; }
    /// <summary>Gets optionCalculator model and numerical policy version.</summary>
    [Key(87)] public string? PricingModelVersion { get; init; }
    /// <summary>Gets oldest quote time across all four legs.</summary>
    [Key(88)] public DateTime? OldestQuoteAtUtc { get; init; }
    /// <summary>Gets earliest expiry of the captured risk inputs.</summary>
    [Key(89)] public DateTime? RiskValidUntilUtc { get; init; }
    /// <summary>Gets complete, unavailable, stale or failed daily-risk calculation.</summary>
    [Key(90)] public string? CalculationStatus { get; init; }
    /// <summary>Gets daily-loss exit recommendation; null means unavailable.</summary>
    [Key(91)] public bool? ExitRecommended { get; init; }
    /// <summary>Gets business reason for the daily-loss recommendation.</summary>
    [Key(92)] public string? ExitReason { get; init; }
    /// <summary>Gets number of complete strategy units.</summary>
    [Key(93)] public int? Quantity { get; init; }
    /// <summary>Gets currency value of one option price point.</summary>
    [Key(94)] public decimal? ContractMultiplier { get; init; }
    /// <summary>Gets currency shared by all loss, PnL and valuation amounts.</summary>
    [Key(95)] public string? Currency { get; init; }
    /// <summary>Gets the four frozen quote, price and Greek observations used by this snapshot.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [IgnoreMember, System.Text.Json.Serialization.JsonIgnore, Newtonsoft.Json.JsonIgnore]
    public IronCondorTradePlanLegObservation[] Legs => Position.Legs.Select(StrategyPositionLegOptionData.Observe).ToArray();
    /// <summary>Reserved wire slot: reads old snapshots into Position.Legs; new snapshots write no duplicate legs.</summary>
    [Key(96), System.Text.Json.Serialization.JsonIgnore, Newtonsoft.Json.JsonIgnore]
    public IronCondorTradePlanLegObservation[] LegacyLegs
    {
        get => [];
        init
        {
            if (value is not { Length: > 0 }) return;
            Position = Position with { Legs = Position.Legs.Select(leg => leg with
            {
                Scenario = leg.Scenario ?? value.FirstOrDefault(old => old.TradeLegId == leg.TradeLegId && old.ContractId == leg.ContractId)
            }).ToArray() };
        }
    }
    /// <summary>Gets the exact versioned risk policy used by this snapshot.</summary>
    [Key(97)] public TomasAI.IFM.Domain.Trade.Shared.Strategy.Risk.StrategyRiskParameterSet? StrategyRiskParameterSet { get; init; }
    /// <summary>Gets the canonical policy hash for input provenance.</summary>
    [Key(98)] public string? RiskParameterSetHash { get; init; }
    /// <summary>Current spread and position values use retained position marks, not theoretical prices.</summary>
    [Key(99)] public string? CurrentPriceBasis { get; init; }
    /// <summary>Current numeric price of the configured volatility instrument (VX for now).</summary>
    [Key(100)] public decimal? VolatilityPrice { get; init; }
    /// <summary>Versioned signed total-forward-value risk; existing positive-loss wire members retain their historical meaning.</summary>
    [Key(101)] public TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position.IronCondor.ForwardPrice.IronCondorForwardRisk? IronCondorForwardRisk { get; init; }
    /// <summary>Gets the forward-price calculation error; null identifies a calculation without this error.</summary>
    /// <remarks>A zero forward price with this message is a failed-calculation fallback, not a successful zero-risk result.</remarks>
    [Key(102)] public string? ForwardPriceCalculationError { get; init; }
}
