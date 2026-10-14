using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.UnitTests;

public sealed class OptionSpreadLegSelectionTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Delta_is_closest_from_below_and_never_exceeds_target(bool call)
    {
        var rows = new[] { Contract("above", .161, call), Contract("below", .155, call), Contract("far", .10, call) };
        Assert.Equal(new[] { "below", "far" }, OptionSpreadLegSelection.ShortCandidates(rows, call, .16m, null).Select(x => x.ContractId));
        Assert.Equal("exact", OptionSpreadLegSelection.ShortCandidates(rows.Append(Contract("exact", .16, call)), call, .16m, null).First().ContractId);
        Assert.Empty(OptionSpreadLegSelection.ShortCandidates([Contract("above", .161, call)], call, .16m, null));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Wing_is_closest_from_above_and_never_narrower(bool upward)
    {
        decimal Strike(decimal width) => 5000m + (upward ? width : -width);
        var rows = new[] { ("narrow", Strike(45)), ("nearest", Strike(55)), ("wide", Strike(75)), ("wrong-side", Strike(-50)) };
        Assert.Equal("nearest", OptionSpreadLegSelection.Wing(rows, 5000, 50, upward));
        Assert.Equal("exact", OptionSpreadLegSelection.Wing(rows.Append(("exact", Strike(50))), 5000, 50, upward));
        Assert.Null(OptionSpreadLegSelection.Wing([("narrow", Strike(45)), ("wrong-side", Strike(-50))], 5000, 50, upward));
    }

    [Fact]
    public void Invalid_stale_and_unqualified_deltas_are_excluded()
    {
        var valid = Contract("valid", .15, true);
        var rows = new[] { valid with { ContractId = "stale", IsStale = true }, valid with { ContractId = "unqualified", GreeksValid = false },
            valid with { Delta = double.NaN }, valid with { Delta = double.PositiveInfinity }, valid with { Delta = null } };
        Assert.Empty(OptionSpreadLegSelection.ShortCandidates(rows, true, .16m, null));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Frozen_emulator_opt_in_preserves_delta_limits_and_live_stale_rejection(bool call)
    {
        var rows = new[] { Contract("above", .161, call), Contract("nearest", .155, call), Contract("far", .10, call) }
            .Select(x => x with { IsStale = true }).ToArray();
        Assert.Empty(OptionSpreadLegSelection.ShortCandidates(rows, call, .16m, null));
        Assert.Equal(new[] { "nearest", "far" }, OptionSpreadLegSelection.ShortCandidates(rows, call, .16m, null,
            allowFrozenEmulatorPreview: true).Select(x => x.ContractId));
        Assert.Empty(OptionSpreadLegSelection.ShortCandidates([
            rows[0] with { Delta = null }, rows[1] with { GreeksValid = false },
            rows[2] with { Delta = double.NaN }], call, .16m, null, allowFrozenEmulatorPreview: true));
        Assert.All(rows, x => Assert.True(x.IsStale));
    }

    private static EvaluatedOptionContractReadModel Contract(string id, double delta, bool call) => new(
        id, 5000m, call, 1m, 2m, 1, 1, null, null, .2, 1.5, call ? delta : -delta,
        null, null, null, null, null, null, true, false, DateTimeOffset.UtcNow, null, DateTimeOffset.UtcNow);
}
