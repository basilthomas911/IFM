using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;

public static class MarketConditionAssessmentContracts
{
    public static RegimeDiscoveryResult ValidateRequest(ExecuteMarketConditionAssessmentCommand c)
    {
        ArgumentNullException.ThrowIfNull(c);
        var errors = new List<TomasAI.IFM.Shared.Validation.ValidationError>()
            .ValidateAssessmentIdentity(c.EntityId).ValidateAssessmentRevision(c.InputWorkflowRevision)
            .ValidateAssessmentWorkflow(c.WorkflowView).ValidateAssessmentTrigger(c.TriggerEvent)
            .ValidateAssessmentTraceId(c.CorrelationId, nameof(c.CorrelationId)).ValidateAssessmentTraceId(c.CausationId, nameof(c.CausationId))
            .ValidateAssessmentTimestamp(c.RequestedAtUtc, nameof(c.RequestedAtUtc)).ValidateAssessmentTimestamp(c.ExpiresAtUtc, nameof(c.ExpiresAtUtc))
            .ValidateAssessmentParameters(c.ParameterSet).ValidateAssessmentHash(c.ParameterPayloadSha256, nameof(c.ParameterPayloadSha256))
            .ValidateAssessmentHash(c.RegimePayloadSha256, nameof(c.RegimePayloadSha256)).ValidateAssessmentHorizon(c.TargetHorizon)
            .ValidateAssessmentUpstream(c.RegimeResultEnvelope, c.WorkflowView).ValidateAssessmentConsistency(c);
        if (errors.Count != 0) throw Invalid(string.Join(Environment.NewLine, errors.Select(error => error.ErrorMessage)));
        return ReadRegime(c.RegimeResultEnvelope, c.WorkflowView);
    }

    public static RegimeDiscoveryResult ReadRegime(StrategyStageResultEnvelope envelope, IntrinsicTimeStrategyWorkflowView v)
    {
        var p = v.AssessmentBinding?.Parameters ?? throw Invalid("Missing assessment profile.");
        if (envelope.ResultType != nameof(RegimeDiscoveryResult) || envelope.SchemaVersion != RegimeDiscoveryResult.CurrentSchemaVersion ||
            !envelope.HasValidPayloadSha256()) throw Invalid("Invalid accepted regime envelope.");
        var r = envelope.ReadRegimeResult();
        if (!IsRegimeConsistent(r, envelope, v, p)) throw Invalid("Accepted regime lineage or decision is invalid.");
        return r;
    }

    /// <summary>Compares typed upstream lineage without exception-based validation dispatch.</summary>
    internal static bool IsRegimeConsistent(RegimeDiscoveryResult? r, StrategyStageResultEnvelope envelope,
        IntrinsicTimeStrategyWorkflowView v, MarketConditionAssessmentParameterSet p)
    {
        if (r?.Decision is null || v.TriggerEvent?.EntityId is null || v.RegimeDiscoveryParameterSet is null || p.HorizonProfile is null) return false;
        var triggerId = v.TriggerEvent.Id == Guid.Empty ? v.TriggerEvent.CommandId : v.TriggerEvent.Id;
        return !(r.SchemaVersion != RegimeDiscoveryResult.CurrentSchemaVersion || r.ResultId != envelope.ResultId ||
            r.WorkflowId != v.WorkflowId || r.EntityId != v.EntityId || r.TriggerEventId != triggerId ||
            r.TargetHorizon != v.TriggerEvent.EntityId.TimePeriod || r.TargetHorizon != p.TargetHorizon ||
            r.RegimeDiscoveryParameterSetId != p.HorizonProfile.RegimeProfileId ||
            r.RegimeDiscoveryParameterSetVersion != p.HorizonProfile.RegimeProfileVersion ||
            r.RegimeDiscoveryParameterSetId != v.RegimeDiscoveryParameterSet.ParameterSetId ||
            r.RegimeDiscoveryParameterSetVersion != v.RegimeDiscoveryParameterSet.Version ||
            !Utc(r.ProducedAtUtc) || !Utc(r.MarketDataAsOfUtc) || envelope.ProducedAtUtc != r.ProducedAtUtc ||
            envelope.MarketDataAsOfUtc != r.MarketDataAsOfUtc || !r.Decision.IsComplete ||
            r.Decision.Confidence is < 0 or > 1 || !Enum.IsDefined(r.Decision.Direction) || r.Decision.Direction == RegimeDirection.Unknown ||
            !Enum.IsDefined(r.Decision.StructureClassification) || !Enum.IsDefined(r.Decision.VolatilityChange) ||
            r.Decision.Restrictions is null || r.Decision.Restrictions.Any(x => !Enum.IsDefined(x)));
    }

