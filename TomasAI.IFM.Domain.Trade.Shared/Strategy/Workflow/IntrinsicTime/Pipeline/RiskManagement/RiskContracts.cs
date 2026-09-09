using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;

/// <summary>Standard list validation and semantic identities for the typed fifth pipeline stage.</summary>
public static class RiskContracts
{
    public static string Hash<T>(T value) => CompositionSemanticHash.Compute(value);

    public static void ValidateResult(RiskAssessmentResult result)
    {
        bool approved=result.Outcome==RiskAssessmentOutcome.Approved;
        if (result.SchemaVersion!=1 || result.ResultId==Guid.Empty || result.ResultId!=result.InvocationId
            || result.WorkflowId.Value==Guid.Empty || result.InputWorkflowRevision<=0 || result.CompositionResultId==Guid.Empty
            || result.CompositionResultHash.Length!=64 || result.UnitCandidateHash.Length!=64 || result.InputHash.Length!=64
            || result.PolicyHash.Length!=64 || result.PortfolioId<=0 || result.FundId<=0 || result.OrderId<=0
            || result.Reasons.IsDefaultOrEmpty || result.ValidUntilUtc<=result.EvaluatedAtUtc
            || result.ProducedAtUtc!=result.EvaluatedAtUtc || MessagePackBinarySerializer.MeasureContent(result)>524288
            || (approved ? result.StrategyUnits is <=0 or >100 || result.SizedOrderHash.Length!=64
                || result.Requirements is null || result.MarginEvidence is null || result.UnitRisk is null
                || result.Legs.Length is not (1 or 2 or 4) || result.Legs.Any(x=>x.Contracts<=0 || x.TradeId<=0 || x.Side is not ("Buy" or "Sell"))
                || result.Requirements.ContentHash!=FinancialCanonicalHash.Requirements(result.Requirements)
                : result.Outcome!=RiskAssessmentOutcome.Rejected || result.StrategyUnits!=0 || result.Requirements is not null
                    || result.MarginEvidence is not null || !result.Legs.IsEmpty || result.SizedOrderHash.Length!=0))
            throw new ArgumentException("RM.RESULT.INVALID");
    }

