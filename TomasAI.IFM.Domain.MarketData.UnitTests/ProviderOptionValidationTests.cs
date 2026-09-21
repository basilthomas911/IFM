using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.UnitTests;

public sealed class ProviderOptionValidationTests
{
    [Fact]
    public void Provider_reference_does_not_require_legacy_lookup_aliases_but_still_requires_exact_multiplier()
    {
        var rules = new FuturesOptionContractReadModelValidationRules(Substitute.For<IReferenceLookupService>());
        var option = new FuturesOptionContractReadModel("ES20261218C6500.5", "fixture", "ES", "fixture", "FOP",
            "USD", "XCME", "50", new(2026, 12, 18), 6500.5, "Call")
        { SchemaVersion = 1, MultiplierValue = 50, StrikePriceDecimal = 6500.5m };
        Assert.Empty(rules.Execute(option));
        Assert.NotEmpty(rules.Execute(option with { SchemaVersion = 0 }));
        Assert.NotEmpty(rules.Execute(option with { MultiplierValue = 100 }));
        Assert.NotEmpty(rules.Execute(option with { Currency = "" }));
        Assert.NotEmpty(rules.Execute(option with { Exchange = "" }));
    }
}