    public static MarketConditionAssessmentResult ReadResult(StrategyStageResultEnvelope envelope)
    {
        if (envelope.ResultType != nameof(MarketConditionAssessmentResult) || envelope.SchemaVersion != 1 ||
            !envelope.HasValidPayloadSha256())
            throw Invalid("Invalid assessment result envelope.");
        var result = envelope.ReadAssessmentResult();
        ValidateResult(result);
        if (result.ResultId != envelope.ResultId || result.EvaluatedAtUtc != envelope.ProducedAtUtc)
            throw Invalid("Assessment envelope identity or timestamp mismatch.");
        return result;
    }

    public static void ValidateResult(MarketConditionAssessmentResult r)
    {
        var a = r.Assessment;
        if (r.SchemaVersion != 1 || r.ResultId == Guid.Empty || r.CommandId == Guid.Empty || r.WorkflowId.Value == Guid.Empty ||
            r.ParameterSetId == Guid.Empty || r.ParameterSetVersion <= 0 || r.RegimeResultId == Guid.Empty || r.SnapshotId == Guid.Empty ||
            !Hash(r.ParameterPayloadSha256) || !Hash(r.RegimePayloadSha256) || !Hash(r.SnapshotSha256) ||
            string.IsNullOrWhiteSpace(r.MarketProfileId) || r.InstrumentRoot != "ES" || !Utc(r.EvaluatedAtUtc) ||
            !MarketConditionAssessmentParameterSet.IsHorizon(r.TargetHorizon) || a is null || a.SchemaVersion != 1 ||
            a.Horizon != r.TargetHorizon || a.RegimeResultId != r.RegimeResultId || a.RegimePayloadSha256 != r.RegimePayloadSha256 ||
            a.EvaluatedAtUtc != r.EvaluatedAtUtc || a.Availability is not (AssessmentAvailability.Available or AssessmentAvailability.Unavailable) ||
            !Enum.IsDefined(a.LiquidityCondition) || !Enum.IsDefined(a.StressState) || !Enum.IsDefined(a.EventRiskState) ||
            !Enum.IsDefined(a.SessionState) || !Enum.IsDefined(a.VolatilityBehavior) || !Enum.IsDefined(a.TriggerAlignment) ||
            a.EvidenceItems.Any(x => x.Horizon != r.TargetHorizon) || a.ConflictingEvidenceItems.Any(x => x.Horizon != r.TargetHorizon) ||
            a.InheritedRestrictions.Any(x => !Enum.IsDefined(x)) || string.IsNullOrWhiteSpace(r.SummaryText))
            throw Invalid("Assessment result contract is invalid.");
        if (a.Availability == AssessmentAvailability.Available &&
            (a.ConditionType is null or AssessmentCondition.Undefined || !Enum.IsDefined(a.ConditionType.Value) ||
             a.AssessmentConfidence is null or < 0 or > 1 || a.ValidUntilUtc is null || !Utc(a.ValidUntilUtc.Value) ||
             a.ValidUntilUtc <= r.EvaluatedAtUtc || a.UpstreamContext is null))
            throw Invalid("Available assessment has invalid condition, confidence, context or validity.");
        if (a.Availability == AssessmentAvailability.Unavailable &&
            (a.ConditionType is not null || a.AssessmentConfidence is not null || a.ValidUntilUtc is not null || a.LimitationReasons.Length == 0))
            throw Invalid("Unavailable assessment must explain the missing data without current numeric authority.");
    }

