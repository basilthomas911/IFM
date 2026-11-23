using TomasAI.IFM.Application.Strategy.StrategyCatalog;
using System.Text.Json;
using NSubstitute;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Domain.Strategy.Contracts.Shared.Configuration;
using TomasAI.IFM.Domain.Strategy.Contracts.Shared.Canonical;
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
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection;
// Pin one offline portfolio/catalog fixture. The assessment and regime envelopes come from the historical pilot.
internal static class PilotSelection
{
    public static async Task<ExecuteTradeSelectionPipelineCommand> CreateAsync(
        ExecuteMarketConditionAssessmentCommand assessmentCommand,
        MarketConditionAssessmentResult assessment)
    {
        const int scopeId = 1;
        const int policyId = 1;
        var at = assessment.EvaluatedAtUtc;
        var horizon = TimeFrameType.Monthly;
        var assessmentEnvelope = StrategyStageResultEnvelope.CreateAssessment(assessment);
        var common = TradeSelectionDefaultProfiles.Create(TradeSelectionDefaultProfiles.MonthlyProfileId, horizon);
        var examples = StrategyCatalogExamples.Create();
        var variant = examples.Single(x => x.Code == "ShortBalancedIronCondor") with
        {
            Settings = JsonSerializer.SerializeToElement(new
            {
                TargetNetDelta = 0m, BalanceTolerance = .05m, SymmetricWings = true,
                MinimumWingWidth = 50m, MaximumWingWidth = 50m, DeltaUnits = "UnderlyingEquivalent"
            })
        };
        var structure = examples.Single(x => x.Key == variant.Parent);
        var strategy = examples.Single(x => x.Key.Kind == StrategyCatalogKind.Strategy) with { Structures = [structure.Key] };
        var composition = new SelectionConstructionPolicy
        {
            SchemaVersion = 1, ParameterSetId = Guid.Parse("22222222-2222-2222-2222-222222222222"), Version = 1,
            MaximumLegs = 4, MinimumDaysToExpiry = 35, MaximumDaysToExpiry = 45,
            MinimumWingWidth = 50m, MaximumWingWidth = 50m, DeltaUnits = "UnderlyingEquivalent", MaximumDeltaTolerance = .1m
        };
        var selectionRef = new SelectionPipelinePolicyReference
        {
            Kind = CatalogPipelineParameterKind.TradeSelection, Id = common.ParameterSetId,
            Version = common.Version, PayloadSha256 = TradeSelectionPolicy.Hash(common)
        };
        var deployment = StrategyCatalogExamples.New(StrategyCatalogKind.Deployment, "MonthlyReplay", "Monthly replay") with
        {
            Parent = strategy.Key, Horizon = horizon, Variants = [variant.Key],
            Products = [new(1, "ES", "CME", "USD")], Capabilities = [new("validator", "StructureVariant", 1)],
            PipelineParameters = [
                new("selection-policy", CatalogPipelineParameterKind.TradeSelection, common.ParameterSetId, common.Version, selectionRef.PayloadSha256),
                new("composition-policy", CatalogPipelineParameterKind.OrderComposition, composition.ParameterSetId, composition.Version, composition.Hash())
            ]
        };
        var definitions = new[] { examples.Single(x => x.Key == strategy.Families[0]), structure, strategy, variant, deployment };
        var source = definitions.Select(x => new StoredStrategyCatalogDefinition(StrategyCatalogValidation.Freeze(x), StrategyCatalogValidation.ContentHash(x), CatalogLifecycleStatus.Published, at.AddDays(-2), "fixture", at.AddDays(-1), "fixture", null, null)).ToArray();
        var graph = new StrategyCatalogSnapshot(deployment.Key, at, source, SelectionCatalogTransport.GraphHash(deployment.Key, source));
        var config = Substitute.For<IConfigurationDbContext>();
        config.GetEffectiveTradeSelectionVersionAsync(common.ParameterSetId, 1, selectionRef.PayloadSha256, at, Arg.Any<CancellationToken>()).Returns(new ResolvedTradeSelectionParameterSet(common, selectionRef.PayloadSha256, ConfigurationParameterSetStatus.Published, at.AddDays(-1), null));
        var deployments = Substitute.For<IStrategyDeploymentReader>();
        deployments.ReadPublishedAsync(config, deployment.Key, at, Arg.Any<CancellationToken>()).Returns(graph);
        config.GetSelectionPipelinePolicyAsync(CatalogPipelineParameterKind.TradeSelection, common.ParameterSetId, 1, Arg.Any<CancellationToken>()).Returns(new SelectionPipelinePolicySnapshot { Kind = CatalogPipelineParameterKind.TradeSelection, Id = common.ParameterSetId, Version = 1, SchemaVersion = 1, PayloadJson = TradeSelectionPolicy.Serialize(common), PayloadSha256 = selectionRef.PayloadSha256, Status = CatalogLifecycleStatus.Published, EffectiveFromUtc = at.AddDays(-1) });
        config.GetSelectionPipelinePolicyAsync(CatalogPipelineParameterKind.OrderComposition, composition.ParameterSetId, 1, Arg.Any<CancellationToken>()).Returns(new SelectionPipelinePolicySnapshot { Kind = CatalogPipelineParameterKind.OrderComposition, Id = composition.ParameterSetId, Version = 1, SchemaVersion = composition.SchemaVersion, PayloadJson = composition.Serialize(), PayloadSha256 = composition.Hash(), Status = CatalogLifecycleStatus.Published, EffectiveFromUtc = at.AddDays(-1) });
        var permission = new TradeStrategyFamilyReference(0, 0) { CatalogDeployment = deployment.Key };
        const string family = "IronCondor";
        const string asset = "FuturesOptions";
        var authority = new PortfolioFundStrategySnapshot
        {
            WorkflowId = assessmentCommand.WorkflowId.Value,
            WorkflowRevision = 1,
            CorrelationId = assessmentCommand.CorrelationId,
            Portfolio = new() { PortfolioId = scopeId, PortfolioVersion = 1, Name = "Portfolio", OperatingState = PortfolioOperatingState.Active, ActivePolicyId = policyId, ActivePolicyVersion = 1, EffectiveFromUtc = at.AddDays(-1), CreatedOnUtc = at.AddDays(-1), CreatedBy = "fixture" },
            Fund = new()
            {
                PortfolioId = scopeId,
                FundId = scopeId,
                FundMandateVersion = 1,
                FundCode = "1",
                Name = "Fund",
                Objective = "Test",
                SchemaVersion = 3,
                TradingYear = at.Year,
                DecisionHorizon = horizon.ToString(),
                OperatingState = FundOperatingState.Active,
                UnderlyingUniverse = ["ES"],
                EligibleAssetTypes = ["Futures", "FuturesOptions"],
                PermittedDirections = ["Bullish", "Bearish", "Neutral"],
                PermittedConditions = Enum.GetNames<AssessmentCondition>().Where(x => x != "Undefined").ToArray(),
                PermittedTradeFamilies = ["Futures", "IronCondor", "VerticalSpread"],
                PermittedTradeStrategyFamilies = [permission],
                EffectiveFromUtc = at.AddDays(-1),
                CreatedOnUtc = at.AddDays(-1),
                CreatedBy = "fixture"
            },
            FinancialPolicy = new()
            {
                PortfolioId = scopeId,
                PolicyId = policyId,
                PolicyVersion = 1,
                Name = "Policy",
                OperatingState = PortfolioFinancialPolicyState.Active,
                CapitalBase = 10000,
                MaximumDeployableCapital = 10000,
                MaximumRiskPerTrade = 100,
                MaximumAggregateRisk = 1000,
                MaximumMargin = 1000,
                MaximumGrossNotional = 10000,
                MaximumOpenPositions = 10,
                EffectiveFromUtc = at.AddDays(-1),
                CreatedOnUtc = at.AddDays(-1),
                CreatedBy = "fixture",
                TradeFamilyLimits = [new() { CatalogDeployment = deployment.Key, Enabled = true, MaximumRiskPerTrade = 100, MaximumAggregateRisk = 1000, MaximumMargin = 1000, MaximumGrossNotional = 10000, MaximumOpenPositions = 10 }]
            },
            Allocation = new() { PortfolioId = scopeId, PortfolioVersion = 1, FundId = scopeId, FundMandateVersion = 1, AllocationVersion = 1, SourcePolicyId = policyId, SourcePolicyVersion = 1, EffectiveFromUtc = at.AddDays(-1), CreatedOnUtc = at.AddDays(-1), CreatedBy = "fixture" },
            RiskEnvelope = new() { PortfolioId = scopeId, PortfolioVersion = 1, FundId = scopeId, FundMandateVersion = 1, EnvelopeId = Guid.NewGuid(), EnvelopeVersion = 1, SourcePolicyId = policyId, SourcePolicyVersion = 1, CapacityState = FundCapacityState.Available, MaximumRiskPerTrade = 100, MaximumAggregateRisk = 1000, MaximumMargin = 1000, MaximumGrossNotional = 10000, MaximumOpenPositions = 10, EffectiveFromUtc = at.AddDays(-1), ExpiresAtUtc = at.AddMinutes(1), CreatedOnUtc = at.AddDays(-1), CreatedBy = "fixture" },
            Assignments = [new() { PortfolioId = scopeId, PortfolioVersion = 1, FundId = scopeId, FundMandateVersion = 1, SchemaVersion = 3, AssignmentVersion = 1, TradeStrategyFamily = permission, TradeTemplateId = deployment.Key.Id, TradeTemplateVersion = 1, Enabled = true, Priority = 1, DecisionHorizon = horizon.ToString(), UnderlyingUniverse = ["ES"], AssetType = asset, TradeFamily = family, TradeSelectionHintProfileId = common.ParameterSetId, TradeSelectionHintProfileVersion = 1, OrderCompositionProfileId = composition.ParameterSetId, OrderCompositionProfileVersion = 1, EffectiveFromUtc = at.AddDays(-1), CreatedOnUtc = at.AddDays(-1), CreatedBy = "fixture" }],
            ResolvedAtUtc = at,
            ValidUntilUtc = at.AddMinutes(1)
        };
        authority = authority with { PayloadSha256 = PortfolioCanonicalHash.Compute(authority) };
        var binding = await new TradeSelectionBindingResolver(config, deployments).ResolveAsync(authority, selectionRef, DateOnly.FromDateTime(assessmentCommand.TriggerEvent.CreatedOn));
        var view = assessmentCommand.WorkflowView with
        {
            FundId = scopeId,
            CorrelationId = assessmentCommand.CorrelationId,
            WorkflowRevision = 3,
            CurrentStage = StrategyWorkflowStage.TradeSelection,
            UpdatedAtUtc = at.AddMilliseconds(1),
            SelectionBinding = binding,
            MarketCondition = new() { ProcessingStatus = StrategyActorProcessingStatus.Completed, InputWorkflowRevision = 2, Result = assessmentEnvelope },
            TradeSelection = new() { ProcessingStatus = StrategyActorProcessingStatus.Processing, InputWorkflowRevision = 3 }
        };
        return TradeSelectionDispatch.Create(view, Guid.NewGuid());
    }
}
