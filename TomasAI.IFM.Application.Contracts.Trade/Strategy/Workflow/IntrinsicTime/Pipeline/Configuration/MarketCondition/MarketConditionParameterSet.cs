using FluentValidation;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition;

/// <summary>Complete immutable V1 Market Condition configuration.</summary>
[MessagePackObject]
public sealed record MarketConditionParameterSet
{
    public const ushort CurrentSchemaVersion = 1;
    [Key(0)] public Guid ParameterSetId { get; init; }
    [Key(1)] public int Version { get; init; }
    [Key(2)] public ushort SchemaVersion { get; init; } = CurrentSchemaVersion;
    [Key(3)] public Guid StrategyParameterSetId { get; init; }
    [Key(4)] public int StrategyParameterSetVersion { get; init; }
    [Key(5)] public int FundId { get; init; }
    [Key(6)] public string InstrumentRoot { get; init; } = "ES";
    [Key(7)] public TimeFrameType TargetHorizon { get; init; }
    [Key(8)] public MarketConditionSnapshotConfiguration Snapshot { get; init; } = new();
    [Key(9)] public MarketConditionSessionConfiguration Session { get; init; } = new();
    [Key(10)] public MarketConditionEventRiskConfiguration EventRisk { get; init; } = new();
    [Key(11)] public MarketConditionMarketIntegrityConfiguration MarketIntegrity { get; init; } = new();
    [Key(12)] public MarketConditionFuturesLiquidityConfiguration FuturesLiquidity { get; init; } = new();
    [Key(13)] public MarketConditionOptionLiquidityConfiguration OptionLiquidity { get; init; } = new();
    [Key(14)] public MarketConditionOperationalReadinessConfiguration OperationalReadiness { get; init; } = new();
    [Key(15)] public MarketConditionWorkflowEligibilityConfiguration WorkflowEligibility { get; init; } = new();
    [Key(16)] public MarketConditionClassificationConfiguration Classification { get; init; } = new();
    [Key(17)] public MarketConditionScoringConfiguration Scoring { get; init; } = new();
    [Key(18)] public MarketConditionExecutionConfiguration Execution { get; init; } = new();
    [Key(19)] public ushort SummaryTemplateVersion { get; init; } = 1;

    public static MarketConditionParameterSet CreateDefault(
        Guid parameterSetId,
        Guid strategyParameterSetId,
        int fundId,
        TimeFrameType targetHorizon,
        int version = 1,
        int strategyVersion = 1)
    {
        var horizon = targetHorizon switch
        {
            TimeFrameType.Daily => (1, 14, 55m, 0.65m, 30),
            TimeFrameType.Weekly => (7, 45, 60m, 0.68m, 60),
            TimeFrameType.Monthly => (21, 90, 65m, 0.70m, 90),
            _ => throw new ArgumentOutOfRangeException(nameof(targetHorizon))
        };
        return new MarketConditionParameterSet
        {
            ParameterSetId = parameterSetId,
            Version = version,
            StrategyParameterSetId = strategyParameterSetId,
            StrategyParameterSetVersion = strategyVersion,
            FundId = fundId,
            TargetHorizon = targetHorizon,
            OptionLiquidity = new MarketConditionOptionLiquidityConfiguration
            { MinimumDte = horizon.Item1, MaximumDte = horizon.Item2 },
            Scoring = new MarketConditionScoringConfiguration
            { MinimumStrength = horizon.Item3, MinimumConfidence = horizon.Item4 },
            Execution = new MarketConditionExecutionConfiguration { ResultLifetimeSeconds = horizon.Item5 }
        };
    }
}
