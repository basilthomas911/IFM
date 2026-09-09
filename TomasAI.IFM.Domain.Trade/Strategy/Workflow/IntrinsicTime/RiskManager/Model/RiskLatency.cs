using System.Diagnostics.Metrics;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model;

/// <summary>Observe latency in non-live environments; retain strict age checks for production and unknown environments.
/// Uses the immutable execution environment, never the host environment, so replay has the same policy.</summary>
public static class RiskLatency
{
    static readonly Meter Meter = new("TomasAI.IFM.RiskManagement", "1.0");
    static readonly Histogram<double> CandidateAge = Meter.CreateHistogram<double>("risk.candidate.age", "ms");
    static readonly Histogram<double> WorkflowDuration = Meter.CreateHistogram<double>("risk.workflow.duration", "ms");
    static readonly Histogram<double> QuoteAge = Meter.CreateHistogram<double>("risk.quote.oldest_age", "ms");

    public static bool EnforcesCompositionAgeLimit(ExecuteOrderCompositionPipelineCommand input)
    {
        if (!input.SelectionBinding.PipelinePolicies.Any(x => x.Kind == TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog.CatalogPipelineParameterKind.RiskManagement)) return true;
        var deployment = input.SelectionBinding.CatalogDefinitions.Single(x => x.Key == input.CompositionBinding.Selected.DeploymentKey);
        var reference = deployment.PipelineParameters.SingleOrDefault(x => x.Kind == TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog.CatalogPipelineParameterKind.RiskManagement);
        if (reference is null) return true;
        var policy = input.SelectionBinding.PipelinePolicies.Single(x => x.Kind == reference.Kind && x.Id == reference.Id && x.Version == reference.Version);
        RiskUnitModel.Require(policy.PayloadSha256 == reference.Hash, "RM.CONFIG.INVALID");
        return EnforcesAgeLimit(TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement.RiskParameterSet.Read(policy.PayloadJson).Environment);
    }

    public static bool EnforcesAgeLimit(string environment) => environment is not ("Emulator" or "Development" or "Paper" or "Test");
    public static bool WithinAgeLimit(string environment, DateTime evaluatedAtUtc, DateTime observedAtUtc)
        => observedAtUtc <= evaluatedAtUtc && (!EnforcesAgeLimit(environment) || (evaluatedAtUtc - observedAtUtc).TotalMilliseconds <= 1000);

    public static RiskLatencyObservation Measure(ExecuteRiskManagementPipelineCommand input)
    {
        var candidate = input.CompositionResult.ReadCompositionResult().Candidate!;
        var ids = candidate.Legs.Select(x => x.InstrumentId).ToHashSet(StringComparer.Ordinal);
        var quotes = input.MarketSnapshot.Instruments.Where(x => ids.Contains(x.Instrument.ContractId))
            .SelectMany(x => x.Instrument.Underlying is { } underlying ? new[] { x.Instrument.Quote, underlying } : [x.Instrument.Quote]);
        return new((input.EvaluatedAtUtc - candidate.EvaluatedAtUtc).TotalMilliseconds,
            quotes.Select(x => (double?)(input.EvaluatedAtUtc - x.EventAtUtc.UtcDateTime).TotalMilliseconds).DefaultIfEmpty().Max(), EnforcesAgeLimit(input.SizingAuthority.Environment));
    }

    public static double RecordWorkflow(TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model.IntrinsicTimeStrategyWorkflowView view)
    {
        var elapsed = (view.TerminalAtUtc!.Value - view.StartedAtUtc).TotalMilliseconds;
        WorkflowDuration.Record(elapsed, new KeyValuePair<string, object?>("environment", view.RiskExecution!.SizingAuthority.Environment),
            new KeyValuePair<string, object?>("outcome", view.Status.ToString()));
        return elapsed;
    }

    public static void Record(ExecuteRiskManagementPipelineCommand input, RiskLatencyObservation observation)
    {
        var tag = new KeyValuePair<string, object?>("environment", input.SizingAuthority.Environment);
        CandidateAge.Record(observation.CandidateAgeMilliseconds, tag);
        if (observation.OldestQuoteAgeMilliseconds is { } quoteAge) QuoteAge.Record(quoteAge, tag);
    }
}

public sealed record RiskLatencyObservation(double CandidateAgeMilliseconds, double? OldestQuoteAgeMilliseconds, bool AgeLimitEnforced);
