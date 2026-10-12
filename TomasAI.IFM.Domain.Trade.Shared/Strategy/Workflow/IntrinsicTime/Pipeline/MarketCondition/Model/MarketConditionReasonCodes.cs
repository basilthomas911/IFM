namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;

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
    public const string HintFuturesDaily = "MC.HINT.FUTURES.DAILY";
    public const string HintVerticalSpreadWeekly = "MC.HINT.VERTICAL_SPREAD.WEEKLY";
    public const string HintIronCondorMonthly = "MC.HINT.IRON_CONDOR.MONTHLY";
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
