using System.Collections.Concurrent;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Runtime;

/// <summary>Validated configuration generations published exclusively by background preparation.</summary>
public sealed class WorkflowPreparedConfigurationCache
{
    sealed record Assessment(long Generation, DateTime CapturedAtUtc, DateTime ValidUntilUtc, MarketConditionAssessmentBinding Binding);
    readonly ConcurrentDictionary<(string Profile, TimeFrameType Horizon), Assessment> assessments = new();
    readonly ConcurrentDictionary<TimeFrameType, (long Generation, DateTime Captured, ResolvedRegimeDiscoveryParameterSet Value)> regimes = new();

    public void PublishRegime(TimeFrameType horizon, ResolvedRegimeDiscoveryParameterSet resolved, long generation, DateTime atUtc)
    {
        if (resolved.ParameterSet.TargetHorizon != horizon || resolved.EffectiveFromUtc > atUtc
            || new RegimeDiscoveryParameterSetValidationRules().Execute(resolved.ParameterSet).Length != 0
            || !string.Equals(RegimeDiscoveryParameterPayload.ComputeSha256(resolved.ParameterSet), resolved.PayloadSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("RD.CONFIG.PUBLICATION_INVALID");
        regimes[horizon] = (generation, atUtc, resolved);
    }

    public bool TryAcquireRegime(TimeFrameType horizon, DateTime atUtc, out ResolvedRegimeDiscoveryParameterSet? resolved)
    {
        resolved = null;
        if (!regimes.TryGetValue(horizon, out var current) || current.Generation != ReadCacheInvalidation.Configuration
            || atUtc < current.Captured || atUtc >= current.Captured.AddMinutes(1)) return false;
        resolved = current.Value;
        return true;
    }

    public void Publish(string profile, TimeFrameType horizon, ResolvedMarketConditionAssessmentParameterSet resolved,
        long generation, DateTime atUtc)
    {
        var binding = new MarketConditionAssessmentBinding { Parameters = resolved.ParameterSet, PayloadSha256 = resolved.PayloadSha256 };
        binding.Validate();
        if (resolved.Status != TomasAI.IFM.Domain.Strategy.Contracts.Shared.Configuration.ConfigurationParameterSetStatus.Published
            || binding.Parameters.TargetHorizon != horizon || binding.Parameters.MarketProfileId != profile
            || resolved.EffectiveFromUtc > atUtc) throw new InvalidOperationException("MC.CONFIG.PUBLICATION_SCOPE");
        assessments[(profile, horizon)] = new(generation, atUtc, atUtc.AddMinutes(1), binding);
    }

    public bool TryAcquire(string profile, TimeFrameType horizon, DateTime atUtc, out MarketConditionAssessmentBinding? binding)
    {
        binding = null;
        if (!assessments.TryGetValue((profile, horizon), out var current) || current.Generation != ReadCacheInvalidation.Configuration
            || atUtc < current.CapturedAtUtc || atUtc >= current.ValidUntilUtc) return false;
        binding = current.Binding;
        return true;
    }
}
