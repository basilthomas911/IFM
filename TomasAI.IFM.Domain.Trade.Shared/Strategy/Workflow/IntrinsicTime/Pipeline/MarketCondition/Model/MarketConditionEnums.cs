namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;

public enum MarketTradeability : byte { Undefined = 0, Tradeable = 1, NotTradeable = 2 }
public enum MarketConditionType : byte
{
    Undefined = 0, Directional = 1, RangeBound = 2, Transition = 3,
    VolatilityExpansion = 4, VolatilityContraction = 5, Dislocated = 6, NoOpportunity = 7
}
public enum MarketConditionDirection : byte { Undefined = 0, Bullish = 1, Bearish = 2, Neutral = 3 }
public enum MarketConditionPhase : byte
{
    Undefined = 0, Initiating = 1, Confirmed = 2, Continuing = 3,
    Weakening = 4, Exhausting = 5, Reversing = 6
}
public enum MarketConditionVolatilityBehavior : byte
    { Undefined = 0, Contracting = 1, Stable = 2, Expanding = 3, Shock = 4 }
public enum MarketConditionLiquidityQuality : byte { Unknown = 0, Healthy = 1, Degraded = 2, Unusable = 3 }
public enum MarketConditionDataQuality : byte { Unknown = 0, Healthy = 1, Degraded = 2, Unusable = 3 }
public enum MarketConditionUpstreamAlignment : byte { Unknown = 0, Aligned = 1, Neutral = 2, Conflict = 3 }
public enum MarketConditionFailureCategory : byte
{
    Undefined = 0, ContractInvalid = 1, ConfigurationUnavailable = 2, RequiredInputInvalid = 3,
    CalculationFailed = 4, InvariantViolation = 5, ProjectionFailed = 6, PersistenceFailed = 7, Timeout = 8
}
public enum MarketConditionEvidenceArea : byte
{
    Unknown = 0, Workflow = 1, Data = 2, Session = 3, EventRisk = 4, MarketIntegrity = 5,
    FuturesLiquidity = 6, OptionLiquidity = 7, Operations = 8, Classification = 9, Scoring = 10
}
public enum MarketSourceAvailability : byte { Unknown = 0, Available = 1, Unavailable = 2, Degraded = 3 }
public enum MarketSourceValidity : byte { Unknown = 0, Valid = 1, Invalid = 2 }
public enum MarketFreshnessState : byte { Unknown = 0, Fresh = 1, Stale = 2, Future = 3 }
public enum MarketSessionStatus : byte { Unknown = 0, Open = 1, Closed = 2 }
public enum MarketEventRiskStatus : byte { Unknown = 0, Clear = 1, Blocked = 2 }
public enum MarketOperationalStatus : byte { Unknown = 0, Healthy = 1, Degraded = 2, Unavailable = 3 }

public static class MarketConditionReasonCodes
{
    public const string DataFit = "MC.DATA.FIT";
    public const string DataStale = "MC.DATA.STALE";
    public const string OptionalMissing = "MC.DATA.OPTIONAL_MISSING";
    public const string DataUnfit = "MC.BLOCK.DATA_UNFIT";
    public const string Session = "MC.BLOCK.SESSION";
    public const string EventRisk = "MC.BLOCK.EVENT_RISK";
    public const string MarketDislocated = "MC.BLOCK.MARKET_DISLOCATED";
    public const string FuturesLiquidity = "MC.BLOCK.FUTURES_LIQUIDITY";
    public const string OptionLiquidity = "MC.BLOCK.OPTION_LIQUIDITY";
    public const string Operations = "MC.BLOCK.OPERATIONS";
    public const string WorkflowIneligible = "MC.BLOCK.WORKFLOW_INELIGIBLE";
    public const string RegimeNoNewTrade = "MC.BLOCK.REGIME_NO_NEW_TRADE";
    public const string RegimeTriggerConflict = "MC.BLOCK.REGIME_TRIGGER_CONFLICT";
    public const string Strength = "MC.NO_OPPORTUNITY.STRENGTH";
    public const string Confidence = "MC.NO_OPPORTUNITY.CONFIDENCE";
    public const string Directional = "MC.CONDITION.DIRECTIONAL";
    public const string RangeBound = "MC.CONDITION.RANGE_BOUND";
    public const string Transition = "MC.CONDITION.TRANSITION";
    public const string VolatilityExpansion = "MC.CONDITION.VOLATILITY_EXPANSION";
    public const string VolatilityContraction = "MC.CONDITION.VOLATILITY_CONTRACTION";
    public const string ContractInvalid = "MC.FAIL.CONTRACT_INVALID";
    public const string Configuration = "MC.FAIL.CONFIGURATION";
    public const string RequiredInput = "MC.FAIL.REQUIRED_INPUT";
    public const string Calculation = "MC.FAIL.CALCULATION";
    public const string Invariant = "MC.FAIL.INVARIANT";
    public const string Projection = "MC.FAIL.PROJECTION";
    public const string Persistence = "MC.FAIL.PERSISTENCE";
    public const string Timeout = "MC.FAIL.TIMEOUT";
    public const string ResultExpired = "MC.RESULT.EXPIRED";
}
