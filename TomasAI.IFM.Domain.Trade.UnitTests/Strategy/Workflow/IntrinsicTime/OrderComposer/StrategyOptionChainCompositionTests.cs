using System.Collections.Immutable;
using TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;
namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.OrderComposer;
public sealed class StrategyOptionChainCompositionTests
{
    [Fact]
    public async Task Temporary_cache_unavailability_completes_signal_as_no_trade_without_retrying()
    {
        var c = await CompositionFixture.Command();
        var preparation = new TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Model.WorkflowSnapshotPreparation(c.WorkflowView);
        var context = NSubstitute.Substitute.For<TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Actor.IIntrinsicTimeStrategyWorkflowCommandContext>();
        NSubstitute.SubstituteExtensions.Returns(context.TimeProvider, new Clock(new DateTimeOffset(c.EvaluatedAtUtc)));
        var command = new TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands.FailOrderCompositionCommand
        {
            CommandId = Guid.NewGuid(), EntityId = c.WorkflowEntityId, WorkflowId = c.WorkflowId,
            InputWorkflowRevision = c.InputWorkflowRevision, SourceEventId = Guid.NewGuid(),
            Failure = new() { ErrorType = "OptionChainUnavailable", ErrorMessage = "ScopeNotReady", ErrorCode = 23100, FailedAtUtc = c.EvaluatedAtUtc }
        };
        var result = TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.FailOrderComposition.PrepareWorkflow(command, context, preparation);
        Assert.True(result.Success, result.ErrorMessage);
        var next = Assert.Single(preparation.Freeze()).WorkflowDefinition;
        Assert.Equal(TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model.StrategyWorkflowOutcome.NoTrade, next.Outcome);
        Assert.Equal(TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model.WorkflowStrategyMachineStatus.Completed, next.Status);
        Assert.Equal("ScopeNotReady", next.StopReasonCode);
    }
    sealed class Clock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }

    [Fact]
    public async Task Frozen_global_bias_targets_apply_without_reading_current_configuration()
    {
        var c = await CompositionFixture.Command("ShortBullishIronCondor");
        var selected = c.CompositionBinding.Selected;
        var policy = StrategyOptionChainParameterDefaults.IronCondor(Guid.NewGuid(), selected.StructureKey.Id, selected.StructureKey.Version)
            with { Enabled = true };
        var old = c.SelectionBinding.PipelinePolicies.Single(x => x.Id == selected.CompositionPolicyReference.Id);
        var construction = SelectionConstructionPolicy.Read(old.PayloadJson) with { SchemaVersion = 3, MarketData = null,
            OptionChainCache = new() { ParameterSetId = policy.ParameterSetId, Version = policy.Version, ConfigurationDigest = policy.Hash() } };
        var reference = selected.CompositionPolicyReference with { PayloadSha256 = construction.Hash() };
        c = c with
        {
            CompositionBinding = c.CompositionBinding with { Selected = selected with { CompositionPolicyReference = reference } },
            SelectionBinding = c.SelectionBinding with { PipelinePolicies = c.SelectionBinding.PipelinePolicies.Select(x => x.Id == old.Id
                ? x with { SchemaVersion = 3, PayloadJson = construction.Serialize(), PayloadSha256 = construction.Hash() } : x).ToArray() },
            MarketSnapshot = c.MarketSnapshot with { StrategyOptionChainParametersJson = policy.Serialize(), StrategyOptionChainBias = "Bullish" }
        };
        var rule = c.CompositionBinding.Rules.VariantRules.Single(x => x.VariantKey == selected.VariantKey) with { AllowedWidths = [50], HardBounds = [] };
        var resolved = CompositionParameterResolver.Resolve(rule, new Dictionary<CompositionFeature, decimal>(), default);
        resolved = resolved with { Values = resolved.Values with { MinimumDaysToExpiry = 1, MaximumDaysToExpiry = 90 } };
        var applied = StrategyOptionChainCompositionPolicy.Apply(c, rule, resolved);
        Assert.Equal(.20m, applied.Values.TargetPutDelta); Assert.Equal(.10m, applied.Values.TargetCallDelta);
        Assert.Equal(30, applied.Values.MinimumDaysToExpiry); Assert.Equal(45, applied.Values.MaximumDaysToExpiry);
        Assert.Equal(45, applied.Values.TargetDaysToExpiry);
        Assert.NotEqual(resolved.Hash, applied.Hash);
        var wrong = c with { MarketSnapshot = c.MarketSnapshot with { StrategyOptionChainParametersJson = (policy with { Version = 2 }).Serialize() } };
        Assert.Throws<CompositionException>(() => StrategyOptionChainCompositionPolicy.Apply(wrong, rule, resolved));
        Assert.Throws<CompositionException>(() => StrategyOptionChainCompositionPolicy.Apply(c, rule with { AllowedWidths = [25] }, resolved));
    }

    [Fact]
    public async Task Frozen_selection_binding_accepts_exact_schema_three_cache_policy()
    {
        var command = await TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.TradeSelection.TradeSelectionFixture.Command();
        var binding = command.SelectionBinding;
        var old = binding.PipelinePolicies.Single(x => x.Kind == TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog.CatalogPipelineParameterKind.OrderComposition);
        var policy = SelectionConstructionPolicy.Read(old.PayloadJson) with
        {
            SchemaVersion = 3,
            OptionChainCache = new() { ParameterSetId = Guid.NewGuid(), Version = 1, ConfigurationDigest = new('a', 64) }
        };
        var reference = binding.Candidates[0].CompositionPolicyReference with { PayloadSha256 = policy.Hash() };
        var candidate = binding.Candidates[0] with { CompositionPolicyReference = reference };
        var definitions = binding.CatalogDefinitions.Select(x => x.Key == candidate.DeploymentKey
            ? x with { PipelineParameters = x.PipelineParameters.Select(p => p.Id == old.Id ? p with { Hash = policy.Hash() } : p).ToArray() } : x).ToArray();
        definitions = definitions.Select(x => x with { ContentHash = TomasAI.IFM.Application.Storage.ConfigurationDb.StrategyCatalog.StrategyCatalogValidation.ContentHash(SelectionCatalogTransport.ToSource(x).Definition) }).ToArray();
        var graph = binding.DeploymentSnapshots.Single();
        graph = graph with { ContentHash = SelectionCatalogTransport.GraphHash(graph.DeploymentKey, definitions.Select(SelectionCatalogTransport.ToSource)) };
        candidate = candidate with { CandidateHash = TradeSelectionContracts.CandidateHash(candidate, graph) };
        binding = TradeSelectionContracts.Seal(binding with
        {
            CatalogDefinitions = definitions, DeploymentSnapshots = [graph], Candidates = [candidate],
            PipelinePolicies = binding.PipelinePolicies.Select(x => x.Id == old.Id
                ? x with { SchemaVersion = 3, PayloadJson = policy.Serialize(), PayloadSha256 = policy.Hash() } : x).ToArray()
        });
        TradeSelectionContracts.ValidateBinding(binding);
    }

    [Fact]
    public void Cache_policy_is_explicit_and_cannot_be_mixed_with_a_cold_market_universe()
    {
        var policy = new SelectionConstructionPolicy { SchemaVersion = 3, ParameterSetId = Guid.NewGuid(), Version = 1,
            MaximumLegs = 4, MinimumDaysToExpiry = 5, MaximumDaysToExpiry = 45, MinimumWingWidth = 50, MaximumWingWidth = 50,
            DeltaUnits = "UnderlyingEquivalent", MaximumDeltaTolerance = .1m,
            OptionChainCache = new() { ParameterSetId = Guid.NewGuid(), Version = 1, ConfigurationDigest = new('a', 64) } };
        Assert.Equal(policy.Hash(), SelectionConstructionPolicy.Read(policy.Serialize()).Hash());
        Assert.Throws<TradeSelectionValidationException>(() => (policy with { OptionChainCache = null }).Validate());
        Assert.Throws<TradeSelectionValidationException>(() => (policy with { MarketData = System.Text.Json.JsonSerializer.SerializeToElement(new { }) }).Validate());
    }
}
