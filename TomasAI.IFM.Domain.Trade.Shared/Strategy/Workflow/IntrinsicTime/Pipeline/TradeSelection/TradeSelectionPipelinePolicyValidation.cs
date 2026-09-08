using System.Text.Json;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.TradeSelection;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
public static partial class TradeSelectionContracts
{
    public static void ValidatePipelinePolicy(SelectionPipelinePolicySnapshot row)
    {
        TradeSelectionPolicy.CheckJson(row.PayloadJson);
        (Guid Id,int Version,string Hash) identity;
        switch(row.Kind)
        {
            case CatalogPipelineParameterKind.TradeSelection:
                var selection=TradeSelectionPolicy.Read(row.PayloadJson);identity=(selection.ParameterSetId,selection.Version,TradeSelectionPolicy.Hash(selection));break;
            case CatalogPipelineParameterKind.OrderComposition:
                var composition=SelectionConstructionPolicy.Read(row.PayloadJson);
                Require(composition.SchemaVersion==row.SchemaVersion,"TS.CONTRACT.SCHEMA","Construction schema metadata differs from its payload.");
                identity=(composition.ParameterSetId,composition.Version,composition.Hash());break;
            case CatalogPipelineParameterKind.IntrinsicTimeStrategyWorkflow:
                var activation=TradeSelectionActivation.Read(row.PayloadJson);identity=(activation.ParameterSetId,activation.Version,activation.Hash());break;
            case CatalogPipelineParameterKind.RegimeDiscovery:
                var regime=JsonSerializer.Deserialize<RegimeDiscoveryParameterSet>(row.PayloadJson)??throw new ArgumentException("Missing regime policy.");
                Require(new RegimeDiscoveryParameterSetValidationRules().Execute(regime).Length==0,"TS.CONFIG.INVALID","Invalid regime policy.");
                identity=(regime.ParameterSetId,regime.Version,RegimeDiscoveryParameterPayload.ComputeSha256(regime));break;
            case CatalogPipelineParameterKind.MarketConditionAssessment:
                var assessment=JsonSerializer.Deserialize<MarketConditionAssessmentParameterSet>(row.PayloadJson)??throw new ArgumentException("Missing assessment policy.");
                assessment.Validate();identity=(assessment.ParameterSetId,assessment.Version,MarketConditionAssessmentHash.Parameters(assessment));break;
            default:throw new TradeSelectionValidationException("TS.CONFIG.CAPABILITY_UNSUPPORTED","No implemented owning schema for required pipeline policy "+row.Kind);
        }
        Require(identity.Id==row.Id && identity.Version==row.Version && identity.Hash.Equals(row.PayloadSha256,StringComparison.OrdinalIgnoreCase),"TS.CONTRACT.HASH","Pipeline payload metadata/hash mismatch.");
    }
}
