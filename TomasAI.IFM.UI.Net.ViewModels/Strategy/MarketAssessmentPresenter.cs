using System.Globalization;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;

namespace TomasAI.IFM.UI.Net.ViewModels.Strategy;

public static class MarketAssessmentPresenter
{
    public static string Render(IntrinsicTimeStrategyWorkflowView workflow,MarketConditionAssessmentCompletedEvent? projected,DateTime now)
    {
        if(workflow.AssessmentBinding is null)
        {
            if(workflow.MarketCondition.Result is not { ResultType:nameof(MarketConditionResult) } legacy) return "Legacy Market Condition — no result recorded.";
            if(!legacy.HasValidPayloadSha256()) throw new ArgumentException("Invalid legacy result hash.");
            var r=MessagePackSerializer.Deserialize<MarketConditionResult>(legacy.Payload);
            return $"Legacy Market Condition (schema {r.SchemaVersion})\r\nTimeframe: {r.TargetHorizon}\r\nTradeability: {r.Tradeability}\r\n{r.SummaryText}\r\nEvaluated: {r.EvaluatedAtUtc:O}\r\nValid until: {r.ValidUntilUtc:O}";
        }
        var accepted=workflow.MarketCondition.Result is { ResultType:nameof(MarketConditionAssessmentResult) } e?e:null;
        var envelope=accepted??projected?.Result;
        if(envelope is null) return $"Market assessment — {workflow.AssessmentBinding.Parameters.TargetHorizon}\r\nNo assessment recorded. Workflow: {workflow.Status}, stage: {workflow.CurrentStage}.";
        var result=MarketConditionAssessmentContracts.ReadResult(envelope);
        var a=result.Assessment;
        if(accepted is not null) MarketConditionAssessmentContracts.ValidateAcceptance(result,workflow,workflow.MarketCondition.InputWorkflowRevision);
        var projectionMatches=projected?.Result.PayloadSha256==accepted?.PayloadSha256 && accepted is not null;
        var current=accepted is not null && a.Availability==AssessmentAvailability.Available && a.ValidUntilUtc>now;
        var lines=new List<string>
        {
            $"Market assessment (schema {result.SchemaVersion}) — {result.TargetHorizon}",
            $"Market: {result.InstrumentRoot} | Profile: {result.MarketProfileId}",
            $"Workflow: {result.WorkflowId} | {workflow.Status} | {workflow.CurrentStage}",
            $"Authority: {(accepted is null?"Projection not accepted by workflow":current?"Accepted and current":a.Availability==AssessmentAvailability.Unavailable?"Accepted: unavailable":"Accepted: expired")}",
            $"Projection: {(projectionMatches?"Matches accepted result":projected is null?"Not found":"Does not match accepted result")}",
            $"Availability: {a.Availability} | Condition at evaluation: {a.ConditionType?.ToString()??"Unavailable"}",
            $"Confidence at evaluation: {a.AssessmentConfidence?.ToString("0.000000",CultureInfo.InvariantCulture)??"Unavailable"}",
            $"Liquidity: {a.LiquidityCondition} | Stress: {a.StressState} | Session: {a.SessionState} | Events: {a.EventRiskState}",
            $"Trigger alignment: {a.TriggerAlignment} | Data quality: {a.DataQuality}",
            $"Inherited restrictions: {string.Join(", ",a.InheritedRestrictions)}",
            $"Evaluated: {a.EvaluatedAtUtc:O} | Valid until: {a.ValidUntilUtc?.ToString("O")??"Unavailable"}",
            $"Limitations: {string.Join(", ",a.LimitationReasons)}",result.SummaryText,"","Evidence:"
        };
        lines.AddRange(a.EvidenceItems.Select(x=>string.Create(CultureInfo.InvariantCulture,$"{x.SourceId} / {x.Feature}: {x.Value} {x.Unit}; {x.Availability}; age at evaluation {x.AgeSeconds:0.###}s; observed {x.ObservedAtUtc:O}; sequence {x.Sequence}; {x.Reason}")));
        lines.Add($"\r\nUpstream result: {result.RegimeResultId}\r\nUpstream hash: {result.RegimePayloadSha256}\r\nParameter hash: {result.ParameterPayloadSha256}\r\nSnapshot: {result.SnapshotId}\r\nSnapshot hash: {result.SnapshotSha256}");
        return string.Join("\r\n",lines);
    }
}
