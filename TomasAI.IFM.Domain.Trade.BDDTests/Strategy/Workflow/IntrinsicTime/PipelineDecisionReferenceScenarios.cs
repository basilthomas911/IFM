using FluentAssertions;
using TomasAI.IFM.Domain.Trade.Shared.DataExport;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Model;

namespace TomasAI.IFM.Domain.Trade.BDDTests.Strategy.Workflow.IntrinsicTime;

[Trait("Category", "BDD")]
public sealed class PipelineDecisionReferenceScenarios
{
    [Fact]
    public async Task Given_generated_pipeline_references_when_exported_then_the_reference_is_reviewable_and_non_authoritative()
    {
        var regimes = new RegimeDiscoveryDecisionReferenceGenerator().Generate();
        var conditions = new MarketConditionDecisionReferenceGenerator().Generate();
        var directory = Path.Combine(Path.GetTempPath(), $"ifm-pdr-bdd-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var regimeFile = Path.Combine(directory, "regime-discovery.csv");
            var conditionFile = Path.Combine(directory, "market-condition.csv");
            await new RegimeDiscoveryDecisionReferenceCsvAdapter().ExportAsync(regimes, regimeFile);
            await new MarketConditionDecisionReferenceCsvAdapter().ExportAsync(conditions, conditionFile);

            regimes.Should().HaveCount(12).And.OnlyContain(x => !x.IsAuthoritative);
            conditions.Should().HaveCount(12).And.OnlyContain(x => !x.IsAuthoritative);
            (await File.ReadAllLinesAsync(regimeFile)).Should().HaveCount(13);
            (await File.ReadAllLinesAsync(conditionFile)).Should().HaveCount(13);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
