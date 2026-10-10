using System.Collections.Immutable;
using TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class StrategyOptionChainParameterSetTests
{
    static StrategyOptionChainParameterSet Policy() => new()
    {
        ParameterSetId = Guid.NewGuid(), StrategyDefinitionId = Guid.NewGuid(), StrategyDefinitionVersion = 1, Name = "Development-only fixture",
        BiasRows = Enum.GetValues<OptionStrategyMarketBias>().Select(bias => new OptionStrategyBiasParameters
        {
            MarketBias = bias, MinimumDte = 20, MaximumDte = 45, PreferredDte = 30,
            PutDelta = new(.1m, .2m, .3m), CallDelta = new(.1m, .2m, .3m),
            PutWingWidths = [25], CallWingWidths = [25], MaximumLegSpreadPoints = 1, NetDeltaTolerance = .05m
        }).ToImmutableArray()
    };

    [Fact]
    public void All_three_global_bias_rows_roundtrip_without_implicit_production_defaults()
    {
        var policy = Policy(); var json = policy.Serialize(); var loaded = StrategyOptionChainParameterSet.Read(json);
        Assert.Equal(policy.Hash(), loaded.Hash());
        Assert.Equal(3, loaded.BiasRows.Length); Assert.Equal("Development", loaded.Environment); Assert.False(loaded.Enabled);
    }

    [Fact]
    public void Duplicate_missing_and_null_bias_rows_cannot_publish()
    {
        var policy = Policy(); var row = policy.BiasRows[0];
        Assert.Throws<ArgumentException>(() => (policy with { BiasRows = [row, row, row] }).Validate());
        Assert.Throws<ArgumentException>(() => (policy with { BiasRows = [row] }).Validate());
        Assert.Throws<ArgumentException>(() => (policy with { BiasRows = [row, null!, policy.BiasRows[2]] }).Validate());
        Assert.Throws<ArgumentException>(() => (row with { PutDelta = null! }).Validate());
    }

    [Fact]
    public void Policy_units_quality_and_resource_bounds_are_validated()
    {
        var policy = Policy(); var row = policy.BiasRows[0];
        Assert.Throws<ArgumentException>(() => (policy with { MaximumContracts = 2049 }).Validate());
        Assert.Throws<ArgumentException>(() => (policy with { CacheRangeMultiplier = double.NaN }).Validate());
        Assert.Throws<ArgumentException>(() => (row with { PreferredDte = 46 }).Validate());
        Assert.Throws<ArgumentException>(() => (row with { MaximumQuoteAgeMilliseconds = 6000 }).Validate());
        Assert.Throws<ArgumentException>(() => (row with { PutWingWidths = [25, 25] }).Validate());
        Assert.Throws<ArgumentException>(() => (row with { PutDelta = new(.4m, .2m, .3m) }).Validate());
        Assert.Throws<System.Text.Json.JsonException>(() => StrategyOptionChainParameterSet.Read(policy.Serialize().Replace("\"SchemaVersion\":1", "\"SchemaVersion\":1,\"Unknown\":true")));
    }
}
