using TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using static TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.CompositionRulesContract;
namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;
/// <summary>Applies pinned global cache parameters to frozen composition inputs without looking up current configuration.</summary>
public static class StrategyOptionChainCompositionPolicy
{
    /// <summary>Returns the frozen global price constraints, never a current policy lookup.</summary>
    public static OptionStrategyBiasParameters? Row(ExecuteOrderCompositionPipelineCommand request)
    {
        if (request.MarketSnapshot.StrategyOptionChainParametersJson is not { } json) return null;
        var policy = StrategyOptionChainParameterSet.Read(json);
        var bias = request.CompositionBinding.Selected.Bias == "Balanced" ? "Neutral" : request.CompositionBinding.Selected.Bias;
        return policy.BiasRows.Single(x => x.MarketBias.ToString() == bias);
    }

    /// <summary>Uses accepted bias delta and DTE targets; catalog hard bounds and narrower quality limits remain binding.</summary>
    public static CompositionResolvedParameters Apply(ExecuteOrderCompositionPipelineCommand request, CompositionVariantRules rule,
        CompositionResolvedParameters resolved)
    {
        var selected = request.CompositionBinding.Selected;
        var outer = SelectionConstructionPolicy.Read(TradeSelectionContracts.Policy(request.SelectionBinding, selected.CompositionPolicyReference).PayloadJson);
        if (outer.CachePolicy(selected.StructureKey) is not { } pinned) return resolved;
        var policy = StrategyOptionChainParameterSet.Read(request.MarketSnapshot.StrategyOptionChainParametersJson
            ?? throw new CompositionException("OC.CONFIG.CACHE_POLICY_MISSING"));
        var bias = selected.Bias == "Balanced" ? "Neutral" : selected.Bias;
        Require(policy.Enabled && policy.ParameterSetId == pinned.ParameterSetId && policy.Version == pinned.Version
            && policy.Hash() == pinned.ConfigurationDigest && policy.StrategyDefinitionId == selected.StructureKey.Id
            && policy.StrategyDefinitionVersion == selected.StructureKey.Version && policy.InstrumentRoot == selected.Product.Symbol
            && request.MarketSnapshot.StrategyOptionChainBias == bias, "OC.CONFIG.CACHE_POLICY_MISMATCH");
        var row = policy.BiasRows.Single(x => x.MarketBias.ToString() == bias);
        Require(row.Enabled && rule.AllowedWidths.All(w => request.CompositionBinding.BuilderCode switch
            { "PutVertical" => row.PutWingWidths.Contains(w), "CallVertical" => row.CallWingWidths.Contains(w),
                _ => row.PutWingWidths.Contains(w) && row.CallWingWidths.Contains(w) }),
            "OC.CONFIG.CACHE_WIDTH_MISMATCH");
        var before = resolved.Values;
        var p = before with
        {
            MinimumDaysToExpiry = Math.Max(before.MinimumDaysToExpiry, row.MinimumDte),
            MaximumDaysToExpiry = Math.Min(before.MaximumDaysToExpiry, row.MaximumDte),
            TargetDaysToExpiry = row.PreferredDte, TargetPutDelta = row.PutDelta.Target, TargetCallDelta = row.CallDelta.Target,
            TargetLegDelta = request.CompositionBinding.BuilderCode == "PutVertical" ? row.PutDelta.Target : row.CallDelta.Target,
            LegDeltaTolerance = Math.Min(before.LegDeltaTolerance, new[] { row.PutDelta.Target - row.PutDelta.Minimum,
                row.PutDelta.Maximum - row.PutDelta.Target, row.CallDelta.Target - row.CallDelta.Minimum, row.CallDelta.Maximum - row.CallDelta.Target }.Min()),
            MaximumQuoteAgeMilliseconds = Math.Min(before.MaximumQuoteAgeMilliseconds, row.MaximumQuoteAgeMilliseconds),
            MaximumQuoteSkewMilliseconds = Math.Min(before.MaximumQuoteSkewMilliseconds, row.MaximumQuoteSkewMilliseconds),
            MinimumDisplayedSize = Math.Max(before.MinimumDisplayedSize, row.MinimumQuoteSize),
            TargetNetDelta = row.TargetNetDelta, BalanceTolerance = Math.Min(before.BalanceTolerance, row.NetDeltaTolerance)
        };
        Validate(p);
        var evidence = resolved.Evidence.ToBuilder();
        foreach (var bound in rule.HardBounds)
        {
            var value = CompositionParameterResolver.Read(p, bound.Parameter);
            Require(value >= bound.Minimum && value <= bound.Maximum && value % bound.Grid == 0, "OC.CONFIG.CACHE_BOUND_MISMATCH");
        }
        foreach (var parameter in Enum.GetValues<CompositionParameter>())
        {
            var prior = CompositionParameterResolver.Read(before, parameter);
            var value = CompositionParameterResolver.Read(p, parameter);
            if (prior != value) evidence.Add(new() { Code = "GlobalChainPolicy:" + policy.ParameterSetId + "/" + policy.Version,
                Parameter = parameter, Before = prior, Unclamped = value, After = value, Status = "Applied" });
        }
        var result = resolved with { Values = p, Evidence = evidence.ToImmutable(), Hash = "" };
        return result with { Hash = CompositionHash.Compute(result) };
    }
}
