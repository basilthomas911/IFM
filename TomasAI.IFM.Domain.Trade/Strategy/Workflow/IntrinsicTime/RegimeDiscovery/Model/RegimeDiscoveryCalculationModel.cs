using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Model;

/// <summary>Calculates one pure Regime Discovery outcome from an immutable input.</summary>
public interface IRegimeDiscoveryCalculationModel
{
    /// <summary>Calculates a result without owning or mutating durable actor state.</summary>
    Task<RegimeDiscoveryResult> CalculateAsync(
        RegimeDiscoveryCalculationInput input,
        RegimeDiscoveryExecutionMode mode = RegimeDiscoveryExecutionMode.Sequential,
        CancellationToken cancellationToken = default);
}

/// <summary>Coordinates the three independent specialists and deterministic Fusion.</summary>
public sealed class RegimeDiscoveryCalculationModel(
    TrendRegimeCalculationModel? trendModel = null,
    VolatilityRegimeCalculationModel? volatilityModel = null,
    MarketStructureRegimeCalculationModel? marketStructureModel = null,
    MarketRegimeFusionModel? fusionModel = null) : IRegimeDiscoveryCalculationModel
{
    readonly TrendRegimeCalculationModel trend = trendModel ?? new();
    readonly VolatilityRegimeCalculationModel volatility = volatilityModel ?? new();
    readonly MarketStructureRegimeCalculationModel marketStructure = marketStructureModel ?? new();
    readonly MarketRegimeFusionModel fusion = fusionModel ?? new();

    /// <summary>Calculates one complete typed Regime Discovery result.</summary>
    /// <param name="input">Complete immutable calculation input.</param>
    /// <param name="mode">Specialist scheduling mode.</param>
    /// <param name="cancellationToken">Signals cancellation before a terminal calculation is created.</param>
    /// <returns>The complete typed result. Incomplete specialists produce an incomplete Fusion result.</returns>
    public async Task<RegimeDiscoveryResult> CalculateAsync(
        RegimeDiscoveryCalculationInput input,
        RegimeDiscoveryExecutionMode mode = RegimeDiscoveryExecutionMode.Sequential,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();
        TrendRegimeResult trendResult;
        VolatilityRegimeResult volatilityResult;
        MarketStructureRegimeResult structureResult;
        if (mode == RegimeDiscoveryExecutionMode.ThreadPoolParallel)
        {
            var trendTask = Task.Run(() => trend.Calculate(input), cancellationToken);
            var volatilityTask = Task.Run(() => volatility.Calculate(input), cancellationToken);
            var structureTask = Task.Run(() => marketStructure.Calculate(input), cancellationToken);
            await Task.WhenAll(trendTask, volatilityTask, structureTask).ConfigureAwait(false);
            trendResult = await trendTask.ConfigureAwait(false);
            volatilityResult = await volatilityTask.ConfigureAwait(false);
            structureResult = await structureTask.ConfigureAwait(false);
        }
        else
        {
            trendResult = trend.Calculate(input);
            volatilityResult = volatility.Calculate(input);
            structureResult = marketStructure.Calculate(input);
        }
        cancellationToken.ThrowIfCancellationRequested();
        var fusionResult = fusion.Calculate(trendResult, volatilityResult, structureResult,
            input.ParameterSet.Fusion);
        var evidence = RegimeDiscoveryMath.OrderEvidence(
            trendResult.Evidence.Concat(volatilityResult.Evidence).Concat(structureResult.Evidence));
        var reasons = RegimeDiscoveryMath.OrderReasons(
            trendResult.Reasons.Concat(volatilityResult.Reasons)
                .Concat(structureResult.Reasons).Concat(fusionResult.Reasons));
        return new RegimeDiscoveryResult
        {
            ResultId = input.ResultId,
            WorkflowId = input.WorkflowId,
            StrategyParameterSetId = input.ParameterSet.StrategyParameterSetId,
            StrategyParameterSetVersion = input.ParameterSet.StrategyParameterSetVersion,
            RegimeDiscoveryParameterSetId = input.ParameterSet.ParameterSetId,
            RegimeDiscoveryParameterSetVersion = input.ParameterSet.Version,
            SignalSnapshotId = input.Snapshot.SnapshotId,
            EntityId = input.EntityId,
            TriggerEventId = input.TriggerEventId,
            MarketDataAsOfUtc = input.Snapshot.MarketDataAsOfUtc,
            ProducedAtUtc = input.ProducedAtUtc,
            TargetHorizon = input.ParameterSet.TargetHorizon,
            Trend = trendResult,
            Volatility = volatilityResult,
            MarketStructure = structureResult,
            Decision = fusionResult,
            SupportingEvidence = evidence,
            OverallQuality = fusionResult.Quality,
            OverallConfidence = fusionResult.Confidence,
            Reasons = reasons,
            SummaryText = Summary(input, trendResult, volatilityResult, structureResult, fusionResult)
        };
    }

    static string Summary(
        RegimeDiscoveryCalculationInput input,
        TrendRegimeResult trend,
        VolatilityRegimeResult volatility,
        MarketStructureRegimeResult structure,
        RegimeDiscoveryDecision fusion) =>
        fusion.IsComplete
            ? $"{input.ParameterSet.TargetHorizon}: {fusion.Direction}; Trend={trend.Direction}/{trend.Phase}; " +
              $"Volatility={volatility.Level}/{volatility.Change}; Structure={structure.Classification}; " +
              $"Confidence={fusion.Confidence:F6}; Quality={fusion.Quality}"
            : $"{input.ParameterSet.TargetHorizon}: Regime Discovery failed because one or more specialists were incomplete.";
}