    public static List<ValidationError> ValidateRiskFields(this List<ValidationError> errors, ExecuteRiskManagementPipelineCommand c)
    {
        void Check(bool valid, string reason) { if (!valid) errors.Add(new(reason)); }
        Check(c.SchemaVersion == 1 && !c.PostEvents && c.EntityId.AttemptOrdinal is > 0 and <= 3
            && c.InputWorkflowRevision > 0 && c.EntityId.InputWorkflowRevision == c.InputWorkflowRevision
            && c.WorkflowId.Value != Guid.Empty && c.CorrelationId != Guid.Empty && c.CausationId != Guid.Empty,
            "RM.INPUT.IDENTITY");
        Check(c.Subject == new ActorSubject(ActorType.Function, ExecuteRiskManagementPipelineCommand.Actor,
            ExecuteRiskManagementPipelineCommand.Verb,c.EntityId.Format()) && c.RouteTo == BoundedContextName.RiskManagementPipelineBoundedContext,
            "RM.INPUT.ROUTE");
        Check(c.RequestedAtUtc.Kind == DateTimeKind.Utc && c.EvaluatedAtUtc.Kind == DateTimeKind.Utc
            && c.ExpiresAtUtc.Kind == DateTimeKind.Utc && c.EvaluatedAtUtc <= c.RequestedAtUtc && c.RequestedAtUtc < c.ExpiresAtUtc,
            "RM.INPUT.TIME");
        Check(c.Policy is not null && c.SizingAuthority is not null && c.MarketSnapshot is not null
            && c.Authority is not null && c.RegimeResult is not null && c.MarketConditionResult is not null
            && c.SelectionResult is not null && c.CompositionResult is not null, "RM.INPUT.REQUIRED");
        if (errors.Count != 0) return errors;
        Check(c.Policy!.Horizon is TimeFrameType.Daily or TimeFrameType.Weekly or TimeFrameType.Monthly
            && c.Policy.MaximumUnits is > 0 and <= 100 && c.Policy.PerTradeRiskFraction is > 0 and <= 1
            && c.PolicyId != Guid.Empty && c.PolicyVersion > 0 && c.PolicyHash == Hash(new { c.PolicyId,c.PolicyVersion,c.Policy }),
            "RM.POLICY.INVALID");
        Check(c.IncrementalLossReserve >= 0 && c.Funding.Length <= 100 && c.SizingAuthority!.Limits.Length <= 256
            && c.SizingAuthority.Usage.Length <= 10000 && c.MarketSnapshot!.Instruments.Length <= 10000, "RM.INPUT.BOUNDS");
        if (errors.Count != 0) return errors;
        Check(c.RegimeResult!.RegimeResult is not null && c.RegimeResult.HasValidPayloadSha256()
            && c.MarketConditionResult!.AssessmentResult is not null && c.MarketConditionResult.HasValidPayloadSha256()
            && c.SelectionResult!.SelectionResult is { Outcome:SelectionOutcome.Selected, SelectedCandidate:not null } && c.SelectionResult.HasValidPayloadSha256()
            && c.CompositionResult!.CompositionResult is { Outcome:CompositionOutcome.Composed, Candidate:not null } && c.CompositionResult.HasValidPayloadSha256(),
            "RM.INPUT.UPSTREAM");
        if (errors.Count != 0) return errors;
        var regime = c.RegimeResult!.RegimeResult!;
        var assessment = c.MarketConditionResult!.AssessmentResult!;
        var selection = c.SelectionResult!.SelectionResult!;
        var composition = c.CompositionResult!.CompositionResult!;
        var candidate = composition.Candidate!;
        Check(regime.WorkflowId == c.WorkflowId && assessment.WorkflowId == c.WorkflowId && selection.WorkflowId == c.WorkflowId
            && composition.WorkflowId == c.WorkflowId && regime.EntityId == c.WorkflowEntityId && assessment.EntityId == c.WorkflowEntityId
            && selection.EntityId == c.WorkflowEntityId && composition.EntityId == c.WorkflowEntityId
            && regime.TargetHorizon == c.Policy.Horizon && assessment.TargetHorizon == c.Policy.Horizon
            && selection.DecisionHorizon == c.Policy.Horizon && composition.TargetHorizon == c.Policy.Horizon,
            "RM.INPUT.UPSTREAM_IDENTITY");
        Check(assessment.RegimeResultId == regime.ResultId && assessment.RegimePayloadSha256 == c.RegimeResult.PayloadSha256
            && selection.DecisionContext.AssessmentResultEnvelope.HasSameContent(c.MarketConditionResult)
            && composition.DecisionContext.SelectionResultId == selection.ResultId
            && composition.DecisionContext.SelectionResultHash == c.SelectionResult.PayloadSha256
            && composition.InputWorkflowRevision < c.InputWorkflowRevision, "RM.INPUT.LINEAGE");
        var intent = selection.SelectedCandidate!;
        Check(candidate.DeploymentKey == intent.DeploymentKey && candidate.StrategyKey == intent.StrategyKey
            && candidate.StructureKey == intent.StructureKey && candidate.VariantKey == intent.VariantKey
            && candidate.AssignmentVersion == intent.AssignmentVersion && candidate.Side == intent.Side
            && candidate.Bias == intent.Bias && candidate.PremiumMode == intent.PremiumMode
            && candidate.PortfolioId == c.SizingAuthority!.PortfolioId && candidate.FundId == c.SizingAuthority.FundId
            && candidate.DeploymentKey == c.SizingAuthority.DeploymentKey && candidate.DeploymentKey == c.Authority!.DeploymentKey
            && candidate.AssignmentVersion == c.Authority.AssignmentVersion, "RM.INPUT.CATALOG_AUTHORITY");
        Check(c.Authority!.FinancialSnapshotHash.Length == 64 && c.Authority.AuthorityEpoch > 0
            && c.Authority.PortfolioVersion>0 && c.Authority.FundMandateVersion>0 && c.Authority.PolicyId>0 && c.Authority.PolicyVersion>0
            && c.Authority.EnvelopeId!=Guid.Empty && c.Authority.EnvelopeVersion>0 && c.Authority.AssignmentVersion>0
            && !string.IsNullOrWhiteSpace(c.Authority.SourceWatermark) && !string.IsNullOrWhiteSpace(c.Authority.ValuationWatermark)
            && c.SizingAuthority!.EvaluatedAtUtc == c.EvaluatedAtUtc && c.SizingAuthority.ValidUntilUtc >= c.ExpiresAtUtc
            && c.Authority.ValidUntilUtc >= c.ExpiresAtUtc && candidate.ValidUntilUtc >= c.ExpiresAtUtc
            && assessment.Assessment.ValidUntilUtc >= c.ExpiresAtUtc
            && c.MarketSnapshot!.ValidUntilUtc.UtcDateTime >= c.ExpiresAtUtc, "RM.INPUT.AUTHORITY_EXPIRY");
        Check(c.InputSha256 == c.Fingerprint() && MessagePackBinarySerializer.MeasureContent(c) <= 1048576
            && MessagePackBinarySerializer.MeasureEncoded(c) <= 1048576, "RM.INPUT.HASH_OR_SIZE");
        return errors;
    }
}
