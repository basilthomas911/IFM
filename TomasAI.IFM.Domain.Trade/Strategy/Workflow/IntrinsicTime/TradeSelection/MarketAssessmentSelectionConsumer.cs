using System.Security.Cryptography;
using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection;

public sealed record AssessmentSelectionCandidate(Guid StrategyId,string AssetType,string TradeFamily,TradeStrategyFamilyReference StrategyFamily);
public sealed record AssessmentSelectionDecision(AssessmentSelectionCandidate[] Candidates,string Reason, long FundMandateVersion)
{ public bool NoStrategy => Candidates.Length == 0; }

/// <summary>Selector-side suitability boundary. The caller supplies a frozen, versioned mandate and strategy candidates.</summary>
public sealed class MarketAssessmentSelectionConsumer
{
    public static string MandateHash(FundMandateReadModel mandate) => Convert.ToHexString(SHA256.HashData(MessagePackSerializer.Serialize(mandate.DefensiveCopy())));
    public AssessmentSelectionDecision Select(StartTradeSelectionPipelineCommand command,FundMandateReadModel frozenMandate,
        string mandatePayloadSha256,IReadOnlyCollection<AssessmentSelectionCandidate> candidates,DateTime now)
    {
        var r=MarketConditionAssessmentContracts.ValidateForSelection(command,now);
        var mandate=frozenMandate.DefensiveCopy();
        if(mandate.Validate().Count!=0 || mandate.FundId!=command.WorkflowState.FundId || MandateHash(mandate)!=mandatePayloadSha256)
            throw new ArgumentException("Frozen fund mandate identity, validation or hash mismatch.");
        if(mandate.EffectiveFromUtc>now || mandate.EffectiveUntilUtc is { } until && now>=until ||
            mandate.OperatingState!=FundOperatingState.Active || mandate.DecisionHorizon!=r.TargetHorizon.ToString() ||
            !mandate.UnderlyingUniverse.Contains(r.InstrumentRoot,StringComparer.Ordinal))
            return new([],"SELECTOR.MANDATE.NOT_ELIGIBLE",mandate.FundMandateVersion);
        var direction=r.Assessment.UpstreamContext!.Direction switch { RegimeDirection.Up=>"Bullish",RegimeDirection.Down=>"Bearish",_=>"Neutral" };
        if(!mandate.PermittedDirections.Contains(direction,StringComparer.OrdinalIgnoreCase) &&
            !mandate.PermittedDirections.Contains(r.Assessment.UpstreamContext.Direction.ToString(),StringComparer.OrdinalIgnoreCase) ||
            !mandate.PermittedConditions.Contains(r.Assessment.ConditionType!.Value.ToString(),StringComparer.OrdinalIgnoreCase))
            return new([],"SELECTOR.MANDATE.MARKET_CONDITION",mandate.FundMandateVersion);
        var allowed=candidates.Where(x=>x.StrategyId!=Guid.Empty && mandate.EligibleAssetTypes.Contains(x.AssetType,StringComparer.Ordinal) &&
            mandate.PermittedTradeFamilies.Contains(x.TradeFamily,StringComparer.Ordinal) && mandate.PermittedTradeStrategyFamilies.Contains(x.StrategyFamily))
            .Distinct().OrderBy(x=>x.StrategyId).ToArray();
        return new(allowed,allowed.Length==0?"SELECTOR.NO_SUITABLE_STRATEGY":"SELECTOR.CANDIDATES",mandate.FundMandateVersion);
    }
}
