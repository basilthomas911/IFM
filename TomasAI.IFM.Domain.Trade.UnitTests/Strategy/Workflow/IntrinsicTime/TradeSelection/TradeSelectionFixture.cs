using System.Text.Json;
using MessagePack;
using NSubstitute;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Application.Storage.ConfigurationDb.StrategyCatalog;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Workflow;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.TradeSelection;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection;
using TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.MarketCondition;
namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.TradeSelection;
internal static class TradeSelectionFixture
{
    public static async Task<ExecuteTradeSelectionPipelineCommand> Command(string variantCode="LongFuture",TimeFrameType horizon=TimeFrameType.Daily, DateTime? atUtc=null, string contractId="ESZ6", int scopeId=1, bool compositionReady=false, bool compositionIntegrationTiming=false,
        ExecuteMarketConditionAssessmentCommand? actualAssessmentCommand=null,StrategyStageResultEnvelope? actualAssessmentEnvelope=null)
    {
        var assessmentCommand=actualAssessmentCommand??AssessmentFixture.Command(horizon,atUtc,contractId);
        var at=actualAssessmentEnvelope is null?assessmentCommand.RequestedAtUtc:DateTime.UtcNow;
        var common=TradeSelectionDefaultProfiles.Create(atUtc.HasValue?Guid.NewGuid():Guid.Parse("11111111-1111-1111-1111-111111111111"),horizon) with {MaximumExecutionMilliseconds=atUtc.HasValue?60000:2000};
        var examples=StrategyCatalogExamples.Create();var sourceVariant=examples.Single(x=>x.Code==variantCode);
        var structure=examples.Single(x=>x.Key==sourceVariant.Parent);
        var builder=structure.Capabilities.Single(x=>x.Role=="builder");
        var rule=common.VariantRules.Single(x=>x.BuilderCapabilityCode==builder.Code && x.Side==sourceVariant.Side && x.Bias==sourceVariant.Bias && x.PremiumMode==sourceVariant.PremiumMode);
        var decision=new RegimeDiscoveryDecision{IsComplete=true,Direction=rule.AllowedRegimeDirections[0],Confidence=.9m,Quality=RegimeOverallQuality.High,
            TrendPhase=rule.AllowedTrendPhases[0],TrendStrength=rule.AllowedTrendStrengths[0],VolatilityLevel=VolatilityRegimeLevel.Normal,VolatilityChange=VolatilityRegimeChange.Stable,StructureClassification=rule.AllowedStructureClassifications[0]};
        var upstream=assessmentCommand.RegimeResultEnvelope.ReadRegimeResult();
        if(actualAssessmentEnvelope is null) upstream=upstream with {Decision=decision};
        var regimeEnvelope=StrategyStageResultEnvelope.CreateRegime(upstream);
        assessmentCommand=assessmentCommand with {RegimeResultEnvelope=regimeEnvelope,RegimePayloadSha256=regimeEnvelope.PayloadSha256,WorkflowView=assessmentCommand.WorkflowView with {RegimeDiscovery=assessmentCommand.WorkflowView.RegimeDiscovery with {Result=regimeEnvelope}}};
        var assessment=actualAssessmentEnvelope?.AssessmentResult??new MarketConditionAssessmentCalculator().Calculate(assessmentCommand,Snapshot(assessmentCommand).Seal(),assessmentCommand.CommandId);
        if(actualAssessmentEnvelope is null) assessment=assessment with {Assessment=assessment.Assessment with {Availability=AssessmentAvailability.Available,ConditionType=rule.AllowedAssessmentConditions[0],AssessmentConfidence=.9m,
            LiquidityCondition=AssessmentLiquidity.Healthy,SessionState=MarketSessionStatus.Open,EventRiskState=AssessmentEventContext.Clear,StressState=AssessmentStress.Normal,
            VolatilityBehavior=rule.AllowedVolatilityBehavior[0],TriggerAlignment=AssessmentTriggerAlignment.Aligned,DataQuality=MarketConditionDataQuality.Healthy,ValidUntilUtc=at.AddSeconds(30),UpstreamContext=decision,InheritedRestrictions=[]}};
        var assessmentEnvelope=StrategyStageResultEnvelope.CreateAssessment(assessment);
        var variant=sourceVariant with {Settings=JsonSerializer.SerializeToElement(new{TargetNetDelta=builder.Code=="Future"?(sourceVariant.Side=="Long"?1m:-1m):sourceVariant.Bias=="Balanced"?0m:sourceVariant.Bias=="Bullish"?.15m:-.15m,
            BalanceTolerance=.05m,SymmetricWings=true,MinimumWingWidth=builder.Code=="Future"?0m:5m,MaximumWingWidth=builder.Code=="Future"?0m:10m,DeltaUnits="UnderlyingEquivalent"})};
        var strategy=examples.Single(x=>x.Key.Kind==StrategyCatalogKind.Strategy) with {Structures=[structure.Key]};
        var composition=new SelectionConstructionPolicy{SchemaVersion=1,ParameterSetId=atUtc.HasValue?Guid.NewGuid():Guid.Parse("22222222-2222-2222-2222-222222222222"),Version=1,MaximumLegs=4,MinimumDaysToExpiry=7,MaximumDaysToExpiry=90,
            MinimumWingWidth=builder.Code=="Future"?0m:5m,MaximumWingWidth=10m,DeltaUnits="UnderlyingEquivalent",MaximumDeltaTolerance=.1m};
        var selectionRef=new SelectionPipelinePolicyReference{Kind=CatalogPipelineParameterKind.TradeSelection,Id=common.ParameterSetId,Version=1,PayloadSha256=TradeSelectionPolicy.Hash(common)};
        var deployment=StrategyCatalogExamples.New(StrategyCatalogKind.Deployment,"TestDeployment","Test deployment") with {Parent=strategy.Key,Horizon=horizon,Variants=[variant.Key],Products=[new(1,"ES","CME","USD")],Capabilities=[new("validator","StructureVariant",1)],
            PipelineParameters=[new("selection-policy",CatalogPipelineParameterKind.TradeSelection,common.ParameterSetId,1,selectionRef.PayloadSha256),new("composition-policy",CatalogPipelineParameterKind.OrderComposition,composition.ParameterSetId,1,composition.Hash())]};
        var definitions = new List<StrategyCatalogDefinition> { examples.Single(x=>x.Key==strategy.Families[0]),structure,strategy,variant,deployment };
        if (compositionReady)
        {
            var authored = TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model.CompositionDefaultProfiles.Create([variant], horizon,
                new TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model.Black76ComposerPricer().Version);
            // Outer policy intersects the same horizon bounds; test authority is an explicit new immutable graph.
            var compositionRule = authored.VariantRules[0];
            authored = authored with { VariantRules = [compositionRule with { BaseParameters = compositionRule.BaseParameters with { MaximumDaysToExpiry = Math.Min(compositionRule.BaseParameters.MaximumDaysToExpiry, composition.MaximumDaysToExpiry) } }] };
            if (compositionIntegrationTiming)
                authored = authored with { VariantRules = [authored.VariantRules[0] with { BaseParameters = authored.VariantRules[0].BaseParameters with
                { LoadingMilliseconds = 15000, ExecutionMilliseconds = 15000, CandidateLifetimeMilliseconds = 5000, MaximumQuoteAgeMilliseconds = 5000 } }] };
            var settings = JsonSerializer.SerializeToElement(authored);
            var schema = StrategyCatalogExamples.New(StrategyCatalogKind.ParameterSchema, "CompositionRulesSchema", "Composition rules schema") with
            { Settings = TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model.CompositionRulesSchema.Settings(), Capabilities = [new("validator", "OrderCompositionRules", 1)] };
            var parameters = StrategyCatalogExamples.New(StrategyCatalogKind.ParameterSet, "CompositionRules", "Composition rules") with { Parent = schema.Key, Settings = settings };
            deployment = deployment with { Parameters = [new("OrderCompositionRules", parameters.Key)] };
            definitions[^1] = deployment; definitions.Add(schema); definitions.Add(parameters);
        }
        var source=definitions.Select(x=>new StoredStrategyCatalogDefinition(StrategyCatalogValidation.Freeze(x),StrategyCatalogValidation.ContentHash(x),CatalogLifecycleStatus.Published,at.AddDays(-2),"fixture",at.AddDays(-1),"fixture",null,null)).ToArray();
        var graph=new StrategyCatalogSnapshot(deployment.Key,at,source,SelectionCatalogTransport.GraphHash(deployment.Key,source));
        var config=Substitute.For<IConfigurationDbContext>();
        config.ResolveTradeSelectionVersionAsync(common.ParameterSetId,1,selectionRef.PayloadSha256,at,Arg.Any<CancellationToken>()).Returns(new ResolvedTradeSelectionParameterSet(common,selectionRef.PayloadSha256,ConfigurationParameterSetStatus.Published,at.AddDays(-1),null));
        config.GetPublishedStrategyDeploymentAsync(deployment.Key,at,Arg.Any<CancellationToken>()).Returns(graph);
        config.GetSelectionPipelinePolicyAsync(CatalogPipelineParameterKind.TradeSelection,common.ParameterSetId,1,Arg.Any<CancellationToken>()).Returns(new SelectionPipelinePolicySnapshot{Kind=CatalogPipelineParameterKind.TradeSelection,Id=common.ParameterSetId,Version=1,SchemaVersion=1,PayloadJson=TradeSelectionPolicy.Serialize(common),PayloadSha256=selectionRef.PayloadSha256,Status=CatalogLifecycleStatus.Published,EffectiveFromUtc=at.AddDays(-1)});
        config.GetSelectionPipelinePolicyAsync(CatalogPipelineParameterKind.OrderComposition,composition.ParameterSetId,1,Arg.Any<CancellationToken>()).Returns(new SelectionPipelinePolicySnapshot{Kind=CatalogPipelineParameterKind.OrderComposition,Id=composition.ParameterSetId,Version=1,SchemaVersion=1,PayloadJson=composition.Serialize(),PayloadSha256=composition.Hash(),Status=CatalogLifecycleStatus.Published,EffectiveFromUtc=at.AddDays(-1)});
        var permission=new TradeStrategyFamilyReference(0,0){CatalogDeployment=deployment.Key};
        var family=builder.Code=="Future"?"Futures":builder.Code=="IronCondor"?"IronCondor":"VerticalSpread";
        var asset=builder.Code=="Future"?"Futures":"FuturesOptions";
        var authority=new PortfolioFundStrategySnapshot
        {
            WorkflowId=assessmentCommand.WorkflowId.Value,WorkflowRevision=1,CorrelationId=assessmentCommand.CorrelationId,
            Portfolio=new(){PortfolioId=scopeId,PortfolioVersion=1,Name="Portfolio",OperatingState=PortfolioOperatingState.Active,ActivePolicyId=1,ActivePolicyVersion=1,EffectiveFromUtc=at.AddDays(-1),CreatedOnUtc=at.AddDays(-1),CreatedBy="fixture"},
            Fund=new(){PortfolioId=scopeId,FundId=scopeId,FundMandateVersion=1,FundCode="1",Name="Fund",Objective="Test",SchemaVersion=3,TradingYear=at.Year,DecisionHorizon=horizon.ToString(),OperatingState=FundOperatingState.Active,
                UnderlyingUniverse=["ES"],EligibleAssetTypes=["Futures","FuturesOptions"],PermittedDirections=["Bullish","Bearish","Neutral"],PermittedConditions=Enum.GetNames<AssessmentCondition>().Where(x=>x!="Undefined").ToArray(),PermittedTradeFamilies=["Futures","IronCondor","VerticalSpread"],PermittedTradeStrategyFamilies=[permission],EffectiveFromUtc=at.AddDays(-1),CreatedOnUtc=at.AddDays(-1),CreatedBy="fixture"},
            FinancialPolicy=new(){PortfolioId=scopeId,PolicyId=1,PolicyVersion=1,Name="Policy",OperatingState=PortfolioFinancialPolicyState.Active,CapitalBase=10000,MaximumDeployableCapital=10000,MaximumRiskPerTrade=100,MaximumAggregateRisk=1000,MaximumMargin=1000,MaximumGrossNotional=10000,MaximumOpenPositions=10,EffectiveFromUtc=at.AddDays(-1),CreatedOnUtc=at.AddDays(-1),CreatedBy="fixture",
                TradeFamilyLimits=[new(){CatalogDeployment=deployment.Key,Enabled=true,MaximumRiskPerTrade=100,MaximumAggregateRisk=1000,MaximumMargin=1000,MaximumGrossNotional=10000,MaximumOpenPositions=10}]},
            Allocation=new(){PortfolioId=scopeId,PortfolioVersion=1,FundId=scopeId,FundMandateVersion=1,AllocationVersion=1,SourcePolicyId=1,SourcePolicyVersion=1,EffectiveFromUtc=at.AddDays(-1),CreatedOnUtc=at.AddDays(-1),CreatedBy="fixture"},
            RiskEnvelope=new(){PortfolioId=scopeId,PortfolioVersion=1,FundId=scopeId,FundMandateVersion=1,EnvelopeId=Guid.NewGuid(),EnvelopeVersion=1,SourcePolicyId=1,SourcePolicyVersion=1,CapacityState=FundCapacityState.Available,MaximumRiskPerTrade=100,MaximumAggregateRisk=1000,MaximumMargin=1000,MaximumGrossNotional=10000,MaximumOpenPositions=10,EffectiveFromUtc=at.AddDays(-1),ExpiresAtUtc=at.AddMinutes(1),CreatedOnUtc=at.AddDays(-1),CreatedBy="fixture"},
            Assignments=[new(){PortfolioId=scopeId,PortfolioVersion=1,FundId=scopeId,FundMandateVersion=1,SchemaVersion=3,AssignmentVersion=1,TradeStrategyFamily=permission,TradeTemplateId=deployment.Key.Id,TradeTemplateVersion=1,Enabled=true,Priority=1,DecisionHorizon=horizon.ToString(),UnderlyingUniverse=["ES"],AssetType=asset,TradeFamily=family,TradeSelectionHintProfileId=common.ParameterSetId,TradeSelectionHintProfileVersion=1,OrderCompositionProfileId=composition.ParameterSetId,OrderCompositionProfileVersion=1,EffectiveFromUtc=at.AddDays(-1),CreatedOnUtc=at.AddDays(-1),CreatedBy="fixture"}],
            ResolvedAtUtc=at,ValidUntilUtc=at.AddMinutes(1)
        };
        authority=authority with {PayloadSha256=PortfolioCanonicalHash.Compute(authority)};
        var binding=await new TradeSelectionBindingResolver(config).ResolveAsync(authority,selectionRef,DateOnly.FromDateTime(assessmentCommand.TriggerEvent.CreatedOn));
        var view=assessmentCommand.WorkflowView with {FundId=scopeId,WorkflowRevision=3,CurrentStage=StrategyWorkflowStage.TradeSelection,UpdatedAtUtc=at.AddMilliseconds(1),SelectionBinding=binding,
            MarketCondition=new(){ProcessingStatus=StrategyActorProcessingStatus.Completed,InputWorkflowRevision=2,Result=assessmentEnvelope},TradeSelection=new(){ProcessingStatus=StrategyActorProcessingStatus.Processing,InputWorkflowRevision=3}};
        return TradeSelectionDispatch.Create(view,Guid.NewGuid());
    }
    internal static CatalogParameterShape Shape(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Object => new() { Type = CatalogValueType.Object, Properties = value.EnumerateObject().ToDictionary(x => x.Name, x => Shape(x.Value)), Required = value.EnumerateObject().Select(x => x.Name).ToArray() },
        JsonValueKind.Array => new() { Type = CatalogValueType.Array, Items = value.GetArrayLength() == 0 ? new() { Type = CatalogValueType.Object } : Shape(value[0]), MaxLength = 64 },
        JsonValueKind.String => new() { Type = CatalogValueType.String },
        JsonValueKind.Number => new() { Type = CatalogValueType.Decimal },
        JsonValueKind.True or JsonValueKind.False => new() { Type = CatalogValueType.Boolean },
        _ => throw new ArgumentException("Unsupported fixture field.")
    };
    internal static MarketConditionAssessmentSnapshot Snapshot(ExecuteMarketConditionAssessmentCommand c) => new()
    {
        SnapshotId = Guid.NewGuid(), MarketProfileId = c.MarketProfileId, InstrumentRoot = c.InstrumentRoot, TargetHorizon = c.TargetHorizon,
        ReferenceInstrumentId = "ES.TEST", EvaluatedAtUtc = c.RequestedAtUtc, Quote = new(5000,5000.25m,10,10), SessionState = MarketSessionStatus.Open, EventContext = AssessmentEventContext.Clear,
        Observations = c.ParameterSet.Sources.Select(x => new AssessmentObservation
        {
            SourceId = x.SourceId, ObservedAtUtc = c.RequestedAtUtc.AddSeconds(-1), ReceivedAtUtc = c.RequestedAtUtc, Sequence = 10,
            Availability = MarketSourceAvailability.Available, Validity = MarketSourceValidity.Valid, Value = 0, Unit = "ratio"
        }).ToArray(),
        CalendarEvidence = new() { CheckedAtUtc = c.RequestedAtUtc, CoverageConfirmed = true, ValidUntilUtc = c.RequestedAtUtc.AddHours(1) }
    };
}
