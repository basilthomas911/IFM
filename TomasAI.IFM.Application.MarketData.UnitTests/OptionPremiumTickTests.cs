using System.Globalization;
using System.Text.Json;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.MarketData.Pricing;
using TomasAI.IFM.Framework.Serialization;
using static TomasAI.IFM.Application.MarketData.UnitTests.OrderCompositionPricingPrerequisiteTests;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class OptionPremiumTickTests
{
    static OptionPricingConvention Reviewed() => Contract() with { SchemaVersion = 2, TickSize = .05m,
        PremiumTickRule = OptionPremiumTickRule.CmeEsGlobex358A, TickRuleVersion = OptionPremiumTicks.CmeEsGlobexVersion };

    [Theory]
    [InlineData("0", ".05")] [InlineData("5", ".05")] [InlineData("5.01", ".10")]
    [InlineData("20", ".10")] [InlineData("20.01", ".25")]
    [InlineData("100", ".25")] [InlineData("100.01", ".50")]
    [InlineData("-5", ".05")] [InlineData("-5.01", ".10")]
    [InlineData("-20", ".10")] [InlineData("-20.01", ".25")]
    [InlineData("-100", ".25")] [InlineData("-100.01", ".50")]
    public void Current_rule_resolves_boundary_and_signed_net_premiums(string premium, string expected)
        => Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture),
            OptionPremiumTicks.GetIncrement(Reviewed(), decimal.Parse(premium, CultureInfo.InvariantCulture)));

    [Fact]
    public void Combination_leg_increment_is_distinct_and_extreme_net_premiums_do_not_overflow()
    {
        Assert.Equal(.05m, OptionPremiumTicks.GetIncrement(Reviewed(), 500m, combinationLeg: true));
        Assert.Equal(.50m, OptionPremiumTicks.GetIncrement(Reviewed(), decimal.MinValue));
        Assert.Equal(.50m, OptionPremiumTicks.GetIncrement(Reviewed(), decimal.MaxValue));
    }

    [Fact]
    public void Shared_serialization_preserves_explicit_rule_and_legacy_semantic_shape()
    {
        var reviewed = Reviewed();
        var shared = MessagePackBinarySerializer.Shared;
        Assert.Equal(reviewed, shared.Deserialize<OptionPricingConvention>(shared.Serialize(reviewed)!));
        Assert.Null(OptionPricingQualification.Validate(reviewed, At));
        var legacy = Contract();
        Assert.DoesNotContain("PremiumTickRule", JsonSerializer.Serialize(legacy));
        Assert.Equal(legacy.TickSize, OptionPremiumTicks.GetIncrement(legacy, 1m));
        Assert.Equal(legacy.TickSize, OptionPremiumTicks.GetIncrement(legacy, 101m));
    }

    [Fact]
    public void Invalid_schema_rule_product_or_minimum_increment_fails_qualification()
    {
        var c = Reviewed();
        foreach (var invalid in new[] { c with { SchemaVersion = 3 }, c with { PremiumTickRule = (OptionPremiumTickRule)99 },
            c with { PremiumTickRule = OptionPremiumTickRule.Unspecified }, c with { TickSize = .25m },
            c with { TickRuleVersion = "unverified" }, c with { Multiplier = 5 }, c with { Exchange = "XNAS" },
            c with { SchemaVersion = 1 } })
        {
            Assert.NotNull(OptionPricingQualification.Validate(invalid, At));
            Assert.Throws<ArgumentException>(() => OptionPremiumTicks.GetIncrement(invalid, 10m));
        }
    }
}