    public static void ValidateAcceptance(MarketConditionAssessmentResult r, IntrinsicTimeStrategyWorkflowView v, long revision)
    {
        ValidateResult(r);
        var binding = v.AssessmentBinding ?? throw Invalid("An assessment cannot satisfy a legacy workflow.");
        binding.Validate();
        var regime = ReadRegime(v.RegimeDiscovery.Result ?? throw Invalid("Missing accepted regime."), v);
        if (r.WorkflowId != v.WorkflowId || r.EntityId != v.EntityId || r.InputWorkflowRevision != revision ||
            r.TargetHorizon != v.TriggerEvent.EntityId.TimePeriod || r.TargetHorizon != binding.Parameters.TargetHorizon ||
            r.MarketProfileId != binding.Parameters.MarketProfileId || r.InstrumentRoot != binding.Parameters.InstrumentRoot ||
            r.ParameterSetId != binding.Parameters.ParameterSetId || r.ParameterSetVersion != binding.Parameters.Version ||
            r.ParameterPayloadSha256 != binding.PayloadSha256 || r.RegimeResultId != regime.ResultId ||
            r.RegimePayloadSha256 != v.RegimeDiscovery.Result.PayloadSha256 ||
            !r.Assessment.InheritedRestrictions.Order().SequenceEqual(regime.Decision.Restrictions.Distinct().Order()) ||
            r.Assessment.UpstreamContext is { } context && RegimeDiscoveryResultContent.DecisionFingerprint(context) != RegimeDiscoveryResultContent.DecisionFingerprint(regime.Decision))
            throw Invalid("Assessment conflicts with the accepted workflow invocation.");
    }

    public static bool Utc(DateTime at) => at != default && at.Kind == DateTimeKind.Utc;
    public static MarketConditionAssessmentResult ValidateForSelection(StartTradeSelectionPipelineCommand command, DateTime now)
    {
        var state = command.WorkflowState;
        if (!Utc(now) || state.AssessmentBinding is null || command.WorkflowId != state.WorkflowId || command.EntityId != state.EntityId ||
            command.InputWorkflowRevision != state.WorkflowRevision || state.Status != StrategyWorkflowStatus.Running ||
            state.CurrentStage != StrategyWorkflowStage.TradeSelection || state.MarketCondition.ProcessingStatus != StrategyActorProcessingStatus.Completed ||
            command.ExpectedCompletionAtUtc is null || now >= command.ExpectedCompletionAtUtc)
            throw Invalid("Trade Selection requires a current accepted assessment workflow.");
        var r = ReadResult(state.MarketCondition.Result ?? throw Invalid("Missing accepted assessment."));
        var view = new IntrinsicTimeStrategyWorkflowView
        {
            WorkflowId=state.WorkflowId, EntityId=state.EntityId, TriggerEvent=command.TriggerEvent,
            AssessmentBinding=state.AssessmentBinding, RegimeDiscovery=state.RegimeDiscovery,
            RegimeDiscoveryParameterSet=state.RegimeDiscoveryParameterSet
        };
        ValidateAcceptance(r,view,state.MarketCondition.InputWorkflowRevision);
        if (r.Assessment.Availability != AssessmentAvailability.Available || r.Assessment.ValidUntilUtc <= now ||
            r.Assessment.InheritedRestrictions.Contains(RegimeRestriction.NoNewTrade))
            throw Invalid("Assessment is unavailable, expired or carries an inherited NoNewTrade restriction.");
        return r;
    }
    static bool Hash(string value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
    static ArgumentException Invalid(string message) => new(message);
}
