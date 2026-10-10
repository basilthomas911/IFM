using System.Collections.Immutable;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;
using Cache = TomasAI.IFM.Application.MarketData.OptionChainCache.OptionChainCache;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.OrderComposer;

public sealed class CachedCompositionIntegrationTests
{
    [Theory]
    [InlineData("ShortBalancedIronCondor", TimeFrameType.Weekly)]
    [InlineData("ShortBullishIronCondor", TimeFrameType.Monthly)]
    [InlineData("ShortBearishIronCondor", TimeFrameType.Weekly)]
    [InlineData("BullCallDebit", TimeFrameType.Monthly)]
    [InlineData("BearPutDebit", TimeFrameType.Weekly)]
    public async Task Cache_freezes_exact_prepriced_input_and_composer_produces_deterministic_candidate(string variant, TimeFrameType horizon)
    {
        var command = await CompositionFixture.Command(variant, horizon, cacheDefaults: true);
        var snapshot = CompositionSnapshotAdapter.To(command.MarketSnapshot);
        var profile = SelectionConstructionPolicy.Read(TradeSelectionContracts.Policy(command.SelectionBinding,
            command.CompositionBinding.Selected.CompositionPolicyReference).PayloadJson).OptionChainCache!;
        var date = snapshot.StrategyOptionChainValueDate!.Value;
        var clock = new FixedClock(snapshot.EvaluatedAtUtc);
        using var cache = new Cache(clock); cache.Admit(snapshot.GenerationId);
        Assert.True(cache.Publish(snapshot, date, profile.ConfigurationDigest, 1));
        var request = new OptionChainSnapshotRequest(snapshot.ScopeId, snapshot.GenerationId, date, horizon.ToString(), snapshot.ValidUntilUtc)
        { ConfigurationDigest = profile.ConfigurationDigest, StrategyDefinitionId = command.CompositionBinding.Selected.StructureKey.Id,
            StrategyDefinitionVersion = command.CompositionBinding.Selected.StructureKey.Version, WorkflowId = command.WorkflowId.Value, WorkflowRevision = command.InputWorkflowRevision };
        var provider = Substitute.For<ICompositionMarketDataApi>();
        var store = Substitute.For<ICompositionPreparationStore>();
        CompositionPreparation? committed = null;
        store.ReadAsync(Arg.Any<CompositionPreparationKey>(), Arg.Any<CancellationToken>()).Returns(_ => committed);
        store.CommitAsync(Arg.Any<CompositionPreparation>(), Arg.Any<CancellationToken>()).Returns(call => committed ??= call.Arg<CompositionPreparation>());
        var preparation = new CompositionPreparationService(provider, store, clock);
        var key = new CompositionPreparationKey(command.WorkflowId.Value, command.InputWorkflowRevision, command.InputSha256);
        var accepted = await preparation.PrepareCachedAsync(key, cache, request, default);
        Assert.Null(accepted.Failure); Assert.NotNull(accepted.Preparation);
        command = CompositionFixture.Seal(command with { MarketSnapshot = CompositionSnapshotAdapter.From(accepted.Preparation!.Snapshot) });
        var composer = new TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model.OrderComposer(new Black76ComposerPricer());
        var first = composer.Calculate(command);
        Assert.True(first.Candidate is not null, string.Join(",", first.Reasons));
        var duplicate = composer.Calculate(command);
        Assert.Equal(first.Candidate!.CandidateHash, duplicate.Candidate!.CandidateHash);
        cache.Fence(snapshot.GenerationId);
        Assert.Equal(accepted.Preparation, (await preparation.PrepareCachedAsync(key, cache, request, default)).Preparation);
        Assert.Empty(provider.ReceivedCalls());
        var output = Environment.GetEnvironmentVariable("IFM_COMPOSITION_BENCHMARK_FIXTURES");
        if (!string.IsNullOrEmpty(output))
        {
            Directory.CreateDirectory(output);
            await File.WriteAllTextAsync(Path.Combine(output, variant + ".json"), System.Text.Json.JsonSerializer.Serialize(command));
        }
    }
    sealed class FixedClock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
}
