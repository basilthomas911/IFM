using TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache;
using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
namespace TomasAI.IFM.Domain.Reference.UnitTests.ParameterSets;
public sealed class StrategyOptionChainParameterTests
{
    [Fact]
    public void Registry_validates_all_immutable_bias_rows_and_rejects_unknown_fields()
    {
        var policy = StrategyOptionChainParameterDefaults.IronCondor(Guid.NewGuid(), Guid.NewGuid(), 1);
        var descriptor = new StrategyOptionChainParameterModel();
        Assert.Empty(descriptor.Validate(policy.Serialize(), 1));
        Assert.Empty(descriptor.Validate(descriptor.CreateDraftPayload(Guid.NewGuid()), 1));
        Assert.Throws<ArgumentException>(() => (policy with { Enabled = true, StrategyDefinitionId = Guid.Empty }).Validate());
        var unknown = policy.Serialize().Replace("\"MarketBias\":0", "\"UnknownBiasField\":7,\"MarketBias\":0");
        Assert.NotEmpty(descriptor.Validate(unknown, 1));
        Assert.True(ParameterComponentModelRegistry.Contains(StrategyOptionChainParameterModel.ComponentCode));
    }
}
